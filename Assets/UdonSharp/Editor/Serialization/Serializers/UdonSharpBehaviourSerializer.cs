using System;

namespace UdonSharp.Serialization
{
    public class UdonSharpBehaviourSerializer<T> : Serializer<T> where T : UdonSharpBehaviour 
    {
        public UdonSharpBehaviourSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            throw new NotImplementedException();
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            throw new NotImplementedException();
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            throw new NotImplementedException();
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            throw new NotImplementedException();
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            throw new NotImplementedException();
        }
    }
}

