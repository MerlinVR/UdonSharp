using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundContinueStatement : BoundStatement
    {
        public BoundContinueStatement(SyntaxNode node)
            :base(node)
        {
        }

        public override void Emit(EmitContext context)
        {
            context.Module.AddJump(context.TopContinueLabel);
        }
    }
}
