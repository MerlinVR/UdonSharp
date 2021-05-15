using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpMethodSymbol : MethodSymbol
    {
        public ImportedUdonSharpMethodSymbol(IMethodSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {
        }
    }
}
