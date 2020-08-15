using System;
using UnityEngine;

namespace UdonSharp.Serialization
{
    public class ArraySerializer<T> : Serializer<T[]>
    {
        private Serializer<T> elementSerializer;
        private IValueStorage elementValueStorage;

        public ArraySerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
            if (typeMetadata != null)
            {
                if (typeMetadata.arrayElementMetadata == null)
                    throw new ArgumentException("Array element metadata cannot be null on array type metadata");

                elementSerializer = (Serializer<T>)CreatePooled(typeMetadata.arrayElementMetadata);

                // If using the default serializer, we can just copy the array without iterating through each element.
                if (elementSerializer is DefaultSerializer<T>) 
                {
                    elementSerializer = null;
                }
                else
                {
                    elementValueStorage = ValueStorageUtil.CreateStorage(elementSerializer.GetUdonStorageType());
                }
            }
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return typeMetadata.cSharpType.IsArray && !typeMetadata.cSharpType.GetElementType().IsArray;
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return (Serializer)System.Activator.CreateInstance(typeof(ArraySerializer<>).MakeGenericType(typeMetadata.cSharpType.GetElementType()), typeMetadata);
        }

        public override void Write(IValueStorage targetObject, in T[] sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                targetObject.Value = null;
                return;
            }

            Array targetArray = (Array)targetObject.Value;

            if (targetArray == null || targetArray.Length != sourceObject.Length)
                targetObject.Value = targetArray = (Array)System.Activator.CreateInstance(GetUdonStorageType(), sourceObject.Length);

            if (elementSerializer == null)
            {
                Array.Copy(sourceObject, targetArray, targetArray.Length);
            }
            else
            {
                for (int i = 0; i < sourceObject.Length; ++i)
                {
                    elementValueStorage.Value = targetArray.GetValue(i);
                    elementSerializer.Write(elementValueStorage, in sourceObject[i]);
                    targetArray.SetValue(elementValueStorage.Value, i);
                }
            }
        }

        public override void Read(ref T[] targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject.Value == null)
            {
                targetObject = null;
                return;
            }
            
            Array sourceArray = (Array)sourceObject.Value;

            if (targetObject == null || targetObject.Length != sourceArray.Length)
            {
                targetObject = (T[])Activator.CreateInstance(typeMetadata.cSharpType, new object[] { sourceArray.Length });
            }

            if (elementSerializer == null) // This type can just be serialized simply with a direct array copy. This prevents garbage from passing all the copies through an object.
            {
                Array.Copy(sourceArray, targetObject, sourceArray.Length);
            }
            else // The elements need special handling so use the element serializer
            {
                for (int i = 0; i < sourceArray.Length; ++i)
                {
                    T elementObj = targetObject[i];
                    elementValueStorage.Value = sourceArray.GetValue(i);
                    elementSerializer.Read(ref elementObj, elementValueStorage);
                    targetObject[i] = elementObj;
                }
            }
        }

        public override Type GetUdonStorageType()
        {
            return UdonSharpUtils.UserTypeToUdonType(typeof(T[]));
        }
    }
}

