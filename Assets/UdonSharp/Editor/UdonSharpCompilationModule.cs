using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

        public CompilationModule(MonoScript sourceScript)
        {
            source = sourceScript;
            resolver = new ResolverContext();
            moduleSymbols = new SymbolTable(resolver, null);
        }

        public string Compile()
        {
            sourceCode = File.ReadAllText(AssetDatabase.GetAssetPath(source));

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);

            ASTVisitor visitor = new ASTVisitor(resolver, moduleSymbols);
            visitor.Visit(tree.GetRoot());
            visitor.VerifyIntegrity();


            string dataBlock = BuildHeapDataBlock();
            string codeBlock = visitor.GetCompiledUasm();

            return dataBlock + codeBlock;
        }

        private string BuildHeapDataBlock()
        {
            AssemblyBuilder builder = new AssemblyBuilder();
            HashSet<string> uniqueSymbols = new HashSet<string>();

            builder.AppendLine(".data_start", 0);
            builder.AppendLine("", 0);

            foreach (SymbolDefinition symbol in moduleSymbols.GetAllUniqueChildSymbols())
            {
                if (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public))
                    builder.AppendLine($".export {symbol.symbolUniqueName}", 1);
            }

            builder.AppendLine("", 0);

            foreach (SymbolDefinition symbol in moduleSymbols.GetAllUniqueChildSymbols())
            {
                builder.AppendLine($"{symbol.symbolUniqueName}: %{symbol.symbolResolvedTypeName}, null", 1);
            }

            builder.AppendLine("", 0);
            builder.AppendLine(".data_end", 0);
            builder.AppendLine("", 0);

            return builder.GetAssemblyStr();
        }
    }

}