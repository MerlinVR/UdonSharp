
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundInstanceUserMethodInvocation : BoundUserMethodInvocationExpression
    {
        public BoundInstanceUserMethodInvocation(SyntaxNode node, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions)
            :base(node, method, instanceExpression, parameterExpressions)
        {
        }
    }
}
