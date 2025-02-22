using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp.Lib.Internal;

namespace UdonSharp.Serialization
{
    internal class DictionarySerializer<T> : Serializer<T>
    {
        public DictionarySerializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return (Serializer)Activator.CreateInstance(typeof(DictionarySerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            return UdonSharpUtils.IsDictionaryType(typeMetadata.cSharpType);
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
            Type uSharpDictionaryType = typeof(Lib.Internal.Collections.Dictionary<,>).MakeGenericType(elementTypes);
            Serializer serializer = CreatePooled(uSharpDictionaryType);
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject, out object serializedObject))
            {
                targetObject.Value = serializedObject;
                return;
            }
            
            object newUSharpDictionary = Activator.CreateInstance(uSharpDictionaryType);
            
            MethodInfo addMethod = uSharpDictionaryType.GetMethod("Add");
            
            foreach (DictionaryEntry item in (IDictionary)sourceObject)
            {
                // ReSharper disable once PossibleNullReferenceException
                addMethod.Invoke(newUSharpDictionary, new[] { item.Key, item.Value });
            }
            
            MethodInfo storeSerializedDataMethod = uSharpDictionaryType.GetMethod(nameof(Lib.Internal.Collections.Dictionary<object, object>.StoreSerializedData));
            storeSerializedDataMethod.Invoke(newUSharpDictionary, null);
            
            serializer.WriteWeak(targetObject, newUSharpDictionary);
            
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
                targetObject = (T)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(T).GetGenericArguments()));
            }
            else
            {
                ((IDictionary)targetObject).Clear();
            }

            Type[] elementTypes = typeof(T).GetGenericArguments();
            Type uSharpDictionaryType = typeof(Lib.Internal.Collections.Dictionary<,>).MakeGenericType(elementTypes);
            Serializer serializer = CreatePooled(uSharpDictionaryType);

            object newUSharpDictionary = Activator.CreateInstance(uSharpDictionaryType);
            serializer.ReadWeak(ref newUSharpDictionary, sourceObject);

            MethodInfo getEnumeratorMethod = uSharpDictionaryType.GetMethod("GetEnumerator");
            object enumerator = getEnumeratorMethod.Invoke(newUSharpDictionary, null);
            MethodInfo moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
            
            PropertyInfo currentProperty = enumerator.GetType().GetProperty("Current");
            object current = currentProperty.GetValue(enumerator);
            
            FieldInfo keyField = current.GetType().GetField("key");
            FieldInfo valueField = current.GetType().GetField("value");
            
            while ((bool)moveNextMethod.Invoke(enumerator, null))
            {
                object key = keyField.GetValue(current);
                object value = valueField.GetValue(current);
                
                ((IDictionary)targetObject).Add(key, value);
            }
            
            UsbSerializationContext.SerializedObjectMap[sourceObject.Value] = targetObject;
        }
    }
}