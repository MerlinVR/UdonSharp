
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Core;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundUdonSharpBehaviourInvocationExpression : BoundUserMethodInvocationExpression
    {
        public BoundUdonSharpBehaviourInvocationExpression(SyntaxNode node, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions)
            :base(node, method, instanceExpression, parameterExpressions)
        {
        }

        public override Value EmitValue(EmitContext context)
        {
            // Make base calls to UdonSharpBehaviour events a noop
            if (IsBaseCall)
            {
                if (Method.ContainingType == context.GetTypeSymbol(typeof(UdonSharpBehaviour)))
                {
                    if (Method.Name == "OnOwnershipRequest")
                        return context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Boolean), true);
                    
                    return null;
                }
            }
            
            if (SourceExpression == null || SourceExpression.IsThis)
                return base.EmitValue(context);

            // Calls across UdonBehaviours
            CompilationContext.MethodExportLayout layout =
                context.CompileContext.GetUsbMethodLayout(Method, context);

            Value.CowValue instanceCowValue = GetInstanceValue(context);
            Value instanceValue = instanceCowValue.Value;
            BoundAccessExpression instanceAccess = BoundAccessExpression.BindAccess(instanceValue);

            TypeSymbol stringType = context.GetTypeSymbol(SpecialType.System_String);

            Value[] recursiveValues = null;
            Value stackSizeCheckVal = null;
            bool isRecursiveCall = context.IsRecursiveMethodEmit;

            if (isRecursiveCall)
            {
                stackSizeCheckVal = context.CreateGlobalInternalValue(context.GetTypeSymbol(SpecialType.System_Int32));
            }
                
            if (Method.Parameters.Length > 0)
            {
                MethodSymbol setProgramVariableMethod = context.GetTypeSymbol(typeof(UdonSharpBehaviour))
                    .GetMembers<MethodSymbol>("SetProgramVariable", context)
                    .First(e => e.Parameters.Length == 2 && 
                                e.Parameters[0].Type == stringType);

                Value[] parameterValues = GetParameterValues(context);
                instanceValue = instanceCowValue.Value;
                instanceAccess = BoundAccessExpression.BindAccess(instanceValue); // Re-bind here since the parameters may have changed the cowvalue

                if (isRecursiveCall)
                {
                    EmitContext.MethodLinkage selfLinkage = context.GetMethodLinkage(context.CurrentEmitMethod, false);
                    recursiveValues = selfLinkage.ParameterValues;
                
                    CheckStackSize(stackSizeCheckVal, context);
                    PushRecursiveValues(recursiveValues, context);

                    for (int i = 0; i < parameterValues.Length; ++i)
                    {
                        Value paramIntermediate = context.CreateInternalValue(parameterValues[i].UserType);
                        context.Module.AddCopy(parameterValues[i], paramIntermediate);
                        parameterValues[i] = paramIntermediate;
                    }
                }
                else
                {
                    instanceCowValue.Dispose();
                }
                
                context.TopTable.DirtyAllValues();
                    
                for (int i = 0; i < Method.Parameters.Length; ++i)
                {
                    context.Emit(CreateBoundInvocation(context, SyntaxNode, setProgramVariableMethod, instanceAccess,
                            new BoundExpression[]
                            {
                                BoundAccessExpression.BindAccess(context.GetConstantValue(stringType,layout.ParameterExportNames[i])), 
                                BoundAccessExpression.BindAccess(parameterValues[i])
                            }));
                }
            }

            if (isRecursiveCall)
            {
                if (recursiveValues == null)
                    recursiveValues = Array.Empty<Value>();

                Value[] scopeValues = context.CollectRecursiveValues();
                
                PushRecursiveValues(scopeValues, context);

                recursiveValues = recursiveValues.Concat(scopeValues).ToArray();

                stackSizeCheckVal.DefaultValue = recursiveValues.Length;
                context.UpdateRecursiveStackMaxSize(recursiveValues.Length);
            }

            MethodSymbol sendCustomEventMethod = context.GetTypeSymbol(typeof(UdonSharpBehaviour)).GetMember<MethodSymbol>("SendCustomEvent", context);
            context.Emit(CreateBoundInvocation(context, SyntaxNode, sendCustomEventMethod, BoundAccessExpression.BindAccess(instanceValue),
                    new BoundExpression[]
                    {
                        BoundAccessExpression.BindAccess(context.GetConstantValue(stringType, layout.ExportMethodName))
                    }));
            
            if (isRecursiveCall)
                PopRecursiveValues(recursiveValues, context);

            if (Method.Parameters.Length > 0 &&
                Method.Parameters.Any(e => e.IsOut))
            {
                if (isRecursiveCall)
                    throw new CompilerException("U# does not yet support calling user methods with ref/out parameters from methods marked with RecursiveMethod");

                MethodSymbol getProgramVariableMethod = context.GetTypeSymbol(typeof(UdonSharpBehaviour))
                    .GetMembers<MethodSymbol>("GetProgramVariable", context)
                    .First(e => e.Parameters.Length == 1 && 
                                e.Parameters[0].Type == stringType);

                // Copy out/ref parameters back
                for (int i = 0; i < Method.Parameters.Length; ++i)
                {
                    ParameterSymbol parameterSymbol = Method.Parameters[i];

                    if (parameterSymbol.IsOut)
                    {
                        BoundAccessExpression currentAccessExpression = (BoundAccessExpression)ParameterExpressions[i];

                        currentAccessExpression.EmitSet(context, CreateBoundInvocation(context, SyntaxNode,
                            getProgramVariableMethod, instanceAccess, new[]
                            {
                                BoundAccessExpression.BindAccess(context.GetConstantValue(stringType,
                                    layout.ParameterExportNames[i]))
                            }));
                    }
                }
            }
            
            if (IsPropertySetter)
            {
                return GetParameterValues(context).Last();
            }

            if (Method.ReturnType != null)
            {
                MethodSymbol getProgramVariableMethod = context.GetTypeSymbol(typeof(UdonSharpBehaviour))
                    .GetMembers<MethodSymbol>("GetProgramVariable", context)
                    .First(e => e.Parameters.Length == 1 && 
                                e.Parameters[0].Type == stringType);

                Value returnVal = context.CreateInternalValue(Method.ReturnType);

                BoundInvocationExpression boundGetReturn = CreateBoundInvocation(context, SyntaxNode,
                    getProgramVariableMethod, instanceAccess,
                    new BoundExpression[]
                    {
                        BoundAccessExpression.BindAccess(context.GetConstantValue(stringType,
                            layout.ReturnExportName))
                    });

                context.EmitValueAssignment(returnVal, boundGetReturn);

                return returnVal;
            }

            return null;
        }
    }
}
