
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundAssignmentExpression : BoundExpression
    {
        private BoundAccessExpression TargetExpression { get; }

        public override TypeSymbol ValueType => SourceExpression.ValueType;

        public BoundAssignmentExpression(AssignmentExpressionSyntax node, BoundAccessExpression assignmentTarget, BoundExpression assignmentSource)
            : base(node, assignmentSource)
        {
            TargetExpression = assignmentTarget;
        }

        public override Value EmitValue(EmitContext context)
        {
            return context.EmitSet(TargetExpression, SourceExpression);
        }
    }
}
