
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundInvocationExpression : BoundExpression
    {
        public MethodSymbol Method { get; set; }
        public BoundExpression[] ParameterExpressions { get; set; }

        public BoundInvocationExpression(SyntaxNode node)
            :base(node)
        { }
    }
}
