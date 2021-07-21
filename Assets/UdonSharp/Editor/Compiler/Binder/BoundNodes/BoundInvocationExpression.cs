
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundInvocationExpression : BoundExpression
    {
        [PublicAPI]
        public MethodSymbol Method { get; private set; }
        
        [PublicAPI]
        public BoundExpression[] ParameterExpressions { get; private set; }

        private BoundInvocationExpression(SyntaxNode node, MethodSymbol method, BoundExpression[] parameterExpressions)
            :base(node)
        {
            Method = method;
            ParameterExpressions = parameterExpressions;
        }

        public static BoundInvocationExpression CreateBoundInvocation(SyntaxNode node, MethodSymbol symbol, BoundExpression[] parameterExpressions)
        {
            if (symbol.IsExtern)
                return new BoundExternInvocation(node, symbol, parameterExpressions);

            throw new System.NotImplementedException();
        }

        private class BoundExternInvocation : BoundInvocationExpression
        {
            public BoundExternInvocation(SyntaxNode node, MethodSymbol method, BoundExpression[] parameterExpressions) 
                : base(node, method, parameterExpressions)
            {
            }

            public override void Emit(EmitContext context)
            {
                
            }
        }
    }
}
