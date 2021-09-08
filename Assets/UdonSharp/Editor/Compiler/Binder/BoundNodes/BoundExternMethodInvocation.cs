
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundExternInvocation : BoundInvocationExpression
    {
        public BoundExternInvocation(SyntaxNode node, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions) 
            : base(node, method, instanceExpression, parameterExpressions)
        {
        }

        public override Value EmitValue(EmitContext context)
        {
            var module = context.Module;

            bool hasInstance = false;
            
            using (context.InterruptAssignmentScope())
            {
                if (!Method.IsStatic && !Method.IsConstructor)
                {
                    GetInstanceValue(context);
                    hasInstance = true;

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

            if (hasInstance)
            {
                Value.CowValue instanceValue = GetInstanceValue(context);
                module.AddPush(instanceValue.Value);
                instanceValue.Dispose();
            }

            string methodName = Method.Name;
            if (methodName == "SendCustomEvent" ||
                methodName == "SendCustomNetworkEvent" ||
                methodName == "RunProgram")
                context.TopTable.DirtyAllValues();

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

            module.AddExtern((ExternMethodSymbol) Method);

            return returnValue;
        }

        public override string ToString()
        {
            return $"BoundExternInvocation: {Method}";
        }
    }
}
