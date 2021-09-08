
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundStaticUserMethodInvocation : BoundUserMethodInvocationExpression
    {
        public BoundStaticUserMethodInvocation(SyntaxNode node, MethodSymbol method, BoundExpression[] parameterExpressions)
            :base(node, method, null, parameterExpressions)
        {
        }
    }
}
