using System;
using System.Runtime.Serialization;

namespace UdonSharp.Serialization
{
    public class JaggedArraySerializer : Serializer
    {
        private Serializer rootArraySerializer;

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
                rootArraySerializer.SerializeToCSharpTypeInPlace(ref targetElement, elementValue);
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
                rootArraySerializer.SerializeToCSharpTypeInPlace(ref targetElement, elementValue);
            }
            else
            {
                throw new Exception("Jagged array serializer requires a root array serializer");
            }
        }

        public override void SerializeToUdonTypeInPlace(ref object targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject != null && !UdonSharpUtils.IsUserJaggedArray(sourceObject.GetType()))
                throw new SerializationException($"Cannot convert {targetObject.GetType()} to {typeMetadata.cSharpType}");

            ConvertToUdonArrayElement(ref targetObject, sourceObject, typeMetadata.cSharpType);
        }

        public override void SerializeToCSharpTypeInPlace(ref object targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject != null && !UdonSharpUtils.IsUserJaggedArray(sourceObject.GetType()))
                throw new SerializationException($"Cannot convert {targetObject.GetType()} to {typeMetadata.cSharpType}");

            ConvertToCSharpArrayElement(ref targetObject, sourceObject, typeMetadata.cSharpType);
        }
    }
}

