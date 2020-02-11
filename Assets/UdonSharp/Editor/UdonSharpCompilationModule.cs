using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace UdonSharp
{

    /// <summary>
    /// Handles compiling a class into Udon assembly
    /// </summary>
    public class CompilationModule
    {
        private MonoScript source;
        private string sourceCode;

        public ResolverContext resolver { get; private set; }
        public SymbolTable moduleSymbols { get; private set; }
        public LabelTable moduleLabels { get; private set; }
        
        public HashSet<FieldDeclarationSyntax> fieldsWithInitializers;

        public CompilationModule(MonoScript sourceScript)
        {
            source = sourceScript;
            resolver = new ResolverContext();
            moduleSymbols = new SymbolTable(resolver, null);
            moduleLabels = new LabelTable();
            fieldsWithInitializers = new HashSet<FieldDeclarationSyntax>();
        }

        private void LogBuildError(string message, string filePath, int line, int character)
        {
            MethodInfo buildErrorLogMethod = typeof(UnityEngine.Debug).GetMethod("LogPlayerBuildError", BindingFlags.NonPublic | BindingFlags.Static);

            buildErrorLogMethod.Invoke(null, new object[] {
                        message,
                        filePath,
                        line + 1,
                        character });
        }

        public (string, int) Compile()
        {
            sourceCode = File.ReadAllText(AssetDatabase.GetAssetPath(source));

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            int errorCount = 0;

            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    errorCount++;

                    LinePosition linePosition = diagnostic.Location.GetLineSpan().StartLinePosition;

                    LogBuildError($"[UdonSharp] error {diagnostic.Descriptor.Id}: {diagnostic.GetMessage()}",
                                    AssetDatabase.GetAssetPath(source).Replace("/", "\\"),
                                    linePosition.Line,
                                    linePosition.Character);
                }

                if (errorCount > 0)
                {
                    //Debug.LogError("Udon Sharp script has errors, compilation aborted.");
                    return ("error", errorCount);
                }
            }
            
            var rewriter = new UdonSharpFieldRewriter(fieldsWithInitializers);
            var result = rewriter.Visit(tree.GetRoot());

            NamespaceVisitor namespaceVisitor = new NamespaceVisitor(resolver);
            namespaceVisitor.Visit(result);

            MethodVisitor methodVisitor = new MethodVisitor(resolver, moduleSymbols, moduleLabels);
            methodVisitor.Visit(result);

            ASTVisitor visitor = new ASTVisitor(resolver, moduleSymbols, moduleLabels, methodVisitor.definedMethods);

            try
            {
                visitor.Visit(result);
                visitor.VerifyIntegrity();
            }
            catch (System.Exception e)
            {
                SyntaxNode currentNode = visitor.visitorContext.currentNode;

                if (currentNode != null)
                {
                    FileLinePositionSpan lineSpan = currentNode.GetLocation().GetLineSpan();

                    LogBuildError($"[UdonSharp] {e.GetType()}: {e.Message}",
                                    AssetDatabase.GetAssetPath(source).Replace("/", "\\"),
                                    lineSpan.StartLinePosition.Line,
                                    lineSpan.StartLinePosition.Character);
                }
                else
                {
                    Debug.LogException(e);
                }

                errorCount++;
            }

            string dataBlock = BuildHeapDataBlock();
            string codeBlock = visitor.GetCompiledUasm();

            return (dataBlock + codeBlock, errorCount);
        }

        private string BuildHeapDataBlock()
        {
            AssemblyBuilder builder = new AssemblyBuilder();
            HashSet<string> uniqueSymbols = new HashSet<string>();

            builder.AppendLine(".data_start", 0);
            builder.AppendLine("", 0);

            List<SymbolDefinition> allSymbols = moduleSymbols.GetAllUniqueChildSymbols();

            foreach (SymbolDefinition symbol in allSymbols)
            {
                if (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public))
                    builder.AppendLine($".export {symbol.symbolUniqueName}", 1);
            }

            foreach (SymbolDefinition symbol in allSymbols)
            {
                if (symbol.syncMode != UdonSyncMode.NotSynced)
                    builder.AppendLine($".sync {symbol.symbolUniqueName}, {System.Enum.GetName(typeof(UdonSyncMode), symbol.syncMode).ToLowerInvariant()}", 1);
            }

            builder.AppendLine("", 0);

            // Prettify the symbol order in the data block
            foreach (SymbolDefinition symbol in moduleSymbols.GetAllUniqueChildSymbols()
                .OrderBy(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.Public))
                .ThenBy(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.Private))
                .ThenBy(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.This))
                .ThenBy(e => !e.declarationType.HasFlag(SymbolDeclTypeFlags.Internal))
                .ThenBy(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.Constant))
                .ThenByDescending(e => e.symbolCsType.Name)
                .ThenByDescending(e => e.symbolUniqueName).Reverse())
            {
                if (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.This))
                    builder.AppendLine($"{symbol.symbolUniqueName}: %{symbol.symbolResolvedTypeName}, this", 1);
                else
                    builder.AppendLine($"{symbol.symbolUniqueName}: %{symbol.symbolResolvedTypeName}, null", 1);
            }

            builder.AppendLine("", 0);
            builder.AppendLine(".data_end", 0);
            builder.AppendLine("", 0);

            return builder.GetAssemblyStr();
        }
    }

}