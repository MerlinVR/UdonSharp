
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal enum BuiltinOperatorType
    {
        // -- Math
        Addition,
        Subtraction,
        Multiplication,
        Division,
        Remainder,
        LeftShift,
        RightShift,
        // UnaryPlus // This should be discarded before it gets to this point. I don't think there are any types that are relevant in Udon where this would matter
        UnaryMinus,
        // These operators are not present in Udon so we should handle them at a higher level, and we need handling for prefix vs postfix
        //  UnaryIncrement,
        //  UnaryDecrement,
        // -- Comparison
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual, 
        Equality,
        Inequality, 
        // -- Boolean operators
        UnaryNegation,
        ConditionalAnd, 
        ConditionalOr, 
        // -- Bitwise operators
        LogicalAnd, 
        LogicalOr, 
        LogicalXor,
        BitwiseNot, // Now has hack workaround handling
    }
    
    internal sealed class ExternBuiltinOperatorSymbol : ExternMethodSymbol
    {
        public BuiltinOperatorType OperatorType { get; }
        
        public ExternBuiltinOperatorSymbol(IMethodSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol.Parameters.Length == 1 &&
                (ContainingType == context.GetTypeSymbol(SpecialType.System_Byte) ||
                 ContainingType == context.GetTypeSymbol(SpecialType.System_SByte) ||
                 ContainingType == context.GetTypeSymbol(SpecialType.System_Int16) ||
                 ContainingType == context.GetTypeSymbol(SpecialType.System_UInt16)))
            {
                ReturnType = context.GetTypeSymbol(SpecialType.System_Int32);
            }

            OperatorType = TranslateOperatorName(sourceSymbol.Name);
            ExternSignature = GetSignature(context);
        }

        private static BuiltinOperatorType TranslateOperatorName(string operatorName)
        {
            switch (operatorName)
            {
                case "op_Addition":
                case "op_Increment":
                    return BuiltinOperatorType.Addition;
                case "op_Subtraction":
                case "op_Decrement":
                    return BuiltinOperatorType.Subtraction;
                case "op_Division":
                    return BuiltinOperatorType.Division;
                case "op_Multiply":
                    return BuiltinOperatorType.Multiplication;
                case "op_Modulus":
                    return BuiltinOperatorType.Remainder;
                case "op_LeftShift":
                    return BuiltinOperatorType.LeftShift;
                case "op_RightShift":
                    return BuiltinOperatorType.RightShift;
                case "op_ExclusiveOr":
                    return BuiltinOperatorType.LogicalXor;
                case "op_BitwiseOr":
                    return BuiltinOperatorType.LogicalOr;
                case "op_BitwiseAnd":
                    return BuiltinOperatorType.LogicalAnd;
                case "op_Equality":
                    return BuiltinOperatorType.Equality;
                case "op_Inequality":
                    return BuiltinOperatorType.Inequality;
                case "op_GreaterThan":
                    return BuiltinOperatorType.GreaterThan;
                case "op_GreaterThanOrEqual":
                    return BuiltinOperatorType.GreaterThanOrEqual;
                case "op_LessThan":
                    return BuiltinOperatorType.LessThan;
                case "op_LessThanOrEqual":
                    return BuiltinOperatorType.LessThanOrEqual;
                case "op_OnesComplement": // What kind of name is this Roslyn? This is for `~somevalue`
                    return BuiltinOperatorType.BitwiseNot;
                case "op_LogicalNot": // Very cool mapping name-wise for Roslyn to Udon, this is for bools `!`
                    return BuiltinOperatorType.UnaryNegation;
                case "op_UnaryNegation": // And this is for integers `-somevalue`
                    return BuiltinOperatorType.UnaryMinus;
            }

            throw new ArgumentException($"Unhandled operator {operatorName}");
        }

        public override string ExternSignature { get; }

        private string _udonName;
        private string GetOperatorUdonName()
        {
            if (_udonName != null) return _udonName;
            
            _udonName = Name;

            if (RoslynSymbol.ReturnType.SpecialType != SpecialType.System_Boolean)
            {
                _udonName = _udonName.Replace("UnaryNegation", "UnaryMinus");
            }

            _udonName = _udonName.Replace("BitwiseAnd", "LogicalAnd");
            _udonName = _udonName.Replace("BitwiseOr", "LogicalOr"); 
            _udonName = _udonName.Replace("ExclusiveOr", "LogicalXor");
            _udonName = _udonName.Replace("Modulus", "Remainder");
            _udonName = _udonName.Replace("LogicalNot", "UnaryNegation");
            _udonName = _udonName.Replace("Increment", "Addition");
            _udonName = _udonName.Replace("Decrement", "Subtraction");
            
            if (ContainingType.UdonType.SystemType != typeof(decimal))
                _udonName = _udonName.Replace("Multiply", "Multiplication");

            return _udonName;
        }

        private string GetSignature(AbstractPhaseContext context)
        {
            System.Type methodSourceType = ContainingType.UdonType.SystemType;

            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            if (methodSourceType == typeof(string) && 
                (Parameters[0].Type.UdonType.SystemType == typeof(object) || 
                 Parameters[1].Type.UdonType.SystemType == typeof(object)))
            {
                return "SystemString.__Concat__SystemObject_SystemObject__SystemString";
            }

            string functionNamespace = CompilerUdonInterface.SanitizeTypeName(methodSourceType.FullName ??
                                                                              methodSourceType.Namespace + methodSourceType.Name);

            string methodName = $"__{GetOperatorUdonName().Trim('_').TrimStart('.')}";
            var parameters = RoslynSymbol.Parameters;

            string paramStr = "_"; // Arg separator

            if (parameters.Length > 1 || methodName.Contains("UnaryMinus") || methodName.Contains("UnaryNegation")) // Binary operators
            {
                foreach (IParameterSymbol parameter in parameters)
                {
                    paramStr +=
                        $"_{CompilerUdonInterface.GetUdonTypeName(context.GetTypeSymbol(parameter.Type))}";
                }
            }
            else // Unary operators, we just use the regular binary operator internally and handle it in the bound operator
            {
                paramStr += $"_{CompilerUdonInterface.GetUdonTypeName(context.GetTypeSymbol(parameters[0].Type))}";
                paramStr += $"_{CompilerUdonInterface.GetUdonTypeName(context.GetTypeSymbol(parameters[0].Type))}";
            }
            
            string returnStr =
                $"__{CompilerUdonInterface.GetUdonTypeName(ReturnType)}";

            return $"{functionNamespace}.{methodName}{paramStr}{returnStr}";
        }

        public override string ToString()
        {
            return $"ExternBuiltinOperatorSymbol: {RoslynSymbol}";
        }
    }
}
