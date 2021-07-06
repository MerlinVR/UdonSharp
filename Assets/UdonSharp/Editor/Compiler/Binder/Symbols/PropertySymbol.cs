using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class PropertySymbol : Symbol
    {
        public TypeSymbol Type { get; private set; }
        public PropertyDeclarationSyntax DeclarationSyntax { get; private set; }

        public PropertySymbol(IPropertySymbol sourceSymbol, BindContext context)
            :base(sourceSymbol, context)
        {
            ContainingSymbol = context.GetTypeSymbol(sourceSymbol.ContainingType);
        }

        public new IPropertySymbol RoslynSymbol { get { return (IPropertySymbol)base.RoslynSymbol; } }

        public override void Bind(BindContext context)
        {
            Type = context.GetTypeSymbol(RoslynSymbol.Type);

            DeclarationSyntax = (RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax() as PropertyDeclarationSyntax);
            //Debug.Log(DeclarationSyntax);
        }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }
    }
}
