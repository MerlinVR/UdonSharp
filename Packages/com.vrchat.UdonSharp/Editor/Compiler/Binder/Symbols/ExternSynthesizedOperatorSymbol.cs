

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal sealed class ExternSynthesizedOperatorSymbol : ExternMethodSymbol
    {
        private BuiltinOperatorType OperatorType { get; }

        public override bool IsStatic => true;

        public override string Name => ExternSignature;

        public ExternSynthesizedOperatorSymbol(BuiltinOperatorType operatorType, TypeSymbol type, AbstractPhaseContext context)
            : base(null, context)
        {
            TypeSymbol boolType = context.GetTypeSymbol(SpecialType.System_Boolean);

            if (type == boolType && operatorType == BuiltinOperatorType.UnaryNegation)
            {
                Parameters = new[] {new ParameterSymbol(type, context)}.ToImmutableArray();
            }
            else
            {
                if ((operatorType == BuiltinOperatorType.LeftShift || operatorType == BuiltinOperatorType.RightShift) &&
                    (type == context.GetTypeSymbol(SpecialType.System_UInt32) ||
                     type == context.GetTypeSymbol(SpecialType.System_UInt64) ||
                     type == context.GetTypeSymbol(SpecialType.System_Int64)))
                {
                    Parameters = new[]
                    {
                        new ParameterSymbol(type, context),
                        new ParameterSymbol(context.GetTypeSymbol(SpecialType.System_Int32), context)
                    }.ToImmutableArray();
                }
                else
                    Parameters = new[] {new ParameterSymbol(type, context), new ParameterSymbol(type, context)}
                        .ToImmutableArray();
            }

            if (operatorType == BuiltinOperatorType.Equality ||
                operatorType == BuiltinOperatorType.Inequality ||
                operatorType == BuiltinOperatorType.LessThan ||
                operatorType == BuiltinOperatorType.LessThanOrEqual ||
                operatorType == BuiltinOperatorType.GreaterThan || 
                operatorType == BuiltinOperatorType.GreaterThanOrEqual)
            {
                ReturnType = context.GetTypeSymbol(SpecialType.System_Boolean);
            }
            else
            {
                if (type == context.GetTypeSymbol(SpecialType.System_Byte) ||
                    type == context.GetTypeSymbol(SpecialType.System_SByte) ||
                    type == context.GetTypeSymbol(SpecialType.System_Int16) ||
                    type == context.GetTypeSymbol(SpecialType.System_UInt16))
                {
                    ReturnType = context.GetTypeSymbol(SpecialType.System_Int32);
                }
                else if (operatorType == BuiltinOperatorType.UnaryMinus &&
                         type == context.GetTypeSymbol(SpecialType.System_UInt32))
                {
                    ReturnType = context.GetTypeSymbol(SpecialType.System_Int64);
                }
                else
                {
                    ReturnType = type;
                }
            }

            OperatorType = operatorType;
            ExternSignature = GetSignature(context);

            IsOperator = true;
        }

        public override string ExternSignature { get; }

        private string GetSignature(AbstractPhaseContext context)
        {
            string typeName = Parameters[0].Type.UdonType.ExternSignature;
            string returnName = ReturnType.UdonType.ExternSignature;
            
            if (Parameters.Length == 2)
                return $"{typeName}.__op_{OperatorType.ToString()}__{typeName}_{Parameters[1].Type.UdonType.ExternSignature}__{returnName}";
            
            return $"{typeName}.__op_{OperatorType.ToString()}__{typeName}__{returnName}";
        }

        public override string ToString()
        {
            return $"ExternSynthesizedBuiltinOperatorSymbol: {RoslynSymbol}";
        }
    }
}
