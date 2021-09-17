
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundForEachCharStatement : BoundStatement
    {
        private BoundExpression IteratorSource { get; }
        private Symbol ValueSymbol { get; }
        private BoundStatement BodyStatement { get; }
        
        public BoundForEachCharStatement(SyntaxNode node, BoundExpression iteratorSource, Symbol valueSymbol, BoundStatement bodyStatement)
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

            MethodSymbol toCharArrayMethod = context.GetTypeSymbol(SpecialType.System_String).GetMembers<MethodSymbol>("ToCharArray", context).First(e => e.Parameters.Length == 0);
            PropertySymbol lengthProperty = context.GetTypeSymbol(SpecialType.System_Array).GetMember<PropertySymbol>("Length", context);
            
            Value iteratorValue = context.EmitValue(BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, toCharArrayMethod, IteratorSource, new BoundExpression[] {}));
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
                BoundAccessExpression.BindElementAccess(context, SyntaxNode, iteratorAccess,
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
