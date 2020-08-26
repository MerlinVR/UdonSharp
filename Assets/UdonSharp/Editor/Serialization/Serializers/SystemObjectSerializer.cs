using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UdonSharp.Serialization
{
    public class SystemObjectSerializer : Serializer<object>
    {
        static Dictionary<System.Type, Stack<IValueStorage>> objectValueStorageStack = new Dictionary<Type, Stack<IValueStorage>>();

        public SystemObjectSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(object);
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
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

            if (sourceObject.Value == null || 
                (sourceObject.Value is UnityEngine.Object unityObject && unityObject == null))
            {
                targetObject = null;
                return;
            }

            Serializer serializer = Serializer.CreatePooled(sourceObject.Value.GetType());
            System.Type valueStorageType = serializer.GetUdonStorageType();
            Stack<IValueStorage> varStorageStack;
            if (!objectValueStorageStack.TryGetValue(valueStorageType, out varStorageStack))
            {
                varStorageStack = new Stack<IValueStorage>();
                objectValueStorageStack.Add(valueStorageType, varStorageStack);
            }

            IValueStorage valueStorage;
            if (varStorageStack.Count > 0)
            {
                valueStorage = varStorageStack.Pop();
                valueStorage.Value = sourceObject.Value;
            }
            else
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

            if (sourceObject == null ||
                (sourceObject is UnityEngine.Object unityObject && unityObject == null))
            {
                targetObject.Value = null;
                return;
            }
            
            Serializer serializer = Serializer.CreatePooled(sourceObject.GetType());
            System.Type valueStorageType = serializer.GetUdonStorageType();
            Stack<IValueStorage> varStorageStack;
            if (!objectValueStorageStack.TryGetValue(valueStorageType, out varStorageStack))
            {
                varStorageStack = new Stack<IValueStorage>();
                objectValueStorageStack.Add(valueStorageType, varStorageStack);
            }

            IValueStorage valueStorage;
            if (varStorageStack.Count > 0)
            {
                valueStorage = varStorageStack.Pop();
                valueStorage.Reset();
            }
            else
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

