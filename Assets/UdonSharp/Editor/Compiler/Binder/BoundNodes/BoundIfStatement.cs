
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundIfStatement : BoundStatement
    {
        private BoundExpression ConditionExpression { get; }
        private BoundStatement BodyStatement { get; }
        private BoundStatement ElseStatement { get; }
        
        public BoundIfStatement(SyntaxNode node, BoundExpression conditionExpression, BoundStatement bodyStatement, BoundStatement elseStatement)
            :base(node)
        {
            ConditionExpression = conditionExpression;
            BodyStatement = bodyStatement;
            ElseStatement = elseStatement;
        }

        public override void Emit(EmitContext context)
        {
            Value conditionValue = context.EmitValue(ConditionExpression);

            JumpLabel exitLabel = context.Module.CreateLabel();
            JumpLabel failLabel = context.Module.CreateLabel();
            
            context.Module.AddJumpIfFalse(failLabel, conditionValue);

            if (BodyStatement != null)
                context.Emit(BodyStatement);
            
            if (ElseStatement != null)
                context.Module.AddJump(exitLabel);
            
            context.Module.LabelJump(failLabel);

            if (ElseStatement != null)
                context.Emit(ElseStatement);

            context.Module.LabelJump(exitLabel);
        }
    }
}
