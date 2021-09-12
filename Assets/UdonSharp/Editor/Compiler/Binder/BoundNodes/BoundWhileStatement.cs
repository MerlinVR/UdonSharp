
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundWhileStatement : BoundStatement
    {
        private BoundExpression Condition { get; }
        private BoundStatement Body { get; }
        
        public BoundWhileStatement(SyntaxNode node, BoundExpression condition, BoundStatement body)
            :base(node)
        {
            Condition = condition;
            Body = body;
        }

        public override void Emit(EmitContext context)
        {
            JumpLabel continueLabel = context.PushContinueLabel();
            JumpLabel breakLabel = context.PushBreakLabel();
            
            context.Module.LabelJump(continueLabel);

            context.Module.AddJumpIfFalse(breakLabel, context.EmitValue(Condition));

            context.Emit(Body);

            context.Module.AddJump(continueLabel);

            context.Module.LabelJump(breakLabel);
            
            context.PopBreakLabel();
            context.PopContinueLabel();
        }
    }
}
