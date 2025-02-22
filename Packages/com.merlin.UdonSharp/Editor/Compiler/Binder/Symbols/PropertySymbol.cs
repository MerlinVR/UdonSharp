
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal abstract class PropertySymbol : Symbol
    {
        public TypeSymbol Type { get; protected set; }
        public PropertyDeclarationSyntax DeclarationSyntax { get; private set; }
        
        public ImmutableArray<ParameterSymbol> Parameters { get; }
        
        public MethodSymbol GetMethod { get; protected set; }
        public MethodSymbol SetMethod { get; protected set; }
        
        public BoundExpression InitializerExpression { get; protected set; }
        protected ExpressionSyntax InitializerSyntax { get; set; }
        
        public FieldSymbol BackingField { get; protected set; }

        private bool _isBound;
        public override bool IsBound => _isBound;

        public virtual void MarkFieldCallback(FieldSymbol symbol) => throw new NotImplementedException();

        protected PropertySymbol(IPropertySymbol sourceSymbol, AbstractPhaseContext context)
            :base(sourceSymbol, context)
        {
            if (sourceSymbol == null) return;
            
            Type = context.GetTypeSymbolWithoutRedirect(sourceSymbol.Type);
            ContainingType = context.GetTypeSymbolWithoutRedirect(sourceSymbol.ContainingType);

            Parameters = sourceSymbol.Parameters.Select(p => (ParameterSymbol) context.GetSymbolNoRedirect(p)).ToImmutableArray();

            if (sourceSymbol.GetMethod != null)
                GetMethod = (MethodSymbol) context.GetSymbolNoRedirect(sourceSymbol.GetMethod);
            if (sourceSymbol.SetMethod != null)
                SetMethod = (MethodSymbol) context.GetSymbolNoRedirect(sourceSymbol.SetMethod);
        }

        public new IPropertySymbol RoslynSymbol => (IPropertySymbol)base.RoslynSymbol;

        public override void Bind(BindContext context)
        {
            if (IsBound)
                return;
            
            _isBound = true;
            
            DeclarationSyntax = (RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax() as PropertyDeclarationSyntax);
            
            SetupAttributes(context);
            
            BackingField = GetBackingField(context);
        }

        protected FieldSymbol GetBackingField(BindContext context)
        {
            foreach (FieldSymbol field in ContainingType.GetMembers<FieldSymbol>(context))
            {
                if (field.RoslynSymbol.AssociatedSymbol != null &&
                    field.RoslynSymbol.AssociatedSymbol.Equals(RoslynSymbol))
                    return field;
            }
            
            return null;
        }
    }
}
