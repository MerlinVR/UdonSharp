using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace UdonSharp
{
    public class OperatorParameterInfo : ParameterInfo
    {
        public override string Name { get { return NameImpl; } }

        public override bool HasDefaultValue { get { return false; } }

        public OperatorParameterInfo(System.Type type)
        {
            ClassImpl = type;
            NameImpl = type.Name;
        }
    }

    public enum BuiltinOperatorType
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
        ConditionalXor, // This doesn't exist in C#?
        // -- Bitwise operators
        LogicalAnd, 
        LogicalOr, 
        LogicalXor,
        BitwiseNot, // Now has hack workaround handling
    }

    /// <summary>
    /// Used to represent builtin operations on base types that aren't represented by a method in CIL and just have a corresponding instruction.
    /// Example: The addition operator on base types is an add instruction, this happens with most basic arithmetic ops
    /// Udon exports generated functions for all of the operators on base types, for instance "SystemSingle.__op_Addition__SystemSingle_SystemSingle__SystemSingle" for float addition
    /// </summary>
    public class OperatorMethodInfo : MethodInfo
    {
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();

        // Treat these methods like they are static since that's the calling convention Udon uses for them
        // Assignment variants of these operators will be handled at a higher level
        public override MethodAttributes Attributes { get { return MethodAttributes.Static; } }

        public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();

        public override Type DeclaringType { get { return operatorSourceType; } }

        public override string Name
        {
            get
            {
                return $"op_{Enum.GetName(typeof(BuiltinOperatorType), operatorType)}";
            }
        }

        public override Type ReturnType
        {
            get
            {
                if (operatorType == BuiltinOperatorType.ConditionalAnd ||
                    operatorType == BuiltinOperatorType.ConditionalOr ||
                    operatorType == BuiltinOperatorType.GreaterThan ||
                    operatorType == BuiltinOperatorType.GreaterThanOrEqual ||
                    operatorType == BuiltinOperatorType.LessThan ||
                    operatorType == BuiltinOperatorType.LessThanOrEqual ||
                    operatorType == BuiltinOperatorType.Equality ||
                    operatorType == BuiltinOperatorType.Inequality)
                    return typeof(bool);

                
                if (operatorSourceType == typeof(byte) ||
                    operatorSourceType == typeof(sbyte) ||
                    operatorSourceType == typeof(char) ||
                    operatorSourceType == typeof(short) ||
                    operatorSourceType == typeof(ushort))
                {
                    return typeof(int);
                }

                return operatorSourceType;
            }
        }

        public override Type ReflectedType { get { return operatorSourceType; } }

        public Type operatorSourceType { get; private set; }
        public BuiltinOperatorType operatorType { get; private set; }

        public OperatorMethodInfo(Type type, BuiltinOperatorType operatorTypeIn)
        {
            operatorSourceType = type;
            operatorType = operatorTypeIn;
        }

        public override MethodInfo GetBaseDefinition()
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return null;
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return null;
        }

        public override string ToString()
        {
            return $"UdonSharp Operator {ReturnType} {Name}({string.Join(", ", GetParameters().Select(e => e.Name))})";
        }

        public override bool Equals(object obj)
        {
            OperatorMethodInfo other = obj as OperatorMethodInfo;
            if (other == null)
                return false;

            return operatorSourceType == other.operatorSourceType && operatorType == other.operatorType;
        }

        public override int GetHashCode()
        {
            return operatorSourceType.GetHashCode() + operatorType.GetHashCode();
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return MethodImplAttributes.Runtime;
        }

        public override ParameterInfo[] GetParameters()
        {
            if (operatorType == BuiltinOperatorType.UnaryMinus || 
                operatorType == BuiltinOperatorType.UnaryNegation)
            {
                return new ParameterInfo[] { new OperatorParameterInfo(operatorSourceType) };
            }
            else
            {
                if (operatorType == BuiltinOperatorType.LeftShift || operatorType == BuiltinOperatorType.RightShift)
                {
                    if (operatorSourceType == typeof(uint))
                        return new ParameterInfo[] { new OperatorParameterInfo(operatorSourceType), new OperatorParameterInfo(typeof(int)) };
                    else if (operatorSourceType == typeof(ulong))
                        return new ParameterInfo[] { new OperatorParameterInfo(operatorSourceType), new OperatorParameterInfo(typeof(int)) };
                    else if (operatorSourceType == typeof(long))
                        return new ParameterInfo[] { new OperatorParameterInfo(operatorSourceType), new OperatorParameterInfo(typeof(int)) };
                }

                return new ParameterInfo[] { new OperatorParameterInfo(operatorSourceType), new OperatorParameterInfo(operatorSourceType) };
            }
        }

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotImplementedException("This is a stub, invoke is not needed unless we want to do constant folding and such ourselves.");
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }
    }
}
