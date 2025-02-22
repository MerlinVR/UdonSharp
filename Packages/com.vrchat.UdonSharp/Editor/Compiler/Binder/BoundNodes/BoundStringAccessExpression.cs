
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundStringAccessExpression : BoundAccessExpression
    {
        private BoundExpression IndexerExpression { get; }
        private readonly MethodSymbol _toCharArraySymbol;
        
        public BoundStringAccessExpression(AbstractPhaseContext context, SyntaxNode node, BoundExpression sourceExpression, BoundExpression indexerExpression)
            : base(node, sourceExpression)
        {
            IndexerExpression = indexerExpression;
            ValueType = context.GetTypeSymbol(SpecialType.System_Char);

            _toCharArraySymbol = context.GetTypeSymbol(SpecialType.System_String).GetMembers<MethodSymbol>(nameof(string.ToCharArray), context).First(e => e.Parameters.Length == 2);
        }

        public override TypeSymbol ValueType { get; }

        public override Value EmitValue(EmitContext context)
        {
            Value returnValue = context.GetReturnValue(ValueType);

            var charArray = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, _toCharArraySymbol,
                BindAccess(context.EmitValue(SourceExpression)), new[]
                {
                    IndexerExpression,
                    BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 1))
                });

            context.EmitValueAssignment(returnValue,
                BindElementAccess(context, SyntaxNode, charArray,
                    new BoundExpression[]
                    {
                        BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 0))
                    }));

            return returnValue;
        }

        public override Value EmitSet(EmitContext context, BoundExpression valueExpression)
        {
            throw new InvalidOperationException("Cannot set a character on a string");
        }
    }
}
