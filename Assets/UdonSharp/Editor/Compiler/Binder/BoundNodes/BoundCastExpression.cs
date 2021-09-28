
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundCastExpression : BoundExpression
    {
        private TypeSymbol TargetType { get; }
        
        private bool IsExplicit { get; }

        public override TypeSymbol ValueType => TargetType;

        public BoundCastExpression(SyntaxNode node, BoundExpression sourceExpression, TypeSymbol targetType, bool isExplicit)
            : base(node, sourceExpression)
        {
            if (targetType is TypeParameterSymbol)
                throw new InvalidOperationException("Cannot cast to generic parameter types");
            
            TargetType = targetType;
            IsExplicit = isExplicit;
        }

        public override Value EmitValue(EmitContext context)
        {
            return context.CastValue(context.EmitValue(SourceExpression), TargetType, IsExplicit);
        }
    }
}
