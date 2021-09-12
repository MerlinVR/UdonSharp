
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundForStatement : BoundStatement
    {
        private BoundVariableDeclarationStatement Declaration { get; }
        private BoundExpression Condition { get; }
        private BoundExpression[] Incrementors { get; }
        private BoundStatement Body { get; }
        
        public BoundForStatement(SyntaxNode node, BoundVariableDeclarationStatement declaration, BoundExpression condition, BoundExpression[] incrementors, BoundStatement body)
            :base(node)
        {
            Declaration = declaration;
            Condition = condition;
            Incrementors = incrementors;
            Body = body;
        }

        public override void Emit(EmitContext context)
        {
            using (context.OpenBlockScope())
            {
                if (Declaration != null)
                    context.Emit(Declaration);

                JumpLabel continueLabel = context.PushContinueLabel();
                JumpLabel breakLabel = context.PushBreakLabel();
                JumpLabel loopLabel = context.Module.CreateLabel();
                
                context.Module.LabelJump(loopLabel);

                if (Condition != null)
                    context.Module.AddJumpIfFalse(breakLabel, context.EmitValue(Condition));

                context.Emit(Body);

                context.Module.LabelJump(continueLabel);

                foreach (var incrementor in Incrementors)
                    context.Emit(incrementor);

                context.Module.AddJump(loopLabel);

                context.Module.LabelJump(breakLabel);
                
                context.PopBreakLabel();
                context.PopContinueLabel();
            }
        }
    }
}
