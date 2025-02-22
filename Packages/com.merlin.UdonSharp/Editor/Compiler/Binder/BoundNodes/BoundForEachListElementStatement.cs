
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundForEachListElementStatement : BoundStatement
    {
        private BoundExpression IteratorSource { get; }
        private Symbol ValueSymbol { get; }
        private BoundStatement BodyStatement { get; }
        private PropertySymbol LengthProperty { get; }
        private PropertySymbol IndexerProperty { get; }
        
        public BoundForEachListElementStatement(AbstractPhaseContext context, SyntaxNode node, BoundExpression iteratorSource, Symbol valueSymbol, BoundStatement bodyStatement)
            :base(node)
        {
            IteratorSource = iteratorSource;
            ValueSymbol = valueSymbol;
            BodyStatement = bodyStatement;
            
            LengthProperty = IteratorSource.ValueType.GetMember<PropertySymbol>("Count", context);
            IndexerProperty = IteratorSource.ValueType.GetMember<PropertySymbol>("this[]", context);
            
            // Just bind these to run the redirect logic and mark stuff referenced
            BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, LengthProperty.GetMethod, IteratorSource, Array.Empty<BoundExpression>());
            BoundAccessExpression.BindElementAccess(context, SyntaxNode, IndexerProperty, IteratorSource, new BoundExpression[] {null});
        }

        public override void Emit(EmitContext context)
        {
            IDisposable blockScope = context.OpenBlockScope();

            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);
            Value iteratorValue = context.EmitValue(IteratorSource);
            iteratorValue.MarkUsedRecursively();
            BoundAccessExpression iteratorAccess = BoundAccessExpression.BindAccess(iteratorValue);

            Value arraySize = context.CreateInternalValue(intType);
            arraySize.MarkUsedRecursively();
            
            BoundAccessExpression getLength = BoundAccessExpression.BindAccess(context, SyntaxNode, LengthProperty, iteratorAccess);
            context.EmitValueAssignment(arraySize, getLength);

            // Declare and reset incrementor value
            Value incrementorValue = context.CreateInternalValue(intType);
            incrementorValue.MarkUsedRecursively();
            context.EmitValueAssignment(incrementorValue, BoundAccessExpression.BindAccess(context.GetConstantValue(intType, 0)));

            JumpLabel loopLabel = context.Module.CreateLabel();
            context.Module.LabelJump(loopLabel);

            BoundAccessExpression incrementorAccess = BoundAccessExpression.BindAccess(incrementorValue);

            BoundExpression increment = new BoundInvocationExpression.BoundPrefixOperatorExpression(context, SyntaxNode,
                incrementorAccess, new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Addition, intType, context));

            BoundExpression lengthCheck = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode,
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
            
            BoundAccessExpression indexerAccess = BoundAccessExpression.BindElementAccess(context, SyntaxNode, IndexerProperty,
                iteratorAccess, new BoundExpression[] {incrementorAccess});
            
            context.EmitValueAssignment(context.GetUserValue(ValueSymbol), indexerAccess);
            
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
