
using System;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal sealed class ExternSynthesizedMethodSymbol : ExternMethodSymbol
    {
        public override bool IsStatic { get; }

        public override string Name => ExternSignature;

        public ExternSynthesizedMethodSymbol(AbstractPhaseContext context, string methodName, TypeSymbol containingType, TypeSymbol[] parameterTypes, TypeSymbol returnType, bool isStatic, bool isConstructor = false)
            : base(null, context)
        {
            Parameters = parameterTypes.Select(e => new ParameterSymbol(e, context)).ToImmutableArray();
            ReturnType = returnType;
            IsStatic = isStatic;
            IsConstructor = isConstructor;
            
            ExternSignature = BuildExternSignature(containingType, methodName);
        }
        
        public ExternSynthesizedMethodSymbol(AbstractPhaseContext context, string externSignature, TypeSymbol[] parameterTypes, TypeSymbol returnType, bool isStatic, bool isConstructor = false)
            : base(null, context)
        {
            Parameters = parameterTypes.Select(e => new ParameterSymbol(e, context)).ToImmutableArray();
            ReturnType = returnType;
            IsStatic = isStatic;
            IsConstructor = isConstructor;
            ExternSignature = externSignature;
        }

        private string BuildExternSignature(TypeSymbol containingType, string methodName)
        {
            Type methodSourceType = containingType.UdonType.SystemType;

            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            string functionNamespace = CompilerUdonInterface.SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            methodName = $"__{methodName}";
            var parameters = Parameters;

            string paramStr = "";
            
            if (parameters.Length > 0)
            {
                paramStr = "_"; // Arg separator
            
                foreach (ParameterSymbol parameter in parameters)
                {
                    paramStr += $"_{CompilerUdonInterface.GetUdonTypeName(parameter.Type)}";
                }
            }
            else if (IsConstructor)
                paramStr = "__";

            string returnStr;

            if (!IsConstructor)
            {
                returnStr = ReturnType != null ? $"__{CompilerUdonInterface.GetUdonTypeName(ReturnType)}" : "__SystemVoid";
            }
            else
            {
                returnStr = $"__{CompilerUdonInterface.GetUdonTypeName(containingType)}";
            }

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}{returnStr}";

            return finalFunctionSig;
        }

        public override string ExternSignature { get; }

        public override string ToString()
        {
            return $"ExternSynthesizedMethodSymbol: {ExternSignature}";
        }
    }
}
