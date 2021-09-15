
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

        public virtual void MarkFieldCallback(FieldSymbol symbol) => throw new NotImplementedException();

        protected PropertySymbol(IPropertySymbol sourceSymbol, AbstractPhaseContext context)
            :base(sourceSymbol, context)
        {
            if (sourceSymbol == null) return;
            
            Type = context.GetTypeSymbol(sourceSymbol.Type);
            ContainingType = context.GetTypeSymbol(sourceSymbol.ContainingType);

            Parameters = sourceSymbol.Parameters.Select(p => (ParameterSymbol) context.GetSymbol(p))
                .ToImmutableArray();

            if (sourceSymbol.GetMethod != null)
                GetMethod = (MethodSymbol) context.GetSymbol(sourceSymbol.GetMethod);
            if (sourceSymbol.SetMethod != null)
                SetMethod = (MethodSymbol) context.GetSymbol(sourceSymbol.SetMethod);
        }

        public new IPropertySymbol RoslynSymbol => (IPropertySymbol)base.RoslynSymbol;

        public override void Bind(BindContext context)
        {
            DeclarationSyntax = (RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax() as PropertyDeclarationSyntax);
            
            if (GetMethod != null)
                GetMethod.Bind(context);
            
            if (SetMethod != null)
                SetMethod.Bind(context);
            
            SetupAttributes(context);
        }
    }
}
