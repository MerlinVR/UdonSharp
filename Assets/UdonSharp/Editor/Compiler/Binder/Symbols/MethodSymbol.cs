using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;
using UdonSharp.Core;
using UdonSharp.Localization;

namespace UdonSharp.Compiler.Symbols
{
    internal abstract class MethodSymbol : Symbol
    {
        public MethodSymbol(IMethodSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {
        }

        public bool IsConstructor { get; private set; }
        public TypeSymbol ReturnType { get; private set; }
        public ImmutableArray<ParameterSymbol> Parameters { get; private set; }

        public new IMethodSymbol RoslynSymbol { get { return (IMethodSymbol)base.RoslynSymbol; } }

        public override void Bind(BindContext context)
        {
            IMethodSymbol methodSymbol = RoslynSymbol;

            if ((methodSymbol.DeclaringSyntaxReferences.First().GetSyntax() as MethodDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword))
                throw new NotSupportedException(LocStr.CE_PartialMethodsNotSupported, methodSymbol.DeclaringSyntaxReferences.FirstOrDefault());

            ITypeSymbol returnType = methodSymbol.ReturnType;

            if (returnType != null)
                ReturnType = context.GetTypeSymbol(returnType);

            if (methodSymbol.Parameters != null)
            {
                List<ParameterSymbol> parameterSymbols = new List<ParameterSymbol>();

                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                {
                    ParameterSymbol newSymbol = new ParameterSymbol(parameterSymbol, context);
                    newSymbol.Bind(context);

                    parameterSymbols.Add(newSymbol);
                }

                Parameters = ImmutableArray.CreateRange<ParameterSymbol>(parameterSymbols);
            }

            BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);

            MethodDeclarationSyntax methodSyntax = methodSymbol.DeclaringSyntaxReferences.First().GetSyntax() as MethodDeclarationSyntax;


        }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }
    }
}
