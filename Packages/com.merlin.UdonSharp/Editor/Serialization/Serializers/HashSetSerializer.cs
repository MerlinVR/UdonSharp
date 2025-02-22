using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp.Lib.Internal;

namespace UdonSharp.Serialization
{
    internal class HashSetSerializer<T> : Serializer<T>
    {
        public HashSetSerializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return (Serializer)Activator.CreateInstance(typeof(HashSetSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return UdonSharpUtils.IsHashSetType(typeMetadata.cSharpType);
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
            
            Type elementType = typeof(T).GetGenericArguments()[0];
            Type uSharpHashSetType = typeof(Lib.Internal.Collections.HashSet<>).MakeGenericType(elementType);
            Serializer serializer = CreatePooled(uSharpHashSetType);
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject, out object serializedObject))
            {
                targetObject.Value = serializedObject;
                return;
            }
            
            object newUSharpHashSet = Activator.CreateInstance(uSharpHashSetType);
            
            MethodInfo addMethod = uSharpHashSetType.GetMethod("Add");
            
            foreach (object item in (IEnumerable)sourceObject)
            {
                // ReSharper disable once PossibleNullReferenceException
                addMethod.Invoke(newUSharpHashSet, new[] { item });
            }
            
            MethodInfo storeSerializedDataMethod = uSharpHashSetType.GetMethod(nameof(Lib.Internal.Collections.HashSet<object>.StoreSerializedData));
            storeSerializedDataMethod.Invoke(newUSharpHashSet, null);
            
            serializer.WriteWeak(targetObject, newUSharpHashSet);
            
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
                targetObject = (T)Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(typeof(T).GetGenericArguments()[0]));
            }
            else
            {
                MethodInfo hashSetClearMethod = targetObject.GetType().GetMethod("Clear");
                hashSetClearMethod.Invoke(targetObject, null);
            }

            Type elementType = typeof(T).GetGenericArguments()[0];
            Type uSharpHashSetType = typeof(Lib.Internal.Collections.HashSet<>).MakeGenericType(elementType);
            Serializer serializer = CreatePooled(uSharpHashSetType);

            object newUSharpList = Activator.CreateInstance(uSharpHashSetType);
            serializer.ReadWeak(ref newUSharpList, sourceObject);

            MethodInfo toArrayMethod = uSharpHashSetType.GetMethod("ToArray");
            // ReSharper disable once PossibleNullReferenceException
            Array listArray = (Array)toArrayMethod.Invoke(newUSharpList, null);
            
            MethodInfo addMethod = typeof(T).GetMethod("Add");
            
            foreach (object item in listArray)
            {
                // ReSharper disable once PossibleNullReferenceException
                addMethod.Invoke(targetObject, new[] { item });
            }
            
            UsbSerializationContext.SerializedObjectMap[sourceObject.Value] = targetObject;
        }
    }
}