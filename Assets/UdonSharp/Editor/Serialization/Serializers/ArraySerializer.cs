using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace UdonSharp.Serialization
{
    internal class ArraySerializer<T> : Serializer<T[]>
    {
        private Serializer<T> elementSerializer;

        private ConcurrentStack<IValueStorage> arrayStorages = new ConcurrentStack<IValueStorage>();

        public ArraySerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
            if (typeMetadata == null) 
                return;
            
            if (typeMetadata.arrayElementMetadata == null)
                throw new ArgumentException("Array element metadata cannot be null on array type metadata");

            elementSerializer = (Serializer<T>)CreatePooled(typeMetadata.arrayElementMetadata);

            // If using the default serializer, we can just copy the array without iterating through each element.
            if (elementSerializer is DefaultSerializer<T>) 
            {
                elementSerializer = null;
            }
        }

        private IValueStorage GetNextStorage()
        {
            if (arrayStorages.TryPop(out var storage))
                return storage;
            
            return ValueStorageUtil.CreateStorage(elementSerializer.GetUdonStorageType());
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return typeMetadata.cSharpType.IsArray && !typeMetadata.cSharpType.GetElementType().IsArray;
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return (Serializer)Activator.CreateInstance(typeof(ArraySerializer<>).MakeGenericType(typeMetadata.cSharpType.GetElementType()), typeMetadata);
        }

        public override void Write(IValueStorage targetObject, in T[] sourceObject)
        {
            VerifySerializationSanity();

            if (targetObject == null)
            {
                Debug.LogError($"Field of type '{typeof(T[]).Name}' does not exist any longer, compile U# scripts then allow Unity to compile assemblies to fix this"); 
                return;
            }

            if (sourceObject == null)
            {
                targetObject.Value = null;
                return;
            }

            Array targetArray = (Array)targetObject.Value;

            if (targetArray == null || targetArray.Length != sourceObject.Length)
                targetObject.Value = targetArray = (Array)Activator.CreateInstance(GetUdonStorageType(), sourceObject.Length);

            if (elementSerializer == null)
            {
                if (!UsbSerializationContext.CollectDependencies)
                    Array.Copy(sourceObject, targetArray, targetArray.Length);
            }
            else
            {
                IValueStorage elementValueStorage = GetNextStorage();

                for (int i = 0; i < sourceObject.Length; ++i)
                {
                    elementValueStorage.Value = targetArray.GetValue(i);
                    elementSerializer.Write(elementValueStorage, in sourceObject[i]);
                    
                    if (!UsbSerializationContext.CollectDependencies)
                        targetArray.SetValue(elementValueStorage.Value, i);
                }

                arrayStorages.Push(elementValueStorage);
            }
        }

        public override void Read(ref T[] targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                Debug.LogError($"Field of type '{typeof(T[]).Name}' does not exist any longer, compile U# scripts then allow Unity to compile assemblies to fix this");
                targetObject = null;
                return;
            }

            if (sourceObject.Value == null)
            {
                targetObject = null;
                return;
            }
            
            Array sourceArray = (Array)sourceObject.Value;

            if (targetObject == null || targetObject.Length != sourceArray.Length)
            {
                targetObject = (T[])Activator.CreateInstance(typeMetadata.cSharpType, sourceArray.Length);
            }

            if (elementSerializer == null) // This type can just be serialized simply with a direct array copy. This prevents garbage from passing all the copies through an object.
            {
                if (!UsbSerializationContext.CollectDependencies)
                    Array.Copy(sourceArray, targetObject, sourceArray.Length);
            }
            else // The elements need special handling so use the element serializer
            {
                IValueStorage elementValueStorage = GetNextStorage();

                for (int i = 0; i < sourceArray.Length; ++i)
                {
                    T elementObj = targetObject[i];
                    elementValueStorage.Value = sourceArray.GetValue(i);
                    elementSerializer.Read(ref elementObj, elementValueStorage);
                    
                    if (!UsbSerializationContext.CollectDependencies)
                        targetObject[i] = elementObj;
                }

                arrayStorages.Push(elementValueStorage);
            }
        }

        public override Type GetUdonStorageType()
        {
            return UdonSharpUtils.UserTypeToUdonType(typeof(T[]));
        }
    }
}

