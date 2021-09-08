
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpMethodSymbol : MethodSymbol
    {
        public ImportedUdonSharpMethodSymbol(IMethodSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
        }
    }
}
