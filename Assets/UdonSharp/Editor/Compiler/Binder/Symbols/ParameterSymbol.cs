using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class ParameterSymbol : Symbol
    {
        public TypeSymbol Type { get; private set; }
        public IConstantValue DefaultValue { get; private set; }
        public bool IsParams => ((IParameterSymbol)RoslynSymbol).IsParams;
        public RefKind RefKind => ((IParameterSymbol)RoslynSymbol).RefKind;

        public ParameterSymbol(IParameterSymbol sourceSymbol, BindContext context)
            :base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            Type = context.GetTypeSymbol(((IParameterSymbol)RoslynSymbol).Type);
        }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            return ImmutableArray.Create<Symbol>(Type);
        }
    }
}
