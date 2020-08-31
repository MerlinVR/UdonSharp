

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

        IValueStorage GetElementStorage(string elementKey);
    }

    public interface IValueStorage
    {
        object Value { get; set; }
        void Reset();
    }

    public abstract class ValueStorage<T> : IValueStorage
    {
        public abstract T Value { get; set; }

        object IValueStorage.Value
        {
            get => Value;
            set
            {
                try
                {
                    Value = (T)value;
                }
                catch (System.InvalidCastException)
                {
                    Value = default;

                    UnityEngine.Debug.LogWarning($"Failed to assign element in storage, could not cast from '{value.GetType()}' to '{typeof(T)}'. Assigning default value.");
                }
            }
        }

        public System.Type ValueType { get { return typeof(T); } }

        public void Reset() { Value = default; }
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
