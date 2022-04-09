
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Compiler.Udon;
using UdonSharp.Core;
using UdonSharp.Localization;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundExternInvocation : BoundInvocationExpression
    {
        private ExternMethodSymbol externMethodSymbol;

        public BoundExternInvocation(SyntaxNode node, AbstractPhaseContext context, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions) 
            : base(node, method, instanceExpression, parameterExpressions)
        {
            externMethodSymbol = (ExternMethodSymbol)method;
            if (!CompilerUdonInterface.IsExposedToUdon(externMethodSymbol.ExternSignature))
            {
                externMethodSymbol = FindAlternateInvocation(context, method, instanceExpression, parameterExpressions);
                if (externMethodSymbol == null)
                {
                    throw new NotExposedException(LocStr.CE_UdonMethodNotExposed, node, $"{method.RoslynSymbol?.ToDisplayString() ?? method.ToString()}, sig: {((ExternMethodSymbol)method).ExternSignature}");
                }
            }
        }

        private ExternMethodSymbol FindAlternateInvocation(AbstractPhaseContext context, MethodSymbol originalSymbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions)
        {
            if (originalSymbol.IsStatic || originalSymbol.IsConstructor) return null;

            List<TypeSymbol> candidates = new List<TypeSymbol>();
            FindCandidateInvocationTypes(context, candidates, instanceExpression.ValueType);

            TypeSymbol[] paramTypes = parameterExpressions.Select(ex => ex.ValueType).ToArray();

            foreach (TypeSymbol candidate in candidates)
            {
                ExternMethodSymbol externMethodSymbol = new ExternSynthesizedMethodSymbol(context, originalSymbol.Name, candidate, paramTypes, originalSymbol.ReturnType, false, false);
                if (CompilerUdonInterface.IsExposedToUdon(externMethodSymbol.ExternSignature))
                {
                    return externMethodSymbol;
                }
            }

            return null;
        }

        void FindCandidateInvocationTypes(AbstractPhaseContext context, List<TypeSymbol> candidates, TypeSymbol ty)
        {
            foreach (var intf in ty.RoslynSymbol.AllInterfaces)
            {
                candidates.Add(context.GetTypeSymbol(intf));
            }

            while (ty != null)
            {
                candidates.Add(ty);
                ty = ty.BaseType;
            }
        }

        public override Value EmitValue(EmitContext context)
        {
            var module = context.Module;

            Value.CowValue instanceValue = null;
            
            using (context.InterruptAssignmentScope())
            {
                if (!Method.IsStatic && !Method.IsConstructor)
                {
                    instanceValue = GetInstanceValue(context);

                    // Prevent mutating a value constant if you call a method on the constant for some reason
                    // todo: fix for use with the cowvalues
                    // if (instanceValue.IsConstant && instanceValue.UserType.IsValueType &&
                    //     Method.Name != "ToString" && Method.Name != "Equals") // Common non-mutating methods
                    // {
                    //     Value proxyInstanceValue = context.CreateInternalValue(instanceValue.UserType);
                    //     module.AddCopy(instanceValue, proxyInstanceValue);
                    //     instanceValue = proxyInstanceValue;
                    // }
                }
            }

            Value[] parameterValues = GetParameterValues(context);

            if (instanceValue != null)
                module.AddPush(instanceValue.Value);

            Value[] recursiveValues = null;
            
            string methodName = Method.Name;
            
            if (methodName == "SendCustomEvent" ||
                methodName == "SendCustomNetworkEvent" ||
                methodName == "RunProgram")
            {
                if (context.IsRecursiveMethodEmit)
                {
                    recursiveValues = context.CollectRecursiveValues();
                    CheckStackSize(context.GetConstantValue(context.GetTypeSymbol(SpecialType.System_Int32), recursiveValues.Length), context);
                    PushRecursiveValues(recursiveValues, context);
                    context.UpdateRecursiveStackMaxSize(recursiveValues.Length);
                }
                else
                {
                    instanceValue?.Dispose();
                }

                context.TopTable.DirtyAllValues();
            }

            foreach (Value value in parameterValues)
                module.AddPush(value);

            Value returnValue = null;

            if (IsPropertySetter)
                returnValue = parameterValues.Last();

            if (Method.ReturnType != null)
            {
                returnValue = context.GetReturnValue(Method.ReturnType);
                if (returnValue.IsConstant)
                    throw new InvalidOperationException("Cannot return to const value");
                
                module.AddPush(returnValue);
            }

            module.AddExtern(externMethodSymbol);
            
            if (recursiveValues != null)
                PopRecursiveValues(recursiveValues, context);

            return returnValue;
        }

        public override string ToString()
        {
            return $"BoundExternInvocation: {Method}";
        }
    }
}
