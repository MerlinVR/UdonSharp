using System;
using UnityEngine;

namespace UdonSharp.Serialization
{
    internal class DefaultSerializer<T> : Serializer<T>
    {
        public DefaultSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(T);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
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

            if (UsbSerializationContext.CollectDependencies)
                return;

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

            if (UsbSerializationContext.CollectDependencies)
                return;

            storage.Value = sourceObject;
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return (Serializer)Activator.CreateInstance(typeof(DefaultSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

