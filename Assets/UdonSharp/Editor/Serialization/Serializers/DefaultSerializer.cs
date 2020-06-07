using System;

namespace UdonSharp.Serialization
{
    public class DefaultSerializer : Serializer
    {
        public DefaultSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return true;
        }

        public override void SerializeToUdonTypeInPlace(ref object targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            targetObject = sourceObject;
        }

        public override void SerializeToCSharpTypeInPlace(ref object targetObject, object sourceObject)
        {
            VerifySerializationSanity();

            targetObject = sourceObject;
        }
    }
}

