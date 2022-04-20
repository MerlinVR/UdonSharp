
using System;
using System.Collections.Generic;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace UdonSharp.Serialization
{
    /// <summary>
    /// UdonSharpBehaviour Serialization Context, confusing abbreviation isn't it?
    /// </summary>
    internal static class UsbSerializationContext
    {
        public static readonly HashSet<UdonSharpBehaviour> SerializedBehaviourSet = new HashSet<UdonSharpBehaviour>();
        public static ProxySerializationPolicy CurrentPolicy;
        public static int CurrentDepth;
        public static HashSet<UnityEngine.Object> Dependencies = new HashSet<Object>();
        public static readonly object UsbLock = new object();

        public static bool CollectDependencies => CurrentPolicy?.CollectDependencies ?? false;

        public static bool UseHeapSerialization => CollectDependencies || (CurrentPolicy?.IsPreBuildSerialize ?? false);
    }

    internal class UdonSharpBehaviourSerializer : Serializer<UdonSharpBehaviour>
    {
        public UdonSharpBehaviourSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(UdonBehaviour);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            return typeMetadata.cSharpType == typeof(UdonSharpBehaviour) || typeMetadata.cSharpType.IsSubclassOf(typeof(UdonSharpBehaviour));
        }

        public override void Read(ref UdonSharpBehaviour targetObject, IValueStorage sourceObject)
        {
            UdonBehaviour sourceBehaviour = (UdonBehaviour)sourceObject.Value;

            if (sourceBehaviour == null)
            {
                if (!UsbSerializationContext.CollectDependencies)
                    targetObject = null;
                
                return;
            }
            
            lock (UsbSerializationContext.UsbLock)
            {
                if (UsbSerializationContext.CurrentPolicy == null)
                    throw new NullReferenceException("Serialization policy cannot be null");

                if (UsbSerializationContext.CollectDependencies)
                    UsbSerializationContext.Dependencies.Add(sourceBehaviour);
                
                targetObject = UdonSharpEditorUtility.GetProxyBehaviour(sourceBehaviour);

                if (UsbSerializationContext.CurrentDepth >= UsbSerializationContext.CurrentPolicy.MaxSerializationDepth)
                    return;

                if (UsbSerializationContext.SerializedBehaviourSet.Contains(targetObject))
                    return;

                UsbSerializationContext.SerializedBehaviourSet.Add(targetObject);
                UsbSerializationContext.CurrentDepth++;

                try
                {
                    Type behaviourType = UdonSharpProgramAsset.GetBehaviourClass(sourceBehaviour);
                    IFormatter formatter = UdonSharpBehaviourFormatterEmitter.GetFormatter(behaviourType);

                    object targetSysObj = targetObject;
                    formatter.Read(ref targetSysObj, sourceObject);
                    
                    if (!UsbSerializationContext.CollectDependencies)
                        targetObject = (UdonSharpBehaviour)targetSysObj;
                }
                finally
                {
                    UsbSerializationContext.CurrentDepth--;

                    if (UsbSerializationContext.CurrentDepth <= 0)
                    {
                        Debug.Assert(UsbSerializationContext.CurrentDepth == 0,
                            "Serialization depth cannot be negative");

                        UsbSerializationContext.SerializedBehaviourSet.Clear();
                    }
                }
            }
        }

        public override void Write(IValueStorage targetObject, in UdonSharpBehaviour sourceObject)
        {
            if (sourceObject == null)
            {
                if (!UsbSerializationContext.CollectDependencies)
                    targetObject.Value = null;
                
                return;
            }
            
            lock (UsbSerializationContext.UsbLock)
            {
                if (UsbSerializationContext.CurrentPolicy == null)
                    throw new NullReferenceException("Serialization policy cannot be null");
                
                if (UsbSerializationContext.CollectDependencies)
                    UsbSerializationContext.Dependencies.Add(sourceObject);
            
                UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(sourceObject);
            
                if (UsbSerializationContext.CurrentDepth >= UsbSerializationContext.CurrentPolicy.MaxSerializationDepth)
                {
                    if (!UsbSerializationContext.CollectDependencies)
                        targetObject.Value = backingBehaviour ? backingBehaviour : null;
                    
                    return;
                }
            
                UsbSerializationContext.CurrentDepth++;
                
                try
                {
                    if (!UsbSerializationContext.CollectDependencies)
                    {
                        if (backingBehaviour)
                        {
                            targetObject.Value = backingBehaviour;
                        }
                        else
                        {
                            targetObject.Value = null;
                        }
                    }

                    if (UsbSerializationContext.SerializedBehaviourSet.Contains(sourceObject))
                        return;
            
                    UsbSerializationContext.SerializedBehaviourSet.Add(sourceObject);

                    IFormatter formatter = UdonSharpBehaviourFormatterEmitter.GetFormatter(sourceObject.GetType());
            
                    formatter.Write(targetObject, sourceObject);
                }
                finally
                {
                    UsbSerializationContext.CurrentDepth--;
            
                    if (UsbSerializationContext.CurrentDepth <= 0)
                    {
                        Debug.Assert(UsbSerializationContext.CurrentDepth == 0,
                            "Serialization depth cannot be negative");
            
                        UsbSerializationContext.SerializedBehaviourSet.Clear();
                    }
                }
            }
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            Serializer innerSerializer = (Serializer)Activator.CreateInstance(typeof(UdonSharpBehaviourSerializer), typeMetadata);

            return (Serializer)Activator.CreateInstance(typeof(UdonSharpBehaviourTypedWrapper<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata, innerSerializer);
        }

        private class UdonSharpBehaviourTypedWrapper<T> : Serializer<T> where T : UdonSharpBehaviour
        {
            private readonly UdonSharpBehaviourSerializer _innerSerializer;
            
            public UdonSharpBehaviourTypedWrapper(TypeSerializationMetadata typeMetadata, UdonSharpBehaviourSerializer innerSerializer) 
                :base(typeMetadata)
            {
                _innerSerializer = innerSerializer;
            }

            protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
            {
                throw new NotImplementedException();
            }

            protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
            {
                throw new NotImplementedException();
            }

            public override Type GetUdonStorageType()
            {
                return _innerSerializer.GetUdonStorageType();
            }

            public override void Write(IValueStorage targetObject, in T sourceObject)
            {
                _innerSerializer.Serialize(targetObject, sourceObject);
            }

            public override void Read(ref T targetObject, IValueStorage sourceObject)
            {
                UdonSharpBehaviour refObj = targetObject;
                _innerSerializer.Read(ref refObj, sourceObject);
                targetObject = (T)refObj;
            }
        }
    }
}

