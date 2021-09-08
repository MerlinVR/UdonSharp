
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal class ExternMethodSymbol : MethodSymbol, IExternSymbol
    {
        public ExternMethodSymbol(IMethodSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol != null)
                ExternSignature = GetUdonMethodName(this, context);
        }

        public override bool IsExtern => true;

        public override bool IsBound => true;
        public virtual string ExternSignature { get; }

        public override string ToString()
        {
            return $"ExternMethodSymbol: {RoslynSymbol}";
        }
        
        protected static string GetUdonMethodName(ExternMethodSymbol methodSymbol, AbstractPhaseContext context)
        {
            Type methodSourceType = methodSymbol.ContainingType.UdonType.SystemType;
            var roslynSymbol = methodSymbol.RoslynSymbol;

            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            string functionNamespace = CompilerUdonInterface.SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            string methodName = $"__{methodSymbol.Name.Trim('_').TrimStart('.')}";
            var parameters = roslynSymbol.Parameters;

            string paramStr = "";
            
            if (parameters.Length > 0)
            {
                paramStr = "_"; // Arg separator
            
                foreach (IParameterSymbol parameter in parameters)
                {
                    paramStr += $"_{CompilerUdonInterface.GetUdonTypeName(context.GetTypeSymbol(parameter.Type))}";
                    if (parameter.RefKind != RefKind.None)
                        paramStr += "Ref";
                }
            }
            else if (methodSymbol.IsConstructor)
                paramStr = "__";

            string returnStr = "";

            if (!methodSymbol.IsConstructor)
            {
                returnStr = $"__{CompilerUdonInterface.GetUdonTypeName(context.GetTypeSymbol(roslynSymbol.ReturnType))}";
            }
            else
            {
                returnStr = $"__{CompilerUdonInterface.GetUdonTypeName(methodSymbol.ContainingType)}";
            }

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}{returnStr}";

            return finalFunctionSig;
        }
    }
}
