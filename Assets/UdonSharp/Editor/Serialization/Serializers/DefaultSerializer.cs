using System;
using UnityEngine;

namespace UdonSharp.Serialization
{
    public class DefaultSerializer<T> : Serializer<T>
    {
        public DefaultSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(T);
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return true;
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                Debug.LogError($"Field for {typeof(T)} does not exist");
                return;
            }

            ValueStorage<T> storage = sourceObject as ValueStorage<T>;
            if (storage == null)
            {
                Debug.LogError($"Type {typeof(T)} not compatible with serializer {sourceObject}");
                return;
            }

            targetObject = storage.Value;
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            VerifySerializationSanity();
            if (targetObject == null)
            {
                Debug.LogError($"Field for {typeof(T)} does not exist");
                return;
            }

            ValueStorage<T> storage = targetObject as ValueStorage<T>;
            if (storage == null)
            {
                Debug.LogError($"Type {typeof(T)} not compatible with serializer {targetObject}");
                return;
            }

            storage.Value = sourceObject;
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return (Serializer)System.Activator.CreateInstance(typeof(DefaultSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

