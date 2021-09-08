
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal static class ConstantExpressionOptimizer
    {
        private static class DynamicInvoke
        {
            public static dynamic Add(dynamic a, dynamic b) => a + b;
            public static dynamic Sub(dynamic a, dynamic b) => a - b;
            public static dynamic Mul(dynamic a, dynamic b) => a * b;
            public static dynamic Div(dynamic a, dynamic b) => a / b;
            public static dynamic Mod(dynamic a, dynamic b) => a % b;
            public static dynamic LSh(dynamic a, dynamic b) => a << b;
            public static dynamic RSh(dynamic a, dynamic b) => a >> b;
            public static dynamic Xor(dynamic a, dynamic b) => a ^ b;
            public static dynamic BitwiseAnd(dynamic a, dynamic b) => a & b;
            public static dynamic BitwiseOr(dynamic a, dynamic b) => a | b;
            public static dynamic GreaterThan(dynamic a, dynamic b) => a > b;
            public static dynamic GreaterThanOrEquals(dynamic a, dynamic b) => a >= b;
            public static dynamic LessThan(dynamic a, dynamic b) => a < b;
            public static dynamic LessThanOrEquals(dynamic a, dynamic b) => a <= b;
            public static dynamic Equal(dynamic a, dynamic b) => a == b;
            public static dynamic NotEqual(dynamic a, dynamic b) => a != b;
            public static dynamic UnaryNegate(dynamic a) => -a;
            public static dynamic UnaryNot(dynamic a) => !a;
            public static dynamic BitwiseNot(dynamic a) => ~a;

            // public static T ConvertToT<T>(dynamic val)
            // {
            //     return (T) val;
            // }

            public static int ConvertToInt(dynamic input) => (int) input;
            public static uint ConvertToUInt(dynamic input) => (uint) input;
            public static byte ConvertToByte(dynamic input) => (byte) input;
            public static sbyte ConvertToSByte(dynamic input) => (sbyte) input;
            public static short ConvertToShort(dynamic input) => (short) input;
            public static ushort ConvertToUShort(dynamic input) => (ushort) input;
            public static long ConvertToLong(dynamic input) => (long) input;
            public static ulong ConvertToULong(dynamic input) => (ulong) input;
            public static float ConvertToFloat(dynamic input) => (float) input;
            public static double ConvertToDouble(dynamic input) => (double) input;
            public static decimal ConvertToDecimal(dynamic input) => (decimal) input;
            public static char ConvertToChar(dynamic input) => (char) input;
        }
        
        public static BoundConstantExpression FoldConstantBinaryExpression(
            BindContext context,
            BinaryExpressionSyntax syntax,
            MethodSymbol binaryOperator,
            BoundExpression lhs, BoundExpression rhs)
        {
            // Value type + null comparison which will always be false for == and true for !=
            // This folding is needed for null comparisons in generics to work as expected
            if ((lhs.ValueType.IsValueType && rhs.IsConstant && rhs.ConstantValue.Value == null) ||
                (rhs.ValueType.IsValueType && lhs.IsConstant && lhs.ConstantValue.Value == null))
            {
                if (syntax.OperatorToken.Kind() == SyntaxKind.EqualsEqualsToken)
                    return new BoundConstantExpression(new ConstantValue<bool>(false),
                        context.GetTypeSymbol(typeof(bool)), syntax);
                
                if (syntax.OperatorToken.Kind() == SyntaxKind.ExclamationEqualsToken)
                    return new BoundConstantExpression(new ConstantValue<bool>(true),
                        context.GetTypeSymbol(typeof(bool)), syntax);
            }
            
            if (!lhs.IsConstant || !rhs.IsConstant)
                return null;

            if (binaryOperator == null || (binaryOperator.IsOperator && binaryOperator.IsExtern))
            {
                object foldedValue;
                object lhsValue = lhs.ConstantValue.Value;
                object rhsValue = rhs.ConstantValue.Value;
                
                switch (syntax.OperatorToken.Kind())
                {
                    case SyntaxKind.PlusToken:
                        foldedValue = DynamicInvoke.Add(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.MinusToken:
                        foldedValue = DynamicInvoke.Sub(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.AsteriskToken:
                        foldedValue = DynamicInvoke.Mul(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.SlashToken:
                        foldedValue = DynamicInvoke.Div(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.PercentToken:
                        foldedValue = DynamicInvoke.Mod(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.LessThanLessThanToken:
                        foldedValue = DynamicInvoke.LSh(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.GreaterThanGreaterThanToken:
                        foldedValue = DynamicInvoke.RSh(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.CaretToken:
                        foldedValue = DynamicInvoke.Xor(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.AmpersandToken:
                    case SyntaxKind.AmpersandAmpersandToken: // When we're dealing with constants short circuiting shouldn't matter
                        foldedValue = DynamicInvoke.BitwiseAnd(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.BarToken: 
                    case SyntaxKind.BarBarToken:
                        foldedValue = DynamicInvoke.BitwiseOr(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.GreaterThanToken:
                        foldedValue = DynamicInvoke.GreaterThan(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.GreaterThanEqualsToken:
                        foldedValue = DynamicInvoke.GreaterThanOrEquals(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.LessThanToken:
                        foldedValue = DynamicInvoke.LessThan(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.LessThanOrEqualExpression:
                        foldedValue = DynamicInvoke.LessThanOrEquals(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.EqualsEqualsToken:
                        foldedValue = DynamicInvoke.Equal(lhsValue, rhsValue);
                        break;
                    case SyntaxKind.ExclamationEqualsToken:
                        foldedValue = DynamicInvoke.NotEqual(lhsValue, rhsValue);
                        break;
                    default:
                        return null;
                }
                
                IConstantValue constantValue = (IConstantValue)Activator.CreateInstance(typeof(ConstantValue<>).MakeGenericType(foldedValue.GetType()), foldedValue);

                return new BoundConstantExpression(constantValue, context.GetTypeSymbol(foldedValue.GetType()), syntax);
            }

            return null;
        }

        public static BoundConstantExpression FoldConstantUnaryPrefixExpression(
            BindContext context,
            PrefixUnaryExpressionSyntax syntax,
            MethodSymbol unaryOperator,
            BoundExpression operand)
        {
            if (!operand.IsConstant)
                return null;

            if (unaryOperator.IsOperator && unaryOperator.IsExtern)
            {
                object foldedValue;
                object operandValue = operand.ConstantValue.Value;
                
                switch (syntax.OperatorToken.Kind())
                {
                    case SyntaxKind.MinusToken:
                        foldedValue = DynamicInvoke.UnaryNegate(operandValue);
                        break;
                    case SyntaxKind.ExclamationToken:
                        foldedValue = DynamicInvoke.UnaryNot(operandValue);
                        break;
                    case SyntaxKind.TildeToken:
                        foldedValue = DynamicInvoke.BitwiseNot(operandValue);
                        break;
                    default:
                        return null;
                }
                
                IConstantValue constantValue = (IConstantValue)Activator.CreateInstance(typeof(ConstantValue<>).MakeGenericType(foldedValue.GetType()), foldedValue);

                return new BoundConstantExpression(constantValue, context.GetTypeSymbol(foldedValue.GetType()), syntax);
            }
            
            return null;
        }

        // Making generic methods for dynamic methods breaks like 5% of the time, thanks Mono.
        // private static readonly MethodInfo _convertToT =
        //     typeof(DynamicInvoke).GetMethod(nameof(DynamicInvoke.ConvertToT), BindingFlags.Public | BindingFlags.Static);

        private static readonly HashSet<Type> _convertableConstantTypes = new HashSet<Type>()
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(char),
            // It seems like mono doesn't like dynamic methods on non-base types. We'll disallow these for a while and see if it stops crashing.
            // typeof(Vector2),
            // typeof(Vector3),
            // typeof(Vector4),
        };

        public static bool CanDoConstantConversion(Type type)
        {
            return _convertableConstantTypes.Contains(type);
        }
        
        public static object FoldConstantConversion(Type targetType, object value)
        {
            // return _convertToT.MakeGenericMethod(targetType).Invoke(null, new [] {value});

            if (value == null)
                return null;

            if (targetType == value.GetType())
                return value;
            
            if (targetType == typeof(int))
                return DynamicInvoke.ConvertToInt(value);
            if (targetType == typeof(uint))
                return DynamicInvoke.ConvertToUInt(value);
            if (targetType == typeof(byte))
                return DynamicInvoke.ConvertToByte(value);
            if (targetType == typeof(sbyte))
                return DynamicInvoke.ConvertToSByte(value);
            if (targetType == typeof(short))
                return DynamicInvoke.ConvertToShort(value);
            if (targetType == typeof(ushort))
                return DynamicInvoke.ConvertToUShort(value);
            if (targetType == typeof(long))
                return DynamicInvoke.ConvertToLong(value);
            if (targetType == typeof(ulong))
                return DynamicInvoke.ConvertToULong(value);
            if (targetType == typeof(float))
                return DynamicInvoke.ConvertToFloat(value);
            if (targetType == typeof(double))
                return DynamicInvoke.ConvertToDouble(value);
            if (targetType == typeof(decimal))
                return DynamicInvoke.ConvertToDecimal(value);
            if (targetType == typeof(char))
                return DynamicInvoke.ConvertToChar(value);

            throw new NotImplementedException("Cannot cast to type " + targetType);
        }
    }
}
