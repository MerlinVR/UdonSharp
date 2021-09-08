
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundUserMethodInvocationExpression : BoundInvocationExpression
    {
        protected BoundUserMethodInvocationExpression(SyntaxNode node, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions) 
            : base(node, method, instanceExpression, parameterExpressions)
        {
        }

        private bool _isBaseCall;
        
        public override void MarkForcedBaseCall()
        {
            _isBaseCall = true;
        }

        public override Value EmitValue(EmitContext context)
        {
            JumpLabel returnPoint = context.Module.CreateLabel();
            Value returnPointVal =
                context.CreateGlobalInternalValue(context.GetTypeSymbol(SpecialType.System_UInt32));

            context.Module.AddPush(returnPointVal);
            var linkage = context.GetMethodLinkage(Method, !_isBaseCall);

            Value[] parameterValues = GetParameterValues(context);
            
            context.TopTable.DirtyAllValues();
                
            for (int i = 0; i < linkage.ParameterValues.Length; ++i)
                context.Module.AddCopy(parameterValues[i], linkage.ParameterValues[i]);
                
            context.Module.AddJump(linkage.MethodLabel);

            context.Module.LabelJump(returnPoint);
            returnPointVal.DefaultValue = returnPoint.Address;

            // Handle out/ref parameters
            for (int i = 0; i < Method.Parameters.Length; ++i)
            {
                if (!Method.Parameters[i].IsOut) continue;
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
                // Value returnVal = context.CreateInternalValue(Method.ReturnType);
                //     
                // context.Module.AddCopy(linkage.ReturnValue, returnVal);
                //
                // return returnVal;

                return linkage.ReturnValue;
            }

            return null;
        }
    }
}
