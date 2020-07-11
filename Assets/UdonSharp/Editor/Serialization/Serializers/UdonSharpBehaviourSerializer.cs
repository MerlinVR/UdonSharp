using System;
using System.Collections.Generic;
using System.Reflection;
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

        T CreateProxyBehaviour(UdonBehaviour sourceBehaviour)
        {
            FieldInfo backingBehaviourField = typeof(UdonSharpBehaviour).GetField("_backingUdonBehaviour", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingBehaviourField == null)
                throw new System.Exception("Could not find backing behaviour field");

            
            UdonSharpBehaviour[] existingBehaviours = sourceBehaviour.GetComponents<UdonSharpBehaviour>();

            foreach (UdonSharpBehaviour behaviour in existingBehaviours)
            {
                UdonBehaviour backingBehaviour = (UdonBehaviour)backingBehaviourField.GetValue(behaviour);

                if (backingBehaviour == sourceBehaviour)
                    return (T)behaviour;
            }

            T newBackingBehaviour = sourceBehaviour.gameObject.AddComponent<T>();
            backingBehaviourField.SetValue(newBackingBehaviour, sourceBehaviour);
            newBackingBehaviour.hideFlags = UnityEngine.HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                                            UnityEngine.HideFlags.HideInInspector |
#endif
                                            UnityEngine.HideFlags.DontSaveInEditor;

            return newBackingBehaviour;
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            if (sourceObject.Value == null)
            {
                targetObject = null;
                return;
            }

            if (targetObject == null)
                targetObject = CreateProxyBehaviour((UdonBehaviour)sourceObject.Value);

            targetObject.enabled = false;

            if (UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Contains(targetObject))
                return;

            UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Add(targetObject);

            UdonSharpBehaviourFormatterEmitter.GetFormatter<T>().Read(ref targetObject, sourceObject);

            UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Remove(targetObject);
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
            
            UdonSharpBehaviourFormatterEmitter.GetFormatter<T>().Write(targetObject, sourceObject);

            UdonSharpBehaviourSerializationTracker.serializedBehaviourSet.Remove(sourceObject);
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            return (Serializer)System.Activator.CreateInstance(typeof(UdonSharpBehaviourSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

