
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundConstArrayCreationExpression : BoundExpression
    {
        private TypeSymbol ArrayType { get; }
        private TypeSymbol ElementType { get; }
        private BoundExpression[] Initializers { get; }

        public BoundConstArrayCreationExpression(SyntaxNode node, TypeSymbol arrayType, BoundExpression[] initializers)
            : base(node)
        {
            ArrayType = arrayType;
            ElementType = arrayType.ElementType;
            Initializers = initializers;
        }

        public override TypeSymbol ValueType => ArrayType;

        public override Value EmitValue(EmitContext context)
        {
            Value returnValue;

            if (context.IsRecursiveMethodEmit)
            {
                using (context.InterruptAssignmentScope())
                {
                    returnValue = context.EmitValue(new BoundArrayCreationExpression(SyntaxNode, context, ArrayType,
                        new BoundExpression[]
                        {
                            BoundAccessExpression.BindAccess(
                                context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32),
                                    Initializers.Length))
                        }, null));
                }
            }
            else
            {
                returnValue = context.CreateGlobalInternalValue(ArrayType);
                returnValue.DefaultValue = Activator.CreateInstance(ArrayType.UdonType.SystemType, Initializers.Length);
            }

            BoundAccessExpression arrayAccess = BoundAccessExpression.BindAccess(returnValue);
            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);
            
            // This is quite wasteful for allocations in the compile, todo: look at caching these safely
            for (int i = 0; i < Initializers.Length; ++i)
            {
                BoundAccessExpression elementAccess = BoundAccessExpression.BindElementAccess(context, SyntaxNode, arrayAccess, new BoundExpression[]{new BoundConstantExpression(new ConstantValue<int>(i), intType, SyntaxNode)});
                context.EmitSet(elementAccess, Initializers[i]);
            }

            return returnValue;
        }
    }
}
