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
        public UdonSharpProgramAsset programAsset { get; private set; }
        private string sourceCode;

        public ResolverContext resolver { get; private set; }
        public SymbolTable moduleSymbols { get; private set; }
        public LabelTable moduleLabels { get; private set; }
        
        public HashSet<FieldDeclarationSyntax> fieldsWithInitializers;

        public CompilationModule(UdonSharpProgramAsset sourceAsset)
        {
            programAsset = sourceAsset;
            resolver = new ResolverContext();
            moduleSymbols = new SymbolTable(resolver, null);
            moduleLabels = new LabelTable();
            fieldsWithInitializers = new HashSet<FieldDeclarationSyntax>();
        }

        public int Compile(List<ClassDefinition> classDefinitions)
        {
            sourceCode = File.ReadAllText(AssetDatabase.GetAssetPath(programAsset.sourceCsScript));

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            int errorCount = 0;

            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    errorCount++;

                    LinePosition linePosition = diagnostic.Location.GetLineSpan().StartLinePosition;

                    UdonSharpUtils.LogBuildError($"error {diagnostic.Descriptor.Id}: {diagnostic.GetMessage()}",
                                                    AssetDatabase.GetAssetPath(programAsset.sourceCsScript).Replace("/", "\\"),
                                                    linePosition.Line,
                                                    linePosition.Character);
                }

                if (errorCount > 0)
                {
                    return errorCount;
                }
            }
            
            var rewriter = new UdonSharpFieldRewriter(fieldsWithInitializers);
            var result = rewriter.Visit(tree.GetRoot());

            MethodVisitor methodVisitor = new MethodVisitor(resolver, moduleSymbols, moduleLabels);
            methodVisitor.Visit(result);

            ASTVisitor visitor = new ASTVisitor(resolver, moduleSymbols, moduleLabels, methodVisitor.definedMethods, classDefinitions);

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

                    UdonSharpUtils.LogBuildError($"{e.GetType()}: {e.Message}",
                                                    AssetDatabase.GetAssetPath(programAsset.sourceCsScript).Replace("/", "\\"),
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

            programAsset.SetUdonAssembly(dataBlock + codeBlock);
            programAsset.AssembleCsProgram();

            programAsset.fieldDefinitions = visitor.visitorContext.localFieldDefinitions;

            return errorCount;
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