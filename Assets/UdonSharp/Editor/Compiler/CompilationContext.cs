
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler
{
    internal class CompilationContext
    {
        public CSharpCompilation RoslynCompilation { get; private set; }

        public CompilationContext(CSharpCompilation compilation)
        {
            RoslynCompilation = compilation;
        }

        public SemanticModel GetSemanticModel(SyntaxTree modelTree)
        {
            return RoslynCompilation.GetSemanticModel(modelTree);
        }


    }
}
