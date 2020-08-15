using System;
using System.Collections.Generic;
using System.Reflection;
using UdonSharpEditor;
using VRC.Udon;

namespace UdonSharp.Serialization
{
    internal class UdonSharpBehaviourSerializationTracker
    {
        public static Stack<Serializer> serializerStack = new Stack<Serializer>();
        public static HashSet<UdonSharpBehaviour> serializedBehaviourSet = new HashSet<UdonSharpBehaviour>();
    }

    public class UdonSharpBehaviourSerializer<T> : Serializer<T> where T : UdonSharpBehaviour 
    {
        public UdonSharpBehaviourSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(UdonBehaviour);
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            return typeMetadata.cSharpType == typeof(UdonSharpBehaviour) || typeMetadata.cSharpType.IsSubclassOf(typeof(UdonSharpBehaviour));
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            if (sourceObject.Value == null)
            {
                targetObject = null;
                return;
            }

            if (targetObject == null)
                targetObject = (T)UdonSharpEditorUtility.GetProxyBehaviour((UdonBehaviour)sourceObject.Value, false);

            targetObject.enabled = false;

            if (UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Contains(targetObject))
                return;

            UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Add(targetObject);

            try
            {
                UdonSharpBehaviourFormatterEmitter.GetFormatter<T>().Read(ref targetObject, sourceObject);
            }
            finally
            {
                UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Remove(targetObject);
            }
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            if (sourceObject == null)
            {
                targetObject.Value = null;
                return;
            }

            if (UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Contains(sourceObject))
                return;

            UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Add(sourceObject);

            try
            {
                UdonSharpBehaviourFormatterEmitter.GetFormatter<T>().Write(targetObject, sourceObject);
            }
            finally
            {
                UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Remove(sourceObject);
            }

            targetObject.Value = UdonSharpEditorUtility.GetBackingUdonBehaviour(sourceObject);
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            return (Serializer)System.Activator.CreateInstance(typeof(UdonSharpBehaviourSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

