
using System.Collections.Generic;
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
            Value returnPointVal = context.CreateGlobalInternalValue(context.GetTypeSymbol(SpecialType.System_UInt32));

            context.Module.AddPush(returnPointVal);
            EmitContext.MethodLinkage linkage = context.GetMethodLinkage(Method, !IsBaseCall);

            Value.CowValue instanceCow = null;

            if (Method.IsConstructor)
            {
                Value.CowValue[] instanceValues = context.GetExpressionCowValues(this, "instance");

                if (instanceValues == null)
                {
                    // using (context.InterruptAssignmentScope())
                    {
                        instanceCow = context.EmitValue(new BoundArrayCreationExpression(SyntaxNode, context,
                                context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context),
                                new BoundExpression[]
                                {
                                    BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), Method.ContainingType.UserTypeAllocationSize))
                                }, 
                                null))
                            .GetCowValue(context);
                    }
                    
                    context.RegisterCowValues(new [] { instanceCow }, this, "instance");
                }
            }
            else if (!Method.IsStatic && !SourceExpression.IsThis)
            {
                instanceCow = EmitInstanceValue(context);
            }
            
            Value[] parameterValues = EmitParameterValues(context);

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
            
            Value instanceValue = instanceCow?.Value;

            ReleaseCowReferences(context);

            if (isRecursiveCall)
            {
                Value.CowValue[] paramCows = parameterValues.Select(e => e.GetCowValue(context)).ToArray();

                for (int i = 0; i < linkage.ParameterValues.Length; ++i)
                    context.EmitValueAssignment(linkage.ParameterValues[i], BoundAccessExpression.BindAccess(paramCows[i]));

                foreach (Value.CowValue paramCow in paramCows)
                    paramCow.Dispose();
            }
            else
            {
                for (int i = 0; i < linkage.ParameterValues.Length; ++i)
                    context.Module.AddCopy(parameterValues[i], linkage.ParameterValues[i]);
            }
            
            context.TopTable.DirtyAllValues();

            Value stackInstanceVal = null;
            
            // We can use the existing stack instance value if we are calling a method on the same instance as the current method
            if (instanceValue != null && (!(SourceExpression?.IsThis ?? true) || Method.IsConstructor || context.CurrentEmitMethod.ContainingType != Method.ContainingType))
            {
                stackInstanceVal = context.CreateInternalValue(context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context));
                context.Module.AddCopy(context.GetInstanceValue(), stackInstanceVal); // Copy the current instance value to the stack
                context.Module.AddCopy(instanceValue, context.GetInstanceValue()); // Set the instance value to the new instance
                
                // Make editor builds try to access element 0 of the instance array that we know exists in order to trigger a null reference exception earlier so it's more obvious what's going on
                if (context.CompileContext.Options.IsEditorBuild)
                {
                    BoundArrayAccessExpression arrayAccess = new BoundArrayAccessExpression(
                        SyntaxNode,
                        context, 
                        BoundAccessExpression.BindAccess(context.GetInstanceValue()),
                        new BoundExpression[] { BoundAccessExpression.BindAccess(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), 0)) });
                    
                    context.Module.AddCommentTag("Editor-only null check");
                    context.EmitValue(arrayAccess);
                }
            }

            if (isRecursiveCall)
            {
                Value[] values = recursiveValues;
                IEnumerable<Value> collectedValues = context.CollectRecursiveValues().Where(e => !values.Contains(e));
                
                if (stackInstanceVal != null)
                    collectedValues = collectedValues.Append(stackInstanceVal);
                
                Value[] recursiveValuesArray = collectedValues.ToArray();
                
                PushRecursiveValues(recursiveValuesArray, context);

                recursiveValues = recursiveValues.Concat(recursiveValuesArray).ToArray();

                stackSizeCheckVal.DefaultValue = recursiveValues.Length;
                context.UpdateRecursiveStackMaxSize(recursiveValues.Length);
            }
            
            context.Module.AddCommentTag($"Calling {Method}");
            
            // Jump to the method
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

            if (stackInstanceVal != null)
                context.Module.AddCopy(stackInstanceVal, context.GetInstanceValue());

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

            if (Method.IsConstructor)
            {
                return instanceValue;
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
