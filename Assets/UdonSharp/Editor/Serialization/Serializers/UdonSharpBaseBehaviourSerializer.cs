using System;
using VRC.Udon;

namespace UdonSharp.Serialization
{
    public class UdonSharpBaseBehaviourSerializer : Serializer<UdonSharpBehaviour>
    {
        public UdonSharpBaseBehaviourSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(UdonBehaviour);
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
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

            System.Type behaviourType = UdonSharpProgramAsset.GetBehaviourClass(sourceBehaviour);

            Serializer behaviourSerializer = Serializer.CreatePooled(behaviourType);

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

            Serializer behaviourSerializer = Serializer.CreatePooled(sourceObject.GetType());
            
            behaviourSerializer.WriteWeak(targetObject, sourceObject);
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return new UdonSharpBaseBehaviourSerializer(typeMetadata);
        }
    }
}

