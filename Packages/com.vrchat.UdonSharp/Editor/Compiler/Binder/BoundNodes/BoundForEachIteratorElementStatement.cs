
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundForEachIteratorElementStatement : BoundStatement
    {
        private BoundExpression IteratorSource { get; }
        private Symbol ValueSymbol { get; }
        private BoundStatement BodyStatement { get; }
        private MethodSymbol IteratorMethod { get; }
        private MethodSymbol IteratorMoveNextMethod { get; }
        private MethodSymbol IteratorCurrentPropertyGet { get; }
        
        public BoundForEachIteratorElementStatement(AbstractPhaseContext context, SyntaxNode node, BoundExpression iteratorSource, Symbol valueSymbol, BoundStatement bodyStatement)
            :base(node)
        {
            IteratorSource = iteratorSource;
            ValueSymbol = valueSymbol;
            BodyStatement = bodyStatement;
            
            IteratorMethod = IteratorSource.ValueType.GetMember<MethodSymbol>("GetEnumerator", context);
            
            // Just bind these to run the redirect logic and mark stuff referenced
            IteratorMethod = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, IteratorMethod, IteratorSource, Array.Empty<BoundExpression>()).Method;
            IteratorMoveNextMethod = IteratorMethod.ReturnType.GetMember<MethodSymbol>("MoveNext", context);
            IteratorCurrentPropertyGet = IteratorMethod.ReturnType.GetMember<PropertySymbol>("Current", context).GetMethod;
            
            IteratorMoveNextMethod = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, IteratorMoveNextMethod, IteratorSource, Array.Empty<BoundExpression>()).Method;
            IteratorCurrentPropertyGet = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, IteratorCurrentPropertyGet, IteratorSource, Array.Empty<BoundExpression>()).Method;
            
            context.MarkSymbolReferenced(IteratorMethod);
            context.MarkSymbolReferenced(IteratorMoveNextMethod);
            context.MarkSymbolReferenced(IteratorCurrentPropertyGet);
        }

        public override void Emit(EmitContext context)
        {
            IDisposable blockScope = context.OpenBlockScope();
            
            Value sourceDictValue = context.EmitValue(IteratorSource);
            sourceDictValue.MarkUsedRecursively();
            
            BoundAccessExpression iteratorAccess = BoundAccessExpression.BindAccess(sourceDictValue);
            
            BoundInvocationExpression getIteratorExpression = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, IteratorMethod, iteratorAccess, Array.Empty<BoundExpression>());
            Value iteratorValue = context.EmitValue(getIteratorExpression);
            iteratorValue.MarkUsedRecursively();
            BoundAccessExpression iteratorValueAccess = BoundAccessExpression.BindAccess(iteratorValue);
            
            BoundExpression getCurrentExpression = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, IteratorCurrentPropertyGet, iteratorValueAccess, Array.Empty<BoundExpression>());
            
            BoundExpression moveNext = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, IteratorMoveNextMethod, iteratorValueAccess, Array.Empty<BoundExpression>());
            
            JumpLabel loopLabel = context.Module.CreateLabel();
            JumpLabel continueLabel = context.PushContinueLabel();
            JumpLabel exitLoopLabel = context.PushBreakLabel();
            
            context.Module.LabelJump(loopLabel);
            context.Module.LabelJump(continueLabel);
            
            Value moveNextResult = context.EmitValue(moveNext);
            context.Module.AddJumpIfFalse(exitLoopLabel, moveNextResult);
            
            // Copy the Current value to the user value
            context.EmitValueAssignment(context.GetUserValue(ValueSymbol), getCurrentExpression);
            
            context.Emit(BodyStatement);
            
            context.Module.AddJump(loopLabel);
            
            context.Module.LabelJump(exitLoopLabel);
            
            context.PopBreakLabel();
            context.PopContinueLabel();
            
            blockScope.Dispose();
        }
    }
}
