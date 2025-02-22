using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp.Lib.Internal;

namespace UdonSharp.Serialization
{
    internal class ListSerializer<T> : Serializer<T>
    {
        public ListSerializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return (Serializer)Activator.CreateInstance(typeof(ListSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return UdonSharpUtils.IsListType(typeMetadata.cSharpType);
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
            Type uSharpListType = typeof(Lib.Internal.Collections.List<>).MakeGenericType(elementType);
            Serializer serializer = CreatePooled(uSharpListType);
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject, out object serializedObject))
            {
                targetObject.Value = serializedObject;
                return;
            }
            
            object newUSharpList = Activator.CreateInstance(uSharpListType);
            
            MethodInfo addMethod = uSharpListType.GetMethod("Add");
            
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
                targetObject = (T)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(T).GetGenericArguments()[0]));
            }
            else
            {
                ((IList)targetObject).Clear();
            }

            Type elementType = typeof(T).GetGenericArguments()[0];
            Type uSharpListType = typeof(Lib.Internal.Collections.List<>).MakeGenericType(elementType);
            Serializer serializer = CreatePooled(uSharpListType);

            object newUSharpList = Activator.CreateInstance(uSharpListType);
            serializer.ReadWeak(ref newUSharpList, sourceObject);
            
            IList list = (IList)targetObject;
            
            foreach (object item in (IEnumerable)newUSharpList)
            {
                list.Add(item);
            }
            
            targetObject = (T)list;
            
            UsbSerializationContext.SerializedObjectMap[sourceObject.Value] = targetObject;
        }
    }
}