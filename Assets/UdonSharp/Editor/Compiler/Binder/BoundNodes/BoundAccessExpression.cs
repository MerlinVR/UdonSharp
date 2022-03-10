
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundAccessExpression : BoundExpression
    {
        protected BoundAccessExpression(SyntaxNode node, BoundExpression sourceExpression)
            : base(node, sourceExpression)
        {
        }
        
        public virtual void MarkForcedBaseCall() {}

        /// <summary>
        /// Emits a set operation on this access
        /// </summary>
        /// <param name="context"></param>
        /// <param name="valueExpression"></param>
        /// <returns>Returns the evaluated value of the valueExpression. This is primarily to handle chained assignment expressions correctly.</returns>
        public abstract Value EmitSet(EmitContext context, BoundExpression valueExpression);

        public static BoundAccessExpression BindAccess(AbstractPhaseContext context, SyntaxNode node, Symbol accessSymbol, BoundExpression symbolExpressionSource)
        {
            if (node.Kind() == SyntaxKind.ThisExpression)
                return new BoundThisAccessExpression(node, ((ParameterSymbol)accessSymbol).Type, symbolExpressionSource);
            
            switch (accessSymbol)
            {
                case LocalSymbol localSymbol:
                    return BoundLocalAccessExpression.BindLocalAccess(node, localSymbol);
                case ParameterSymbol parameterSymbol:
                    return new BoundParameterAccessExpression(node, parameterSymbol);
                case FieldSymbol fieldSymbol:
                    return BoundFieldAccessExpression.BindFieldAccess(node, fieldSymbol, symbolExpressionSource);
                case PropertySymbol propertySymbol:
                    return BoundPropertyAccessExpression.BindPropertyAccess(context, node, propertySymbol, symbolExpressionSource);
            }

            throw new System.NotImplementedException();
        }

        public static BoundAccessExpression BindThisAccess(TypeSymbol typeSymbol)
        {
            return new BoundThisAccessExpression(null, typeSymbol, null);
        }

        public static BoundAccessExpression BindAccess(Value value)
        {
            return new BoundValueAccessExpression(value);
        }

        public static BoundAccessExpression BindAccess(Value.CowValue value)
        {
            return new BoundCowValueAccessExpression(value);
        }

        public static BoundAccessExpression BindElementAccess(AbstractPhaseContext context, SyntaxNode node,
            BoundExpression symbolExpressionSource, BoundExpression[] indexers)
        {
            return new BoundArrayAccessExpression(node, context, symbolExpressionSource, indexers);
        }
        
        public static BoundAccessExpression BindElementAccess(AbstractPhaseContext context, SyntaxNode node, PropertySymbol accessSymbol,
            BoundExpression symbolExpressionSource, BoundExpression[] indexers)
        {
            return BoundPropertyAccessExpression.BindPropertyAccess(context, node, accessSymbol,
                symbolExpressionSource, indexers);
        }

        private sealed class BoundLocalAccessExpression : BoundAccessExpression
        {
            private LocalSymbol AccessSymbol { get; }

            public override TypeSymbol ValueType => AccessSymbol.Type;

            public BoundLocalAccessExpression(SyntaxNode node, LocalSymbol accessSymbol) 
                : base(node, null)
            {
                AccessSymbol = accessSymbol;
            }

            public static BoundAccessExpression BindLocalAccess(SyntaxNode node, LocalSymbol localSymbol)
            {
                if (localSymbol.IsConst)
                    return new BoundConstantExpression(localSymbol.ConstantValue, localSymbol.Type);

                return new BoundLocalAccessExpression(node, localSymbol);
            }

            public override Value EmitValue(EmitContext context)
            {
                if (AccessSymbol.IsConst)
                    return context.GetConstantValue(ValueType, AccessSymbol.ConstantValue);

                return context.GetUserValue(AccessSymbol);
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                return context.EmitValueAssignment(context.GetUserValue(AccessSymbol), valueExpression);
            }
        }

        private sealed class BoundParameterAccessExpression : BoundAccessExpression
        {
            private ParameterSymbol AccessSymbol { get; }

            public override TypeSymbol ValueType => AccessSymbol.Type;

            public BoundParameterAccessExpression(SyntaxNode node, ParameterSymbol accessSymbol) 
                : base(node, null)
            {
                AccessSymbol = accessSymbol;
            }

            private Value GetParamValue(EmitContext context)
            {
                MethodSymbol containingMethod = AccessSymbol.ContainingSymbol;
                EmitContext.MethodLinkage linkage = context.GetMethodLinkage(containingMethod, false);

                int paramIdx = -1;
                for (int i = 0; i < containingMethod.Parameters.Length; ++i)
                {
                    if (containingMethod.Parameters[i] == AccessSymbol)
                    {
                        paramIdx = i;
                        break;
                    }
                }

                return linkage.ParameterValues[paramIdx];
            }
            
            public override Value EmitValue(EmitContext context)
            {
                return GetParamValue(context);
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                return context.EmitValueAssignment(GetParamValue(context), valueExpression);
            }
        }

        private sealed class BoundThisAccessExpression : BoundAccessExpression
        {
            public BoundThisAccessExpression(SyntaxNode node, TypeSymbol thisType, BoundExpression sourceExpression) : base(node, sourceExpression)
            {
                ValueType = thisType;
                IsThis = true;
            }

            public override TypeSymbol ValueType { get; }

            public override Value EmitValue(EmitContext context)
            {
                if (!((INamedTypeSymbol) ValueType.RoslynSymbol).IsUdonSharpBehaviour())
                    throw new NotImplementedException("Non udonsharp behaviour `this` is not yet implemented");

                return context.GetUdonThisValue(ValueType);
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                throw new NotSupportedException("Cannot set `this` reference", SyntaxNode.GetLocation());
            }
        }
        
        private sealed class BoundValueAccessExpression : BoundAccessExpression
        {
            private Value AccessValue { get; }
            
            public override TypeSymbol ValueType => AccessValue.UserType;

            public BoundValueAccessExpression(Value value) 
                : base(null, null)
            {
                AccessValue = value;
            }

            public override Value EmitValue(EmitContext context)
            {
                return AccessValue;
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                return context.EmitValueAssignment(AccessValue, valueExpression);
            }
        }
        
        private sealed class BoundCowValueAccessExpression : BoundAccessExpression
        {
            private Value.CowValue AccessValue { get; }
            
            public override TypeSymbol ValueType { get; }

            public BoundCowValueAccessExpression(Value.CowValue value) 
                : base(null, null)
            {
                AccessValue = value;
                ValueType = value.Value.UserType;
            }

            public override Value EmitValue(EmitContext context)
            {
                return AccessValue.Value;
            }

            public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
            {
                return context.EmitValueAssignment(AccessValue.Value, valueExpression);
            }
        }
    }
}
