using System;

namespace UdonSharp.Serialization
{
    public class ArraySerializer : Serializer
    {
        private Serializer elementSerializer;

        public ArraySerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
            if (typeMetadata.arrayElementMetadata == null)
                throw new ArgumentException("Array element metadata cannot be null on array type metadata");

            if (!UdonSharpUtils.IsUserDefinedType(typeMetadata.arrayElementMetadata.cSharpType))
                elementSerializer = CreatePooled(typeMetadata.arrayElementMetadata);
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return typeMetadata.cSharpType.IsArray && !typeMetadata.cSharpType.GetElementType().IsArray;
        }

        public override void SerializeToUdonTypeInPlace(ref object targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                targetObject = null;
                return;
            }

            Array targetArray = (Array)targetObject;
            Array sourceArray = (Array)sourceObject;

            if (targetArray == null || targetArray.Length != sourceArray.Length)
            {
                targetObject = targetArray = (Array)Activator.CreateInstance(typeMetadata.udonStorageType, new object[] { sourceArray.Length });
            }
            
            if (elementSerializer == null) // This type can just be serialized simply with a direct array copy. This prevents garbage from passing all the copies through an object.
            {
                Array.Copy(sourceArray, targetArray, sourceArray.Length);
            }
            else // The elements need special handling so use the element serializer
            {
                for (int i = 0; i < sourceArray.Length; ++i)
                {
                    object elementObj = targetArray.GetValue(i);
                    elementSerializer.SerializeToUdonTypeInPlace(ref elementObj, sourceArray.GetValue(i));
                    targetArray.SetValue(elementObj, i);
                }
            }
        }

        public override void SerializeToCSharpTypeInPlace(ref object targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                targetObject = null;
                return;
            }

            Array targetArray = (Array)targetObject;
            Array sourceArray = (Array)sourceObject;

            if (targetArray == null || targetArray.Length != sourceArray.Length)
            {
                targetObject = targetArray = (Array)Activator.CreateInstance(typeMetadata.cSharpType, new object[] { sourceArray.Length });
            }

            if (elementSerializer == null) // This type can just be serialized simply with a direct array copy. This prevents garbage from passing all the copies through an object.
            {
                Array.Copy(sourceArray, targetArray, sourceArray.Length);
            }
            else // The elements need special handling so use the element serializer
            {
                for (int i = 0; i < sourceArray.Length; ++i)
                {
                    object elementObj = targetArray.GetValue(i);
                    elementSerializer.SerializeToCSharpTypeInPlace(ref elementObj, sourceArray.GetValue(i));
                    targetArray.SetValue(elementObj, i);
                }
            }
        }
    }
}

