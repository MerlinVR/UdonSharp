
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
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
            
            [UsedImplicitly] public static byte CastToByte(byte source) => source;
            [UsedImplicitly] public static sbyte CastToSByte(byte source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(byte source) => source;
            [UsedImplicitly] public static ushort CastToUInt16(byte source) => source;
            [UsedImplicitly] public static int CastToInt32(byte source) => source;
            [UsedImplicitly] public static uint CastToUInt32(byte source) => source;
            [UsedImplicitly] public static long CastToInt64(byte source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(byte source) => source;
            [UsedImplicitly] public static float CastToSingle(byte source) => source;
            [UsedImplicitly] public static double CastToDouble(byte source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(byte source) => source;
            [UsedImplicitly] public static char CastToChar(byte source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(sbyte source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(sbyte source) => source;
            [UsedImplicitly] public static short CastToInt16(sbyte source) => source;
            [UsedImplicitly] public static ushort CastToUInt16(sbyte source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(sbyte source) => source;
            [UsedImplicitly] public static uint CastToUInt32(sbyte source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(sbyte source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(sbyte source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(sbyte source) => source;
            [UsedImplicitly] public static double CastToDouble(sbyte source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(sbyte source) => source;
            [UsedImplicitly] public static char CastToChar(sbyte source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(short source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(short source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(short source) => source;
            [UsedImplicitly] public static ushort CastToUInt16(short source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(short source) => source;
            [UsedImplicitly] public static uint CastToUInt32(short source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(short source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(short source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(short source) => source;
            [UsedImplicitly] public static double CastToDouble(short source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(short source) => source;
            [UsedImplicitly] public static char CastToChar(short source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(ushort source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(ushort source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(ushort source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(ushort source) => source;
            [UsedImplicitly] public static int CastToInt32(ushort source) => source;
            [UsedImplicitly] public static uint CastToUInt32(ushort source) => source;
            [UsedImplicitly] public static long CastToInt64(ushort source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(ushort source) => source;
            [UsedImplicitly] public static float CastToSingle(ushort source) => source;
            [UsedImplicitly] public static double CastToDouble(ushort source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(ushort source) => source;
            [UsedImplicitly] public static char CastToChar(ushort source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(int source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(int source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(int source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(int source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(int source) => source;
            [UsedImplicitly] public static uint CastToUInt32(int source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(int source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(int source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(int source) => source;
            [UsedImplicitly] public static double CastToDouble(int source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(int source) => source;
            [UsedImplicitly] public static char CastToChar(int source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(uint source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(uint source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(uint source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(uint source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(uint source) => (int)source;
            [UsedImplicitly] public static uint CastToUInt32(uint source) => source;
            [UsedImplicitly] public static long CastToInt64(uint source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(uint source) => source;
            [UsedImplicitly] public static float CastToSingle(uint source) => source;
            [UsedImplicitly] public static double CastToDouble(uint source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(uint source) => source;
            [UsedImplicitly] public static char CastToChar(uint source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(long source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(long source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(long source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(long source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(long source) => (int)source;
            [UsedImplicitly] public static uint CastToUInt32(long source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(long source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(long source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(long source) => source;
            [UsedImplicitly] public static double CastToDouble(long source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(long source) => source;
            [UsedImplicitly] public static char CastToChar(long source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(ulong source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(ulong source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(ulong source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(ulong source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(ulong source) => (int)source;
            [UsedImplicitly] public static uint CastToUInt32(ulong source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(ulong source) => (long)source;
            [UsedImplicitly] public static ulong CastToUInt64(ulong source) => source;
            [UsedImplicitly] public static float CastToSingle(ulong source) => source;
            [UsedImplicitly] public static double CastToDouble(ulong source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(ulong source) => source;
            [UsedImplicitly] public static char CastToChar(ulong source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(float source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(float source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(float source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(float source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(float source) => (int)source;
            [UsedImplicitly] public static uint CastToUInt32(float source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(float source) => (long)source;
            [UsedImplicitly] public static ulong CastToUInt64(float source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(float source) => source;
            [UsedImplicitly] public static double CastToDouble(float source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(float source) => (decimal)source;
            [UsedImplicitly] public static char CastToChar(float source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(double source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(double source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(double source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(double source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(double source) => (int)source;
            [UsedImplicitly] public static uint CastToUInt32(double source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(double source) => (long)source;
            [UsedImplicitly] public static ulong CastToUInt64(double source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(double source) => (float)source;
            [UsedImplicitly] public static double CastToDouble(double source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(double source) => (decimal)source;
            [UsedImplicitly] public static char CastToChar(double source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(decimal source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(decimal source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(decimal source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(decimal source) => (ushort)source;
            [UsedImplicitly] public static int CastToInt32(decimal source) => (int)source;
            [UsedImplicitly] public static uint CastToUInt32(decimal source) => (uint)source;
            [UsedImplicitly] public static long CastToInt64(decimal source) => (long)source;
            [UsedImplicitly] public static ulong CastToUInt64(decimal source) => (ulong)source;
            [UsedImplicitly] public static float CastToSingle(decimal source) => (float)source;
            [UsedImplicitly] public static double CastToDouble(decimal source) => (double)source;
            [UsedImplicitly] public static decimal CastToDecimal(decimal source) => source;
            [UsedImplicitly] public static char CastToChar(decimal source) => (char)source;
            [UsedImplicitly] public static byte CastToByte(char source) => (byte)source;
            [UsedImplicitly] public static sbyte CastToSByte(char source) => (sbyte)source;
            [UsedImplicitly] public static short CastToInt16(char source) => (short)source;
            [UsedImplicitly] public static ushort CastToUInt16(char source) => source;
            [UsedImplicitly] public static int CastToInt32(char source) => source;
            [UsedImplicitly] public static uint CastToUInt32(char source) => source;
            [UsedImplicitly] public static long CastToInt64(char source) => source;
            [UsedImplicitly] public static ulong CastToUInt64(char source) => source;
            [UsedImplicitly] public static float CastToSingle(char source) => source;
            [UsedImplicitly] public static double CastToDouble(char source) => source;
            [UsedImplicitly] public static decimal CastToDecimal(char source) => source;
            [UsedImplicitly] public static char CastToChar(char source) => source;
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

        private static readonly HashSet<Type> _convertableConstantTypes = new HashSet<Type>
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
            // todo: add to generated cast method allowed types
            // typeof(Vector2),
            // typeof(Vector3),
            // typeof(Vector4),
        };

        public static bool CanDoConstantConversion(Type type)
        {
            return _convertableConstantTypes.Contains(type);
        }

        private static Dictionary<(Type, Type), MethodInfo> _typeFoldLookup;
        
        private static Dictionary<(Type, Type), MethodInfo> GetFoldingLookup()
        {
            if (_typeFoldLookup != null)
                return _typeFoldLookup;
            
            Dictionary<(Type, Type), MethodInfo> newLookup = new Dictionary<(Type, Type), MethodInfo>();

            foreach (Type sourceType in _convertableConstantTypes)
            {
                foreach (Type targetType in _convertableConstantTypes)
                {
                    string methodName = $"CastTo{targetType.Name}";
                    MethodInfo castMethod = typeof(DynamicInvoke).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .First(m => m.Name == methodName && m.GetParameters()[0].ParameterType == sourceType);
                    
                    newLookup.Add((sourceType, targetType), castMethod);
                }
            }

            _typeFoldLookup = newLookup;

            return _typeFoldLookup;
        }
        
        public static object FoldConstantConversion(Type targetType, object value)
        {
            if (value == null)
                return null;

            if (targetType == value.GetType())
                return value;

            if (value.GetType().IsEnum && UdonSharpUtils.IsIntegerType(targetType))
            {
                MethodInfo enumToInt = typeof(Convert).GetMethods().First(e =>
                    e.Name == $"To{targetType.Name}" && e.GetParameters()[0].ParameterType == typeof(object));

                return enumToInt.Invoke(null, new [] { value });
            }

            GetFoldingLookup().TryGetValue((value.GetType(), targetType), out var castMethod);

            if (castMethod != null)
                return castMethod.Invoke(null, new [] {value});
            
            throw new NotImplementedException("Cannot cast to type " + targetType);
        }
    }
}
