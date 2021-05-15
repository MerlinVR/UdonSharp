using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class FieldSymbol : Symbol
    {
        readonly TypeSymbol sourceType;

        public TypeSymbol Type { get; private set; }
        public ExpressionSyntax InitializerSyntax { get; private set; }

        public FieldSymbol(IFieldSymbol sourceSymbol, BindContext bindContext)
            :base(sourceSymbol, bindContext)
        {
        }

        public new IFieldSymbol RoslynSymbol { get { return (IFieldSymbol)base.RoslynSymbol; } }
        public override Symbol ContainingType => sourceType;

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }

        bool _resolved = false;
        public override bool IsBound => _resolved;

        public override void Bind(BindContext context)
        {
            Type = context.GetTypeSymbol(RoslynSymbol.Type);
            InitializerSyntax = (RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax() as VariableDeclaratorSyntax)?.Initializer?.Value;

            _resolved = true;
        }
    }
}
