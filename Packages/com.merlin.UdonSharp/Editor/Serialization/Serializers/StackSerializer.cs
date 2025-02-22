using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp.Lib.Internal;

namespace UdonSharp.Serialization
{
    internal class StackSerializer<T> : Serializer<T>
    {
        public StackSerializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return (Serializer)Activator.CreateInstance(typeof(StackSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return UdonSharpUtils.IsStackType(typeMetadata.cSharpType);
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
            Type uSharpStackType = typeof(Lib.Internal.Collections.Stack<>).MakeGenericType(elementTypes);
            Serializer serializer = CreatePooled(uSharpStackType);
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject, out object serializedObject))
            {
                targetObject.Value = serializedObject;
                return;
            }
            
            object newUSharpList = Activator.CreateInstance(uSharpStackType);
            
            MethodInfo addMethod = uSharpStackType.GetMethod("Push");
            
            object[] reverseArray = new object[((IEnumerable)sourceObject).Cast<object>().Count()];
            int index = reverseArray.Length - 1;
            foreach (object item in (IEnumerable)sourceObject)
            {
                reverseArray[index] = item;
                index--;
            }
            
            foreach (object item in reverseArray)
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
                targetObject = (T)Activator.CreateInstance(typeof(Stack<>).MakeGenericType(typeof(T).GetGenericArguments()));
            }
            else
            {
                MethodInfo clearMethod = targetObject.GetType().GetMethod("Clear");
                clearMethod.Invoke(targetObject, null);
            }

            Type[] elementTypes = typeof(T).GetGenericArguments();
            Type uSharpStackType = typeof(Lib.Internal.Collections.Stack<>).MakeGenericType(elementTypes);
            Serializer serializer = CreatePooled(uSharpStackType);

            object newUSharpList = Activator.CreateInstance(uSharpStackType);
            serializer.ReadWeak(ref newUSharpList, sourceObject);

            MethodInfo toArrayMethod = uSharpStackType.GetMethod("ToArray");
            // ReSharper disable once PossibleNullReferenceException
            Array listArray = (Array)toArrayMethod.Invoke(newUSharpList, null);
            
            MethodInfo addMethod = uSharpStackType.GetMethod("Push");
            
            object[] reverseArray = new object[listArray.Cast<object>().Count()];
            int index = reverseArray.Length - 1;
            foreach (object item in (IEnumerable)listArray)
            {
                reverseArray[index] = item;
                index--;
            }
            
            foreach (object item in reverseArray)
            {
                // ReSharper disable once PossibleNullReferenceException
                addMethod.Invoke(targetObject, new[] { item });
            }
            
            UsbSerializationContext.SerializedObjectMap[sourceObject.Value] = targetObject;
        }
    }
}