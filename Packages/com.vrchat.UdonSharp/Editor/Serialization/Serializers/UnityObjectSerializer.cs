﻿
using System;

namespace UdonSharp.Serialization
{
    internal class UnityObjectSerializer<T> : Serializer<T> where T : UnityEngine.Object
    {
        public UnityObjectSerializer(TypeSerializationMetadata typeMetadata)
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
            return typeMetadata.cSharpType == typeof(UnityEngine.Object) || typeMetadata.cSharpType.IsSubclassOf(typeof(UnityEngine.Object));
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                UdonSharpUtils.LogError($"Field for {typeof(T)} does not exist");
                return;
            }

            if (UsbSerializationContext.CollectDependencies)
            {
                if (sourceObject.Value is UnityEngine.Object unityObject && unityObject != null)
                    UsbSerializationContext.Dependencies.Add(unityObject);
                    
                return;
            }

            IValueStorage storage = sourceObject as ValueStorage<T>;
            if (storage == null)
            {
                Type storageType = sourceObject.GetType().GetGenericArguments()[0];

                if (typeof(T).IsSubclassOf(storageType))
                {
                    storage = sourceObject;
                }
                else if (targetObject != null && targetObject.GetType().IsAssignableFrom(storageType))
                {
                    storage = sourceObject;
                }
                else if (targetObject == null && storageType.IsSubclassOf(typeof(T)))
                {
                    storage = sourceObject;
                }
                else
                {
                    UdonSharpUtils.LogError($"Type {typeof(T)} not compatible with serializer {sourceObject}");
                    return;
                }
            }

            targetObject = (T)storage.Value;
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            VerifySerializationSanity();

            if (targetObject == null)
            {
                UdonSharpUtils.LogError($"Field for {typeof(T)} does not exist");
                return;
            }

            if (UsbSerializationContext.CollectDependencies)
            {
                if (sourceObject != null)
                    UsbSerializationContext.Dependencies.Add(sourceObject);
                
                return;
            }

            IValueStorage storage = targetObject as ValueStorage<T>;
            if (storage == null)
            {
                Type storageType = targetObject.GetType().GetGenericArguments()[0];
                if (typeof(T).IsSubclassOf(storageType))
                {
                    storage = targetObject;
                }
                else if (sourceObject != null && storageType.IsInstanceOfType(sourceObject))
                {
                    storage = targetObject;
                }
                else if (sourceObject == null && storageType.IsSubclassOf(typeof(T)))
                {
                    storage = targetObject;
                }
                else
                {
                    UdonSharpUtils.LogError($"Type {typeof(T)} not compatible with serializer {targetObject}");
                    return;
                }
            }

            // This is checking for UnityEngine.Object's special "null" which is not actually null
            // If we allow it to assign the fake "null", Udon can run into issues when attempting to reference fake "null" values since they are intended to be referenced by the proxy object
            // So if the null check passes, this value is either a real null or a fake null, and we assign a real null in either case
            if (sourceObject == null) 
                storage.Value = null;
            else
                storage.Value = sourceObject;
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return (Serializer)Activator.CreateInstance(typeof(UnityObjectSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

