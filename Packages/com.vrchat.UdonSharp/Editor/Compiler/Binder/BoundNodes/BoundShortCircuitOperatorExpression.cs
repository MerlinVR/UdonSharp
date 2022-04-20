
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundShortCircuitOperatorExpression : BoundExpression
    {
        private BoundExpression Lhs { get; }
        private BoundExpression Rhs { get; }

        private BuiltinOperatorType OperatorType { get; }

        public override TypeSymbol ValueType { get; }

        public BoundShortCircuitOperatorExpression(BinaryExpressionSyntax node, BuiltinOperatorType operatorType, BoundExpression lhs, BoundExpression rhs, AbstractPhaseContext context)
            : base(node)
        {
            ValueType = context.GetTypeSymbol(SpecialType.System_Boolean);
            Lhs = lhs;
            Rhs = rhs;
            OperatorType = operatorType;
        }

        public override Value EmitValue(EmitContext context)
        {
            // We don't want any references outside the flow control to be dirtied conditionally
            context.TopTable.DirtyAllValues();
            
            Value resultValue = context.CreateInternalValue(ValueType);

            if (OperatorType == BuiltinOperatorType.LogicalAnd)
            {
                JumpLabel failLabel = context.Module.CreateLabel();

                context.EmitValueAssignment(resultValue, Lhs);
                
                context.Module.AddJumpIfFalse(failLabel, resultValue);

                context.EmitValueAssignment(resultValue, Rhs);
                
                context.Module.LabelJump(failLabel);
            }
            else if (OperatorType == BuiltinOperatorType.LogicalOr)
            {
                JumpLabel failLabel = context.Module.CreateLabel();
                JumpLabel exitLabel = context.Module.CreateLabel();

                context.EmitValueAssignment(resultValue, Lhs);
                
                context.Module.AddJumpIfFalse(failLabel, resultValue);
                context.Module.AddJump(exitLabel);
                
                context.Module.LabelJump(failLabel);

                context.EmitValueAssignment(resultValue, Rhs);
                
                context.Module.LabelJump(exitLabel);
            }
            else
            {
                throw new InvalidOperationException("Invalid operator type");
            }
            
            return resultValue;
        }
    }
}
