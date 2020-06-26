
using System;

namespace UdonSharp.Serialization
{
    public interface IHeapStorage
    {
        /// <summary>
        /// Sets an element value in the storage interface, the storage interface must be a collection interface
        /// </summary>
        /// <param name="elementKey"></param>
        /// <param name="value"></param>
        void SetElementValueWeak(string elementKey, object value);

        /// <summary>
        /// Get an element value 
        /// </summary>
        /// <param name="elementKey"></param>
        /// <returns></returns>
        object GetElementValueWeak(string elementKey);

        T GetElementValue<T>(string elementKey);

        void SetElementValue<T>(string elementKey, T value);

        /// <summary>
        /// Marks all the IStorageElementRef's created as dirty because the format of the underlying storage has potentially changed from a compile or something else.
        /// </summary>
        void InvalidateInterface();

        IValueStorage GetElementStorage(string elementKey);
    }

    public interface IValueStorage
    {
        object Value { get; set; }

        /// <summary>
        /// Invalidates the underlying value storage
        /// If this is a complex storage type that references a heap that may have been modified, this allows it to update to the changes in the heap
        /// </summary>
        void InvalidateStorage();
    }

    public abstract class ValueStorage<T> : IValueStorage
    {
        public abstract T Value { get; set; }

        object IValueStorage.Value { get { return Value; } set => Value = (T)value; }

        public System.Type ValueType { get { return typeof(T); } }

        public abstract void InvalidateStorage();
    }

    /// <summary>
    /// This variant of ValueStorage acts like StrongBox<T> more or less
    /// More complex implementations of ValueStorage<T> can do things like reference an element in an IHeapStorageInterface
    /// </summary>
    public class SimpleValueStorage<T> : ValueStorage<T>
    {
        T _value;
        public override T Value { get => _value; set => _value = value; }

        public SimpleValueStorage()
        {
            _value = default;
        }

        public SimpleValueStorage(T value)
        {
            _value = value;
        }

        public override void InvalidateStorage() { }
    }

    public static class ValueStorageUtil
    {
        public static IValueStorage CreateStorage(System.Type storageType)
        {
            return (IValueStorage)System.Activator.CreateInstance(typeof(SimpleValueStorage<>).MakeGenericType(storageType));
        }

        public static IValueStorage CreateStorage(System.Type storageType, object value)
        {
            return (IValueStorage)System.Activator.CreateInstance(typeof(SimpleValueStorage<>).MakeGenericType(storageType), value);
        }

        public static IValueStorage CreateStorage<T>(T value)
        {
            ValueStorage<T> valueStorage = (ValueStorage<T>)System.Activator.CreateInstance(typeof(SimpleValueStorage<T>));
            valueStorage.Value = value;
            return valueStorage;
        }
    }
}
