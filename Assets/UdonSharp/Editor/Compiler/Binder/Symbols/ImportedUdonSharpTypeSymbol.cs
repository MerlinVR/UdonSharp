
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpTypeSymbol : TypeSymbol
    {
        public ImportedUdonSharpTypeSymbol(INamedTypeSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            throw new System.NotImplementedException();
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, BindContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}
