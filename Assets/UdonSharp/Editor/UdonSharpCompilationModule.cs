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
using UnityEngine.Profiling;

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

        public ClassDefinition compiledClassDefinition = null;

        public int ErrorCount { get; private set; } = 0;

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
            if (programAsset.sourceCsScript == null)
                throw new System.ArgumentException($"Asset '{AssetDatabase.GetAssetPath(programAsset)}' does not have a valid program source to compile from");

            Profiler.BeginSample("Compile Module");

            programAsset.compileErrors.Clear();

            sourceCode = UdonSharpUtils.ReadFileTextSync(AssetDatabase.GetAssetPath(programAsset.sourceCsScript));

            Profiler.BeginSample("Parse AST");
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            Profiler.EndSample();

            int errorCount = 0;

            string errorString = "";

            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    errorCount++;

                    LinePosition linePosition = diagnostic.Location.GetLineSpan().StartLinePosition;

                    errorString = UdonSharpUtils.LogBuildError($"error {diagnostic.Descriptor.Id}: {diagnostic.GetMessage()}",
                                                                AssetDatabase.GetAssetPath(programAsset.sourceCsScript).Replace("/", "\\"),
                                                                linePosition.Line,
                                                                linePosition.Character);

                    programAsset.compileErrors.Add(errorString);
                }
            }

            if (errorCount > 0)
            {
                ErrorCount = errorCount;
                Profiler.EndSample();
                return errorCount;
            }

            Profiler.BeginSample("Visit");
            UdonSharpFieldVisitor fieldVisitor = new UdonSharpFieldVisitor(fieldsWithInitializers);
            fieldVisitor.Visit(tree.GetRoot());

            MethodVisitor methodVisitor = new MethodVisitor(resolver, moduleSymbols, moduleLabels);
            methodVisitor.Visit(tree.GetRoot());

            UdonSharpSettings settings = UdonSharpSettings.GetSettings();

            ClassDebugInfo debugInfo = null;

            if (settings == null || settings.buildDebugInfo)
            {
                debugInfo = new ClassDebugInfo(sourceCode, settings == null || settings.includeInlineCode);
            }

            ASTVisitor visitor = new ASTVisitor(resolver, moduleSymbols, moduleLabels, methodVisitor.definedMethods, classDefinitions, debugInfo);

            try
            {
                visitor.Visit(tree.GetRoot());
                visitor.VerifyIntegrity();
                foreach (SymbolDefinition d in visitor.visitorContext.topTable.GetAllSymbols(true))
                {
                    d.AssertCOWClosed();
                }
            }
            catch (System.Exception e)
            {
                SyntaxNode currentNode = visitor.visitorContext.currentNode;

                string logMessage = "";

                if (currentNode != null)
                {
                    FileLinePositionSpan lineSpan = currentNode.GetLocation().GetLineSpan();

                    logMessage = UdonSharpUtils.LogBuildError($"{e.GetType()}: {e.Message}",
                                                                AssetDatabase.GetAssetPath(programAsset.sourceCsScript).Replace("/", "\\"),
                                                                lineSpan.StartLinePosition.Line,
                                                                lineSpan.StartLinePosition.Character);
                }
                else
                {
                    logMessage = e.ToString();
                    Debug.LogException(e);
                }
#if UDONSHARP_DEBUG
                Debug.LogException(e);
                Debug.LogError(e.StackTrace);
#endif

                programAsset.compileErrors.Add(logMessage);

                errorCount++;
            }
            Profiler.EndSample();

            if (errorCount == 0)
            {
                compiledClassDefinition = classDefinitions.Find(e => e.userClassType == visitor.visitorContext.behaviourUserType);

                Profiler.BeginSample("Build assembly");
                string dataBlock = BuildHeapDataBlock();
                string codeBlock = visitor.GetCompiledUasm();

                programAsset.SetUdonAssembly(dataBlock + codeBlock);
                Profiler.EndSample();
                
                Profiler.BeginSample("Assemble Program");
                programAsset.AssembleCsProgram((uint)(moduleSymbols.GetAllUniqueChildSymbols().Count + visitor.GetExternStrCount()));
                Profiler.EndSample();
                programAsset.behaviourIDHeapVarName = visitor.GetIDHeapVarName();

                programAsset.fieldDefinitions = visitor.visitorContext.localFieldDefinitions;

                if (debugInfo != null)
                    debugInfo.FinalizeDebugInfo();

                programAsset.debugInfo = debugInfo;
            }

            Profiler.EndSample();

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
            // Reflection info goes first so that we can use it for knowing what script threw an error from in game logs
            foreach (SymbolDefinition symbol in moduleSymbols.GetAllUniqueChildSymbols()
                .OrderBy(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.Reflection))
                .ThenBy(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.Public))
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