
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundBitwiseNotExpression : BoundInvocationExpression
    {
        public override TypeSymbol ValueType => SourceExpression.ValueType;

        public BoundBitwiseNotExpression(SyntaxNode node, BoundExpression sourceExpression)
            : base(node, null, sourceExpression, new BoundExpression[] {})
        {
        }

        public override Value EmitValue(EmitContext context)
        {
            Type operandType = ValueType.UdonType.SystemType;
            
            object maxIntVal = operandType.GetField("MaxValue").GetValue(null);
            BoundAccessExpression maxVal = BoundAccessExpression.BindAccess(context.GetConstantValue(ValueType, maxIntVal));

            Value returnValue = context.GetReturnValue(ValueType);

            using (context.InterruptAssignmentScope())
            {
                BoundAccessExpression operandVal = BoundAccessExpression.BindAccess(context.EmitValue(SourceExpression));
                
                context.EmitValueAssignment(returnValue,
                    CreateBoundInvocation(context, null,
                        new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.LogicalXor, ValueType, context), null,
                        new BoundExpression[] { operandVal, maxVal }));

                if (UdonSharpUtils.IsSignedType(operandType)) // Signed types need handling for negating the sign
                {
                    Value isNegative = context.EmitValue(CreateBoundInvocation(context, null,
                        new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.LessThan, ValueType, context), null,
                        new BoundExpression[]
                        {
                            operandVal,
                            BoundAccessExpression.BindAccess(context.GetConstantValue(ValueType,
                                Convert.ChangeType(0, operandType)))
                        }));

                    JumpLabel exitLabel = context.Module.CreateLabel();
                    JumpLabel falseLabel = context.Module.CreateLabel();
                    
                    context.Module.AddJumpIfFalse(falseLabel, isNegative);

                    context.EmitValueAssignment(returnValue,
                        CreateBoundInvocation(context, null,
                            new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.LogicalAnd, ValueType, context),
                            null, new BoundExpression[] { BoundAccessExpression.BindAccess(returnValue), maxVal }));
                    
                    context.Module.AddJump(exitLabel);
                    context.Module.LabelJump(falseLabel);
                    
                    long bitOr = 0;

                    if (operandType == typeof(sbyte))
                        bitOr = 1 << 7;
                    else if (operandType == typeof(short))
                        bitOr = 1 << 15;
                    else if (operandType == typeof(int))
                        bitOr = 1 << 31;
                    else if (operandType == typeof(long))
                        bitOr = (long)1 << 63;
                    else
                        throw new Exception();

                    context.EmitValueAssignment(returnValue,
                        CreateBoundInvocation(context, null,
                            new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.LogicalOr, ValueType, context),
                            null,
                            new BoundExpression[]
                            {
                                BoundAccessExpression.BindAccess(returnValue),
                                BoundAccessExpression.BindAccess(context.GetConstantValue(ValueType,
                                    Convert.ChangeType(bitOr, operandType)))
                            }));
                    
                    context.Module.LabelJump(exitLabel);
                }
            }

            return returnValue;
        }
    }
}
