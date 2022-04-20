
using System;
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
using UnityEngine;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

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

                if (!IsGenericMethod && RoslynSymbol != RoslynSymbol.OriginalDefinition)
                    TypeArguments = sourceSymbol.TypeArguments.Length > 0 ? sourceSymbol.TypeArguments.Select(context.GetTypeSymbol).ToImmutableArray() : ImmutableArray<TypeSymbol>.Empty;
                else
                    TypeArguments = sourceSymbol.TypeArguments.Length > 0 ? sourceSymbol.TypeArguments.Select(context.GetTypeSymbolWithoutRedirect).ToImmutableArray() : ImmutableArray<TypeSymbol>.Empty;

                if (RoslynSymbol.IsOverride && RoslynSymbol.OverriddenMethod != null) // abstract methods can be overrides, but not have overriden methods
                    OverridenMethod = (MethodSymbol) context.GetSymbol(RoslynSymbol.OverriddenMethod);

                IsOperator = RoslynSymbol.MethodKind == MethodKind.BuiltinOperator ||
                             RoslynSymbol.MethodKind == MethodKind.UserDefinedOperator;

                if (RoslynSymbol.OriginalDefinition != RoslynSymbol)
                    OriginalSymbol = context.GetSymbolNoRedirect(RoslynSymbol.OriginalDefinition);
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

        public override bool IsBound => MethodBody != null || RoslynSymbol.IsAbstract || IsUntypedGenericMethod;

        public bool IsUntypedGenericMethod
        {
            get
            {
                return (RoslynSymbol.IsGenericMethod && RoslynSymbol.OriginalDefinition == RoslynSymbol) || 
                        TypeArguments.Any(e => e is TypeParameterSymbol) ||
                        ContainingType.TypeArguments.Any(e => e is TypeParameterSymbol);
            }
        }

        public bool HasOverrides => _overrides != null && _overrides.Count > 0;
        public bool IsGenericMethod => RoslynSymbol?.IsGenericMethod ?? false;

        public MethodSymbol ConstructGenericMethod(AbstractPhaseContext context, TypeSymbol[] typeArguments)
        {
            return (MethodSymbol)context.GetSymbol(RoslynSymbol.OriginalDefinition.Construct(typeArguments.Select(e => e.RoslynSymbol).ToArray()));
        }

        private void CheckHiddenMethods(BindContext context)
        {
            if (OverridenMethod != null || ContainingType.BaseType.IsExtern)
                return;

            List<MethodSymbol> foundNamedMethods = new List<MethodSymbol>();

            TypeSymbol currentType = ContainingType.BaseType;

            while (!currentType.IsExtern)
            {
                foundNamedMethods.AddRange(currentType.GetMembers<MethodSymbol>(Name, context));
                currentType = currentType.BaseType;
            }

            foreach (MethodSymbol foundMethod in foundNamedMethods)
            {
                if (foundMethod.Parameters.Length == Parameters.Length && foundMethod.Parameters.Select(e => e.Type).SequenceEqual(Parameters.Select(e => e.Type))) 
                    throw new CompilerException($"U# does not yet support hiding base methods, did you intend to override '{RoslynSymbol.Name}'?");
            }
        }
        
        public override void Bind(BindContext context)
        {
            if (IsBound)
                return;

            if (!IsUntypedGenericMethod)
            {
                foreach (ParameterSymbol param in Parameters)
                    param.Bind(context);
            }

            if (RoslynSymbol.IsAbstract)
                return;

            IMethodSymbol methodSymbol = RoslynSymbol;

            if (methodSymbol.DeclaringSyntaxReferences.IsEmpty)
                throw new CompilerException($"Could not find syntax reference for {methodSymbol}");
            
            SyntaxNode declaringSyntax = methodSymbol.DeclaringSyntaxReferences.First().GetSyntax();
            context.CurrentNode = declaringSyntax;
            
            CheckHiddenMethods(context);
            
            if (OverridenMethod != null)
                OverridenMethod.AddOverride(this);

            if (methodSymbol.MethodKind != MethodKind.PropertyGet && methodSymbol.MethodKind != MethodKind.PropertySet &&
                ((MethodDeclarationSyntax) methodSymbol.DeclaringSyntaxReferences.First().GetSyntax()).Modifiers.Any(SyntaxKind.PartialKeyword))
                throw new NotSupportedException(LocStr.CE_PartialMethodsNotSupported, methodSymbol.DeclaringSyntaxReferences.FirstOrDefault());

            BinderSyntaxVisitor bodyVisitor = new BinderSyntaxVisitor(this, context);

            if (declaringSyntax is MethodDeclarationSyntax methodSyntax)
            {
                if (methodSyntax.Body != null)
                    MethodBody = bodyVisitor.Visit(methodSyntax.Body);
                else if (methodSyntax.ExpressionBody != null)
                    MethodBody = bodyVisitor.VisitExpression(methodSyntax.ExpressionBody, ReturnType);
                else
                    throw new CompilerException("No method body or expression body found", methodSyntax.GetLocation());
            }
            else if (declaringSyntax is AccessorDeclarationSyntax propertySyntax)
            {
                if (propertySyntax.Body != null)
                    MethodBody = bodyVisitor.Visit(propertySyntax.Body);
                else if (propertySyntax.ExpressionBody != null)
                    MethodBody = bodyVisitor.VisitExpression(propertySyntax.ExpressionBody, ReturnType);
                else if (methodSymbol.MethodKind == MethodKind.PropertySet)
                    MethodBody = GeneratePropertyAutoSetter(context, propertySyntax);
                else if (methodSymbol.MethodKind == MethodKind.PropertyGet)
                    MethodBody = GeneratePropertyAutoGetter(context, propertySyntax);
                else
                    throw new CompilerException("No method body or expression body found", propertySyntax.GetLocation());
            }
            else if (declaringSyntax is ArrowExpressionClauseSyntax arrowExpression)
            {
                MethodBody = bodyVisitor.VisitExpression(arrowExpression.Expression, ReturnType);
            }
            else
            {
                throw new Exception($"Declaring syntax {declaringSyntax.Kind()} was not a method or property");
            }

            SetupAttributes(context);
        }

        private BoundNode GeneratePropertyAutoSetter(BindContext context, SyntaxNode node)
        {
            FieldSymbol autoField = GetAutoField(context);
            
            if (autoField == null)
                return null;

            return new BoundAssignmentExpression(node, BoundAccessExpression.BindAccess(context, node, autoField, null),
                BoundAccessExpression.BindAccess(context, node, Parameters[0], null));
        }
        
        private BoundNode GeneratePropertyAutoGetter(BindContext context, SyntaxNode node)
        {
            FieldSymbol autoField = GetAutoField(context);

            if (autoField == null)
                return null;

            return new BoundReturnStatement(node, BoundAccessExpression.BindAccess(context, node, autoField, null));
        }

        private FieldSymbol GetAutoField(BindContext context)
        {
            PropertySymbol containingProperty = (PropertySymbol)context.GetSymbol(RoslynSymbol.AssociatedSymbol);
            foreach (var field in ContainingType.GetMembers<FieldSymbol>(context))
            {
                if (field.RoslynSymbol.AssociatedSymbol != null &&
                    field.RoslynSymbol.AssociatedSymbol.Equals(containingProperty.RoslynSymbol))
                    return field;
            }

            return null;
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
