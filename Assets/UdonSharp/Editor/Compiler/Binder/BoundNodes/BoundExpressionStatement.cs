
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundExpressionStatement : BoundStatement
    {
        public BoundExpression Expression { get; }

        public BoundExpressionStatement(SyntaxNode node, BoundExpression expression)
            : base(node)
        {
            Expression = expression;
        }

        public override void Emit(EmitContext context)
        {
            context.Emit(Expression);
        }
    }
}
