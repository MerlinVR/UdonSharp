using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class ArrayTypeSymbol : TypeSymbol
    {
        public TypeSymbol ElementType { get; private set; }

        public ArrayTypeSymbol(IArrayTypeSymbol sourceSymbol, BindContext context)
            :base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            ElementType = context.GetTypeSymbol(((IArrayTypeSymbol)RoslynSymbol).ElementType);
        }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            return ImmutableArray.Create<Symbol>(ElementType);
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, BindContext context)
        {
            throw new System.InvalidOperationException("Array symbols should not be creating new symbols");
        }
    }
}
