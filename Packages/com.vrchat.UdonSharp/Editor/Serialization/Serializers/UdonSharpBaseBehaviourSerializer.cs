using System;
using VRC.Udon;

namespace UdonSharp.Serialization
{
    internal class UdonSharpBaseBehaviourSerializer : Serializer<UdonSharpBehaviour>
    {
        public UdonSharpBaseBehaviourSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(UdonBehaviour);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return typeMetadata.cSharpType == typeof(UdonSharpBehaviour);
        }

        public override void Read(ref UdonSharpBehaviour targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            UdonBehaviour sourceBehaviour = (UdonBehaviour)sourceObject.Value;
            if (sourceBehaviour == null)
            {
                targetObject = null;
                return;
            }

            Type behaviourType = UdonSharpProgramAsset.GetBehaviourClass(sourceBehaviour);

            Serializer behaviourSerializer = CreatePooled(behaviourType);

            object behaviourRef = targetObject;
            behaviourSerializer.ReadWeak(ref behaviourRef, sourceObject);
            targetObject = (UdonSharpBehaviour)behaviourRef;
        }

        public override void Write(IValueStorage targetObject, in UdonSharpBehaviour sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                targetObject.Value = null;
                return;
            }

            Serializer behaviourSerializer = CreatePooled(sourceObject.GetType());
            
            behaviourSerializer.WriteWeak(targetObject, sourceObject);
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return new UdonSharpBaseBehaviourSerializer(typeMetadata);
        }
    }
}

