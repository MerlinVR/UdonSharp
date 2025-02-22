
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundArrayCreationExpression : BoundExternInvocation
    {
        private class ArrayConstructorExtern : ExternMethodSymbol
        {
            public override string ExternSignature { get; }
            public override bool IsStatic => true;

            public override string Name => ExternSignature;

            public ArrayConstructorExtern(AbstractPhaseContext context, TypeSymbol arrayType) 
                : base(null, context)
            {
                ExternSignature = $"{arrayType.UdonType.ExternSignature}.__ctor__SystemInt32__{arrayType.UdonType.ExternSignature}";
                ReturnType = arrayType;
            }
        }
        
        private TypeSymbol ArrayType { get; }
        
        private BoundExpression[] Initializers { get; }

        public BoundArrayCreationExpression(SyntaxNode node, AbstractPhaseContext context, TypeSymbol arrayType, BoundExpression[] rankSizes, BoundExpression[] initializers)
            : base(node, context, new ArrayConstructorExtern(context, arrayType), null, rankSizes)
        {
            ArrayType = arrayType;
            Initializers = initializers;
        }

        public override TypeSymbol ValueType => ArrayType;

        public override Value EmitValue(EmitContext context)
        {
            Value returnValue = base.EmitValue(context);

            if (Initializers != null)
            {
                BoundAccessExpression arrayAccess = BoundAccessExpression.BindAccess(returnValue);
                TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);

                using (context.InterruptAssignmentScope())
                {
                    // This is quite wasteful for allocations in the compile, todo: look at caching these safely
                    for (int i = 0; i < Initializers.Length; ++i)
                    {
                        BoundAccessExpression elementAccess = BoundAccessExpression.BindElementAccess(context,
                            SyntaxNode, arrayAccess,
                            new BoundExpression[]
                                {new BoundConstantExpression(new ConstantValue<int>(i), intType, SyntaxNode)});
                        context.EmitSet(elementAccess, Initializers[i]);
                    }
                }
            }

            return returnValue;
        }
    }
}
