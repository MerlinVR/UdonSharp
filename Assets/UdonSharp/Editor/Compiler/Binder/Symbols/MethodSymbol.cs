
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Emit;
using UdonSharp.Core;
using UdonSharp.Localization;

namespace UdonSharp.Compiler.Symbols
{
    internal abstract class MethodSymbol : Symbol
    {
        protected MethodSymbol(IMethodSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol != null)
            {
                ContainingType = context.GetTypeSymbol(sourceSymbol.ContainingType);
                IsConstructor = sourceSymbol.MethodKind == MethodKind.Constructor;

                ITypeSymbol returnType = sourceSymbol.ReturnType;

                if (returnType != context.GetTypeSymbol(SpecialType.System_Void).RoslynSymbol)
                    ReturnType = context.GetTypeSymbol(returnType);
                else if (IsConstructor)
                    ReturnType = context.GetTypeSymbol(sourceSymbol.ContainingType);

                if (sourceSymbol.Parameters != null)
                {
                    List<ParameterSymbol> parameterSymbols = new List<ParameterSymbol>();

                    foreach (IParameterSymbol parameterSymbol in sourceSymbol.Parameters)
                    {
                        ParameterSymbol newSymbol = (ParameterSymbol) context.GetSymbol(parameterSymbol);

                        parameterSymbols.Add(newSymbol);
                    }

                    Parameters = ImmutableArray.CreateRange<ParameterSymbol>(parameterSymbols);
                }
                else
                {
                    Parameters = ImmutableArray<ParameterSymbol>.Empty;
                }

                if (sourceSymbol.TypeArguments.Length > 0)
                    TypeArguments = sourceSymbol.TypeArguments.Select(context.GetTypeSymbol).ToImmutableArray();
                else
                    TypeArguments = ImmutableArray<TypeSymbol>.Empty;

                if (RoslynSymbol.IsOverride && RoslynSymbol.OverriddenMethod != null) // abstract methods can be overrides, but not have overriden methods
                    OverridenMethod = (MethodSymbol) context.GetSymbol(RoslynSymbol.OverriddenMethod);

                IsOperator = RoslynSymbol.MethodKind == MethodKind.BuiltinOperator ||
                             RoslynSymbol.MethodKind == MethodKind.UserDefinedOperator;
            }
        }

        public bool IsConstructor { get; protected set; }
        public TypeSymbol ReturnType { get; protected set; }
        public ImmutableArray<ParameterSymbol> Parameters { get; protected set; }
        public ImmutableArray<TypeSymbol> TypeArguments { get; }

        public override bool IsStatic => base.RoslynSymbol.IsStatic;
        
        public MethodSymbol OverridenMethod { get; }

        public bool IsOperator { get; protected set; }

        public new IMethodSymbol RoslynSymbol => (IMethodSymbol)base.RoslynSymbol;
        
        public BoundNode MethodBody { get; private set; }

        public override bool IsBound => MethodBody != null;

        public bool HasOverrides => _overrides != null && _overrides.Count > 0;

        public override void Bind(BindContext context)
        {
            if (OverridenMethod != null)
                OverridenMethod.AddOverride(this);

            foreach (ParameterSymbol param in Parameters)
                param.Bind(context);
            
            IMethodSymbol methodSymbol = RoslynSymbol;

            if (((MethodDeclarationSyntax) methodSymbol.DeclaringSyntaxReferences.First().GetSyntax()).Modifiers.Any(SyntaxKind.PartialKeyword))
                throw new NotSupportedException(LocStr.CE_PartialMethodsNotSupported, methodSymbol.DeclaringSyntaxReferences.FirstOrDefault());

            BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);

            MethodDeclarationSyntax methodSyntax = methodSymbol.DeclaringSyntaxReferences.First().GetSyntax() as MethodDeclarationSyntax;

            if (methodSyntax.Body != null)
                MethodBody = bodyVisitor.Visit(methodSyntax.Body);
            else if (methodSyntax.ExpressionBody != null)
                MethodBody = bodyVisitor.VisitExpression(methodSyntax.ExpressionBody, ReturnType);
            else
                throw new CompilerException("No method body or expression body found", methodSyntax.GetLocation());
        }

        public virtual void Emit(EmitContext context)
        {
            EmitContext.MethodLinkage linkage = context.GetMethodLinkage(this, false);
            
            context.Module.AddCommentTag("");
            context.Module.AddCommentTag(RoslynSymbol.ToDisplayString().Replace("\n", "").Replace("\r", ""));
            context.Module.AddCommentTag("");
            context.Module.LabelJump(linkage.MethodLabel);
            
            using (context.OpenBlockScope())
            {
                if (MethodBody is BoundExpression bodyExpression)
                {
                    context.EmitReturn(bodyExpression);
                }
                else
                {
                    context.Emit(MethodBody);
                    context.EmitReturn();
                }

                context.FlattenTableCounters();
            }
        }

        private static readonly object _overrideSetLock = new object();
        private HashSet<MethodSymbol> _overrides;

        private void AddOverride(MethodSymbol overrider)
        {
            lock (_overrideSetLock)
            {
                if (_overrides == null)
                    _overrides = new HashSet<MethodSymbol>();

                _overrides.Add(overrider);
            }
        }
    }
}
