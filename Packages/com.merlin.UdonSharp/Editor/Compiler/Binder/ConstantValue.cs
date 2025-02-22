using System;

namespace UdonSharp.Compiler.Binder
{
    internal interface IConstantValue
    {
        object Value { get; }
        Type ValueType { get; }
    }

    internal class ConstantValue<T> : IConstantValue
    {
        public T Value { get; }

        object IConstantValue.Value => Value;
        public Type ValueType => typeof(T);

        public ConstantValue(T value)
        {
            Value = value;
        }

        public override bool Equals(object obj)
        {
            if (obj is ConstantValue<T> other)
            {
                return Value.Equals(other.Value);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
