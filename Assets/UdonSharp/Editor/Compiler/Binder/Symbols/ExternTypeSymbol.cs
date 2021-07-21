
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class ExternTypeSymbol : TypeSymbol
    {
        public ExternTypeSymbol(INamedTypeSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {
        }

        public override bool IsExtern => true;

        public override void Bind(BindContext context)
        {
            // Extern types do not need to be bound as they will always have their members bound lazily only when referenced and fields just exist already.
            return;
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, BindContext context)
        {
            return new ExternMethodSymbol((IMethodSymbol)roslynSymbol, context);
        }

        public override string ToString()
        {
            return RoslynSymbol.ToString();
        }
    }
}
