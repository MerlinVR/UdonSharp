
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace UdonSharp.Serialization
{
    internal class SystemObjectSerializer : Serializer<object>
    {
        private static ConcurrentDictionary<Type, ConcurrentStack<IValueStorage>> _objectValueStorageStack =
            new ConcurrentDictionary<Type, ConcurrentStack<IValueStorage>>();

        public SystemObjectSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(object);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return typeMetadata.cSharpType == typeof(object);
        }

        public override void Read(ref object targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();
            if (sourceObject == null)
            {
                Debug.LogError($"Field for {typeof(object)} does not exist");
                return;
            }

            ValueStorage<object> storage = sourceObject as ValueStorage<object>;
            if (storage == null)
            {
                Debug.LogError($"Type {typeof(object)} not compatible with serializer {sourceObject}");
                return;
            }
            
            if (UsbSerializationContext.CollectDependencies)
                return;

            if (sourceObject.Value == null || 
                (sourceObject.Value is UnityEngine.Object unityObject && unityObject == null))
            {
                targetObject = null;
                return;
            }

            Serializer serializer = CreatePooled(sourceObject.Value.GetType());
            Type valueStorageType = serializer.GetUdonStorageType();
            ConcurrentStack<IValueStorage> varStorageStack = _objectValueStorageStack.GetOrAdd(valueStorageType,(type) => new ConcurrentStack<IValueStorage>());

            if (!varStorageStack.TryPop(out var valueStorage))
                valueStorage = (IValueStorage)Activator.CreateInstance(typeof(SimpleValueStorage<>).MakeGenericType(valueStorageType), sourceObject.Value);

            serializer.ReadWeak(ref targetObject, valueStorage);

            varStorageStack.Push(valueStorage);
        }

        public override void Write(IValueStorage targetObject, in object sourceObject)
        {
            VerifySerializationSanity();
            if (targetObject == null)
            {
                Debug.LogError($"Field for {typeof(object)} does not exist");
                return;
            }

            ValueStorage<object> storage = targetObject as ValueStorage<object>;
            if (storage == null)
            {
                Debug.LogError($"Type {typeof(object)} not compatible with serializer {targetObject}");
                return;
            }
            
            if (UsbSerializationContext.CollectDependencies)
                return;

            if (sourceObject == null ||
                (sourceObject is UnityEngine.Object unityObject && unityObject == null))
            {
                targetObject.Value = null;
                return;
            }
            
            Serializer serializer = CreatePooled(targetObject.Value.GetType());
            Type valueStorageType = serializer.GetUdonStorageType();
            ConcurrentStack<IValueStorage> varStorageStack = _objectValueStorageStack.GetOrAdd(valueStorageType,(type) => new ConcurrentStack<IValueStorage>());

            if (!varStorageStack.TryPop(out var valueStorage))
                valueStorage = (IValueStorage)Activator.CreateInstance(typeof(SimpleValueStorage<>).MakeGenericType(valueStorageType), targetObject.Value);

            serializer.WriteWeak(valueStorage, sourceObject);

            targetObject.Value = valueStorage.Value;

            varStorageStack.Push(valueStorage);
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return new SystemObjectSerializer(typeMetadata);
        }
    }
}

