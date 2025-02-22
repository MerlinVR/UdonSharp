using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp.Lib.Internal;

namespace UdonSharp.Serialization
{
    internal class QueueSerializer<T> : Serializer<T>
    {
        public QueueSerializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return (Serializer)Activator.CreateInstance(typeof(QueueSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return UdonSharpUtils.IsQueueType(typeMetadata.cSharpType);
        }

        public override Type GetUdonStorageType()
        {
            return typeof(object[]);
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            if (sourceObject == null)
            {
                targetObject.Value = null;
                return;
            }
            
            Type[] elementTypes = typeof(T).GetGenericArguments();
            Type uSharpQueueType = typeof(Lib.Internal.Collections.Queue<>).MakeGenericType(elementTypes);
            Serializer serializer = CreatePooled(uSharpQueueType);
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject, out object serializedObject))
            {
                targetObject.Value = serializedObject;
                return;
            }
            
            object newUSharpList = Activator.CreateInstance(uSharpQueueType);
            
            MethodInfo addMethod = uSharpQueueType.GetMethod("Enqueue");
            
            foreach (object item in (IEnumerable)sourceObject)
            {
                // ReSharper disable once PossibleNullReferenceException
                addMethod.Invoke(newUSharpList, new[] { item });
            }
            
            serializer.WriteWeak(targetObject, newUSharpList);
            
            UsbSerializationContext.SerializedObjectMap[sourceObject] = targetObject.Value;
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            if (sourceObject.Value == null)
            {
                targetObject = default;
                return;
            }
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject.Value, out object deserializedObject))
            {
                targetObject = (T)deserializedObject;
                return;
            }
            
            if (targetObject == null)
            {
                targetObject = (T)Activator.CreateInstance(typeof(Queue<>).MakeGenericType(typeof(T).GetGenericArguments()));
            }
            else
            {
                MethodInfo clearMethod = targetObject.GetType().GetMethod("Clear");
                clearMethod.Invoke(targetObject, null);
            }

            Type[] elementTypes = typeof(T).GetGenericArguments();
            Type uSharpQueueType = typeof(Lib.Internal.Collections.Queue<>).MakeGenericType(elementTypes);
            Serializer serializer = CreatePooled(uSharpQueueType);

            object newUSharpList = Activator.CreateInstance(uSharpQueueType);
            serializer.ReadWeak(ref newUSharpList, sourceObject);

            MethodInfo toArrayMethod = uSharpQueueType.GetMethod("ToArray");
            // ReSharper disable once PossibleNullReferenceException
            Array listArray = (Array)toArrayMethod.Invoke(newUSharpList, null);
            
            MethodInfo addMethod = uSharpQueueType.GetMethod("Enqueue");
            
            foreach (object item in listArray)
            {
                // ReSharper disable once PossibleNullReferenceException
                addMethod.Invoke(targetObject, new[] { item });
            }
            
            UsbSerializationContext.SerializedObjectMap[sourceObject.Value] = targetObject;
        }
    }
}