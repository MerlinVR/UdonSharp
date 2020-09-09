using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace UdonSharp.Serialization
{
    public class JaggedArraySerializer<T> : Serializer<T>
    {
        private Serializer rootArraySerializer;

        private Stack<IValueStorage> innerValueStorages = new Stack<IValueStorage>();

        public JaggedArraySerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
            if (typeMetadata != null)
            {
                if (!typeMetadata.cSharpType.GetElementType().IsArray)
                    throw new SerializationException($"Cannot convert {typeMetadata.udonStorageType} to {typeMetadata.cSharpType}");

                if (typeMetadata.arrayElementMetadata == null)
                    throw new ArgumentException("Array element metadata cannot be null on array type metadata");

                rootArraySerializer = CreatePooled(new TypeSerializationMetadata(typeMetadata.arrayElementMetadata.cSharpType.MakeArrayType()) { arrayElementMetadata = typeMetadata.arrayElementMetadata });
                
                int arrayDepth = 0;

                System.Type arrayType = typeMetadata.cSharpType;
                while (arrayType.IsArray)
                {
                    arrayDepth++;
                    arrayType = arrayType.GetElementType();
                }

                if (arrayDepth <= 1)
                    throw new SerializationException("Jagged array serializer must run on jagged arrays.");
            }
        }

        IValueStorage GetInnerValueStorage()
        {
            if (innerValueStorages.Count > 0)
                return innerValueStorages.Pop();

            return ValueStorageUtil.CreateStorage(rootArraySerializer.GetUdonStorageType());
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return typeMetadata.cSharpType.IsArray && typeMetadata.cSharpType.GetElementType().IsArray;
        }

        void ConvertToCSharpArrayElement(ref object targetElement, object elementValue, System.Type cSharpType)
        {
            if (elementValue == null)
            {
                targetElement = null;
                return;
            }

            if (UdonSharpUtils.IsUserJaggedArray(cSharpType))
            {
                Array targetArray = (Array)targetElement;
                Array sourceArray = (Array)elementValue;

                if (targetArray == null || targetArray.Length != sourceArray.Length)
                    targetElement = targetArray = (Array)Activator.CreateInstance(cSharpType, new object[] { sourceArray.Length });

                for (int i = 0; i < sourceArray.Length; ++i)
                {
                    object elementVal = targetArray.GetValue(i);
                    ConvertToCSharpArrayElement(ref elementVal, sourceArray.GetValue(i), cSharpType.GetElementType());
                    targetArray.SetValue(elementVal, i);
                }
            }
            else if (cSharpType.IsArray)
            {
                IValueStorage innerArrayValueStorage = GetInnerValueStorage();
                innerArrayValueStorage.Value = elementValue;
                rootArraySerializer.ReadWeak(ref targetElement, innerArrayValueStorage);

                innerValueStorages.Push(innerArrayValueStorage);
            }
            else
            {
                throw new Exception("Jagged array serializer requires a root array serializer");
            }
        }

        void ConvertToUdonArrayElement(ref object targetElement, object elementValue, System.Type cSharpType)
        {
            if (elementValue == null)
            {
                targetElement = null;
                return;
            }

            if (UdonSharpUtils.IsUserJaggedArray(cSharpType))
            {
                Array targetArray = (Array)targetElement;
                Array sourceArray = (Array)elementValue;

                if (targetArray == null || targetArray.Length != sourceArray.Length)
                    targetElement = targetArray = (Array)Activator.CreateInstance(UdonSharpUtils.UserTypeToUdonType(cSharpType), new object[] { sourceArray.Length });

                for (int i = 0; i < sourceArray.Length; ++i)
                {
                    object elementVal = targetArray.GetValue(i);
                    ConvertToUdonArrayElement(ref elementVal, sourceArray.GetValue(i), cSharpType.GetElementType());
                    targetArray.SetValue(elementVal, i);
                }
            }
            else if (cSharpType.IsArray)
            {
                IValueStorage innerArrayValueStorage = GetInnerValueStorage();

                innerArrayValueStorage.Value = targetElement;
                rootArraySerializer.WriteWeak(innerArrayValueStorage, elementValue);
                targetElement = innerArrayValueStorage.Value;

                innerValueStorages.Push(innerArrayValueStorage);
            }
            else
            {
                throw new Exception("Jagged array serializer requires a root array serializer");
            }
        }

        public override void ReadWeak(ref object targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            //if (sourceObject != null && !UdonSharpUtils.IsUserJaggedArray(sourceObject.GetType()))
            //    throw new SerializationException($"Cannot convert {targetObject.GetType()} to {typeMetadata.cSharpType}");

            ConvertToCSharpArrayElement(ref targetObject, sourceObject.Value, typeMetadata.cSharpType);
        }

        public override void WriteWeak(IValueStorage targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject != null && !UdonSharpUtils.IsUserJaggedArray(sourceObject.GetType()))
                throw new SerializationException($"Cannot convert {targetObject.GetType()} to {typeMetadata.cSharpType}");

            object tarArray = targetObject.Value;
            ConvertToUdonArrayElement(ref tarArray, sourceObject, typeMetadata.cSharpType);
            targetObject.Value = tarArray;
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            WriteWeak(targetObject, sourceObject);
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            object target = targetObject;
            ReadWeak(ref target, sourceObject);
            targetObject = (T)target;
        }

        public override Type GetUdonStorageType()
        {
            return typeMetadata.udonStorageType;
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            return (Serializer)System.Activator.CreateInstance(typeof(JaggedArraySerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

