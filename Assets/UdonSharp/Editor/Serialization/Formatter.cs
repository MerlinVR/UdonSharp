using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Serialization
{
    /// <summary>
    /// Handles class serialization where there are multiple unknown fields that need to be serialized.
    /// Instead of how serializers key off the root storage type using type metadata, Formatters try to extract the type data from their target object to serialize
    /// This handles inheritance which serializers cannot handle on their own
    /// </summary>
    public interface IFormatter
    {
        void Write(IValueStorage targetObject, object sourceObject);
        void Read(ref object targetObject, IValueStorage sourceObject);
    }

    public abstract class Formatter<T> : IFormatter
    {
        public abstract void Read(ref T targetObject, IValueStorage sourceObject);

        public abstract void Write(IValueStorage targetObject, T sourceObject);

        public void Read(ref object targetObject, IValueStorage sourceObject)
        {
            T targetT = (T)targetObject;
            Read(ref targetT, sourceObject);
            targetObject = targetT;
        }

        public void Write(IValueStorage targetObject, object sourceObject)
        {
            Write(targetObject, (T)sourceObject);
        }
    }
}
