
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundConditionalExpression : BoundExpression
    {
        private BoundExpression ConditionExpression { get; }
        private BoundExpression TrueExpression { get; }
        private BoundExpression FalseExpression { get; }

        public override TypeSymbol ValueType { get; }

        public BoundConditionalExpression(ConditionalExpressionSyntax node, TypeSymbol resultType, BoundExpression conditionExpression, BoundExpression trueExpression, BoundExpression falseExpression)
            : base(node)
        {
            ConditionExpression = conditionExpression;
            TrueExpression = trueExpression;
            FalseExpression = falseExpression;
            ValueType = resultType;
        }

        public override Value EmitValue(EmitContext context)
        {
            var assignmentInterrupt = context.InterruptAssignmentScope();
            Value conditionValue = context.EmitValue(ConditionExpression);
            assignmentInterrupt.Dispose();

            JumpLabel conditionJump = context.Module.CreateLabel();
            JumpLabel exitTrueJump = context.Module.CreateLabel();

            Value returnValue = context.GetReturnValue(ValueType);
            
            // We don't want any references outside the flow control to be dirtied conditionally
            context.TopTable.DirtyAllValues();
            
            context.Module.AddJumpIfFalse(conditionJump, conditionValue);

            context.EmitValueAssignment(returnValue, TrueExpression);
            
            context.Module.AddJump(exitTrueJump);
            context.Module.LabelJump(conditionJump);

            context.EmitValueAssignment(returnValue, FalseExpression);
            
            context.Module.LabelJump(exitTrueJump);

            return returnValue;
        }
    }
}
