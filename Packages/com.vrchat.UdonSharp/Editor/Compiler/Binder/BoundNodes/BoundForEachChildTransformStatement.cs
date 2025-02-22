
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundForEachChildTransformStatement : BoundStatement
    {
        private BoundExpression IteratorSource { get; }
        private Symbol ValueSymbol { get; }
        private BoundStatement BodyStatement { get; }
        
        public BoundForEachChildTransformStatement(SyntaxNode node, BoundExpression iteratorSource, Symbol valueSymbol, BoundStatement bodyStatement)
            :base(node)
        {
            IteratorSource = iteratorSource;
            ValueSymbol = valueSymbol;
            BodyStatement = bodyStatement;
        }

        public override void Emit(EmitContext context)
        {
            var blockScope = context.OpenBlockScope();

            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);
            TypeSymbol transformType = context.GetTypeSymbol(typeof(Transform));

            MethodSymbol getChildMethod = transformType.GetMember<MethodSymbol>("GetChild", context);
            PropertySymbol lengthProperty = transformType.GetMember<PropertySymbol>("childCount", context);
            
            Value iteratorValue = context.EmitValue(IteratorSource);
            iteratorValue.MarkUsedRecursively();
            var iteratorAccess = BoundAccessExpression.BindAccess(iteratorValue);

            Value arraySize = context.CreateInternalValue(intType);
            arraySize.MarkUsedRecursively();
            
            BoundAccessExpression getLength = BoundAccessExpression.BindAccess(context, SyntaxNode, lengthProperty, iteratorAccess);
            context.EmitValueAssignment(arraySize, getLength);

            // Declare and reset incrementor value
            Value incrementorValue = context.CreateInternalValue(intType);
            incrementorValue.MarkUsedRecursively();
            context.EmitValueAssignment(incrementorValue, BoundAccessExpression.BindAccess(context.GetConstantValue(intType, 0)));

            JumpLabel loopLabel = context.Module.CreateLabel();
            context.Module.LabelJump(loopLabel);

            var incrementorAccess = BoundAccessExpression.BindAccess(incrementorValue);

            BoundExpression increment = new BoundInvocationExpression.BoundPrefixOperatorExpression(context, SyntaxNode,
                incrementorAccess, new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Addition, intType, context));

            var lengthCheck = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode,
                new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.LessThan, intType, context), null,
                new BoundExpression[]
                {
                    incrementorAccess,
                    BoundAccessExpression.BindAccess(arraySize)
                });
            
            JumpLabel exitLoopLabel = context.PushBreakLabel();
            JumpLabel continueLabel = context.PushContinueLabel();
            
            Value lengthCheckResult = context.EmitValue(lengthCheck);
            context.Module.AddJumpIfFalse(exitLoopLabel, lengthCheckResult);

            context.EmitValueAssignment(context.GetUserValue(ValueSymbol),
                BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, getChildMethod, iteratorAccess,
                    new BoundExpression[] {incrementorAccess}));
            
            context.Emit(BodyStatement);
            
            context.Module.LabelJump(continueLabel);
            
            context.Emit(increment);
            
            context.Module.AddJump(loopLabel);
            
            context.Module.LabelJump(exitLoopLabel);
            
            context.PopBreakLabel();
            context.PopContinueLabel();
            
            blockScope.Dispose();
        }
    }
}
