
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Core;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundUserMethodInvocationExpression : BoundInvocationExpression
    {
        protected BoundUserMethodInvocationExpression(SyntaxNode node, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions) 
            : base(node, method, instanceExpression, parameterExpressions)
        {
        }

        protected bool IsBaseCall { get; private set; }
        
        public override void MarkForcedBaseCall()
        {
            IsBaseCall = true;
        }

        public override Value EmitValue(EmitContext context)
        {
            JumpLabel returnPoint = context.Module.CreateLabel();
            Value returnPointVal =
                context.CreateGlobalInternalValue(context.GetTypeSymbol(SpecialType.System_UInt32));

            context.Module.AddPush(returnPointVal);
            var linkage = context.GetMethodLinkage(Method, !IsBaseCall);

            Value[] parameterValues = GetParameterValues(context);

            Value[] recursiveValues = null;
            bool isRecursiveCall = context.IsRecursiveMethodEmit;

            Value stackSizeCheckVal = null;
            
            if (isRecursiveCall)
            {
                EmitContext.MethodLinkage selfLinkage = context.GetMethodLinkage(context.CurrentEmitMethod, false);
                recursiveValues = selfLinkage.ParameterValues;
                
                stackSizeCheckVal = context.CreateGlobalInternalValue(context.GetTypeSymbol(SpecialType.System_Int32));
                
                CheckStackSize(stackSizeCheckVal, context);
                PushRecursiveValues(selfLinkage.ParameterValues, context);
            }
            
            ReleaseCowReferences(context);

            if (isRecursiveCall)
            {
                Value.CowValue[] paramCows = parameterValues.Select(e => e.GetCowValue(context)).ToArray();

                for (int i = 0; i < linkage.ParameterValues.Length; ++i)
                    context.EmitValueAssignment(linkage.ParameterValues[i], BoundAccessExpression.BindAccess(paramCows[i]));

                foreach (var paramCow in paramCows)
                    paramCow.Dispose();
            }
            else
            {
                for (int i = 0; i < linkage.ParameterValues.Length; ++i)
                    context.Module.AddCopy(parameterValues[i], linkage.ParameterValues[i]);
            }

            context.TopTable.DirtyAllValues();

            if (isRecursiveCall)
            {
                Value[] collectedValues = context.CollectRecursiveValues().Where(e => !recursiveValues.Contains(e)).ToArray();
                
                PushRecursiveValues(collectedValues, context);

                recursiveValues = recursiveValues.Concat(collectedValues).ToArray();

                stackSizeCheckVal.DefaultValue = recursiveValues.Length;
                context.UpdateRecursiveStackMaxSize(recursiveValues.Length);
            }
                
            context.Module.AddCommentTag($"Calling {Method}");
            context.Module.AddJump(linkage.MethodLabel);

            context.Module.LabelJump(returnPoint);
            returnPointVal.DefaultValue = returnPoint.Address;

            Value recursiveRet = null;

            if (isRecursiveCall)
            {
                if (linkage.ReturnValue != null)
                {
                    recursiveRet = context.CreateInternalValue(linkage.ReturnValue.UserType);
                    context.Module.AddCopy(linkage.ReturnValue, recursiveRet);
                }

                PopRecursiveValues(recursiveValues, context);
            }

            // Handle out/ref parameters
            for (int i = 0; i < Method.Parameters.Length; ++i)
            {
                if (!Method.Parameters[i].IsOut) continue;
                
                if (isRecursiveCall)
                    throw new CompilerException("U# does not yet support calling user methods with ref/out parameters from methods marked with RecursiveMethod");
                
                BoundAccessExpression paramAccess = (BoundAccessExpression)ParameterExpressions[i];

                paramAccess.EmitSet(context, BoundAccessExpression.BindAccess(linkage.ParameterValues[i]));
            }

            // Properties need to return the value that they are set to for assignment expressions
            if (IsPropertySetter)
            {
                return parameterValues.Last();
            }

            if (Method.ReturnType != null)
            {
                if (isRecursiveCall)
                    return recursiveRet;
                
                return linkage.ReturnValue;
            }

            return null;
        }
    }
}
