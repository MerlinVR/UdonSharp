
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UdonSharp.Compiler.UdonSharpCompiler;

namespace UdonSharp.Compiler
{

    /// <summary>
    /// Handles compiling a class into Udon assembly
    /// </summary>
    public class CompilationModule
    {
        public UdonSharpProgramAsset programAsset { get; private set; }
        UdonSharpSettings settings;

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

            if (programAsset.sourceCsScript == null)
                throw new System.ArgumentException($"Asset '{AssetDatabase.GetAssetPath(programAsset)}' does not have a valid program source to compile from");
            
            settings = UdonSharpSettings.GetSettings();
        }

        void LogException(CompileTaskResult result, System.Exception e, SyntaxNode node, out string logMessage)
        {
            logMessage = "";

            if (node != null)
            {
                FileLinePositionSpan lineSpan = node.GetLocation().GetLineSpan();

                CompileError error = new CompileError();
                error.script = programAsset.sourceCsScript;
                error.errorStr = $"{e.GetType()}: {e.Message}";
                error.lineIdx = lineSpan.StartLinePosition.Line;
                error.charIdx = lineSpan.StartLinePosition.Character;

                result.compileErrors.Add(error);
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

        }

        public CompileTaskResult Compile(List<ClassDefinition> classDefinitions, Microsoft.CodeAnalysis.SyntaxTree syntaxTree, string sourceCode, bool isEditorBuild)
        {
            CompileTaskResult result = new CompileTaskResult();
            result.programAsset = programAsset;

            moduleSymbols.OpenSymbolTable();

            ClassDebugInfo debugInfo = null;

            if (settings == null || settings.buildDebugInfo)
            {
                debugInfo = new ClassDebugInfo(sourceCode, settings == null || settings.includeInlineCode);
            }

            MethodVisitor methodVisitor = new MethodVisitor(resolver, moduleSymbols, moduleLabels);

            try
            {
                methodVisitor.Visit(syntaxTree.GetRoot());
            }
            catch (System.Exception e)
            {
                LogException(result, e, methodVisitor.visitorContext.currentNode, out string logMessage);

                programAsset.compileErrors.Add(logMessage);

                ErrorCount++;
            }

            if (ErrorCount > 0)
                return result;

            PropertyVisitor propertyVisitor = new PropertyVisitor(resolver, moduleSymbols, moduleLabels);

            try
            {
                propertyVisitor.Visit(syntaxTree.GetRoot());
            }
            catch (System.Exception e)
            {
                LogException(result, e, propertyVisitor.visitorContext.currentNode, out string logMessage);

                programAsset.compileErrors.Add(logMessage);

                ErrorCount++;
            }

            if (ErrorCount > 0)
                return result;

            UdonSharpFieldVisitor fieldVisitor = new UdonSharpFieldVisitor(fieldsWithInitializers, resolver, moduleSymbols, moduleLabels, classDefinitions, debugInfo);
            fieldVisitor.visitorContext.definedProperties = propertyVisitor.definedProperties;

            try
            {
                fieldVisitor.Visit(syntaxTree.GetRoot());
            }
            catch (System.Exception e)
            {
                LogException(result, e, fieldVisitor.visitorContext.currentNode, out string logMessage);

                programAsset.compileErrors.Add(logMessage);

                ErrorCount++;
            }

            if (ErrorCount > 0)
                return result;

            ASTVisitor visitor = new ASTVisitor(resolver, moduleSymbols, moduleLabels, methodVisitor.definedMethods, propertyVisitor.definedProperties, classDefinitions, debugInfo);
            visitor.visitorContext.onModifyCallbackFields = fieldVisitor.visitorContext.onModifyCallbackFields;

            try
            {
                visitor.Visit(syntaxTree.GetRoot());
                visitor.VerifyIntegrity();
            }
            catch (System.Exception e)
            {
                LogException(result, e, visitor.visitorContext.currentNode, out string logMessage);
                
                programAsset.compileErrors.Add(logMessage);

                ErrorCount++;
            }

            if (ErrorCount > 0)
            {
                return result;
            }

            moduleSymbols.CloseSymbolTable();

            if (ErrorCount == 0)
            {
                compiledClassDefinition = classDefinitions.Find(e => e.userClassType == visitor.visitorContext.behaviourUserType);

                string dataBlock = BuildHeapDataBlock();
                string codeBlock = visitor.GetCompiledUasm();

                result.compiledAssembly = dataBlock + codeBlock;
                result.symbolCount = (uint)(moduleSymbols.GetAllUniqueChildSymbols().Count + visitor.GetExternStrCount());

                programAsset.behaviourIDHeapVarName = visitor.GetIDHeapVarName();

                programAsset.fieldDefinitions = fieldVisitor.visitorContext.localFieldDefinitions;
                programAsset.behaviourSyncMode = visitor.visitorContext.behaviourSyncMode;

                if (debugInfo != null)
                    debugInfo.FinalizeDebugInfo();

                UdonSharpEditorCache.Instance.SetDebugInfo(programAsset, isEditorBuild ? UdonSharpEditorCache.DebugInfoType.Editor : UdonSharpEditorCache.DebugInfoType.Client, debugInfo);
            }

            return result;
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
                if (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public) && !symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Readonly))
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
                .OrderBy(e => (e.declarationType & SymbolDeclTypeFlags.Reflection) != 0)
                .ThenBy(e => (e.declarationType & SymbolDeclTypeFlags.Public) != 0)
                .ThenBy(e => (e.declarationType & SymbolDeclTypeFlags.Private) != 0)
                .ThenBy(e => (e.declarationType & SymbolDeclTypeFlags.This) != 0)
                .ThenBy(e => (e.declarationType & SymbolDeclTypeFlags.Internal) == 0)
                .ThenBy(e => (e.declarationType &SymbolDeclTypeFlags.Constant) != 0)
                .ThenByDescending(e => e.symbolCsType.Name)
                //.ThenByDescending(e => e.symbolUniqueName)
                .Reverse()
            )
            {
                if ((symbol.declarationType & SymbolDeclTypeFlags.This) != 0)
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