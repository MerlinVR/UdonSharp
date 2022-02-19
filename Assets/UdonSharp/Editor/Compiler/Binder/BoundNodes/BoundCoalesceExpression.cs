
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundCoalesceExpression : BoundExpression
    {
        private BoundExpression Lhs { get; }
        private BoundExpression Rhs { get; }
        
        public BoundCoalesceExpression(SyntaxNode node, BoundExpression lhs, BoundExpression rhs)
            : base(node, null)
        {
            Lhs = lhs;
            Rhs = rhs;
        }

        public override TypeSymbol ValueType => Lhs.ValueType;

        public override Value EmitValue(EmitContext context)
        {
            // We don't want any references outside the flow control to be dirtied conditionally
            context.TopTable.DirtyAllValues();
            
            Value returnValue = context.GetReturnValue(ValueType);

            context.EmitValueAssignment(returnValue, Lhs);

            TypeSymbol systemObjectType = context.GetTypeSymbol(SpecialType.System_Object);
            
            MethodSymbol objectEquality = new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Equality, systemObjectType, context);

            Value conditionCheck = context.EmitValue(BoundInvocationExpression.CreateBoundInvocation(context, null,
                objectEquality, null,
                new BoundExpression[]
                {
                    BoundAccessExpression.BindAccess(returnValue),
                    BoundAccessExpression.BindAccess(context.GetConstantValue(systemObjectType, null))
                }));

            JumpLabel notNullLabel = context.Module.CreateLabel();
            context.Module.AddJumpIfFalse(notNullLabel, conditionCheck);
            
            context.EmitValueAssignment(returnValue, Rhs);
            
            context.Module.LabelJump(notNullLabel);

            return returnValue;
        }
    }
}
