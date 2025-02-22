using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Serialization
{
    internal class UserClassSerializer<T> : Serializer<T> where T : class
    {
        public UserClassSerializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }
        
        public override Type GetUdonStorageType()
        {
            return typeof(object[]);
        }
        
        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            
            return (Serializer)Activator.CreateInstance(typeof(UserClassSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }

        protected override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();
            
            return UdonSharpUtils.IsUserDefinedClass(typeMetadata.cSharpType);
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            if (sourceObject == null)
            {
                if (!UsbSerializationContext.CollectDependencies)
                {
                    targetObject.Value = null;
                }

                return;
            }
            
            UdonSharpEditorCache.UdonClassInfo classInfo = UdonSharpEditorCache.Instance.GetSerializationInfo(typeof(T));
            
            if (classInfo == null)
            {
                UdonSharpUtils.LogWarning($"Could not find serialization info for type {typeof(T)}, Unity C# compile may need to run.");
                return;
            }
            
            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(sourceObject, out object serializedSourceObj)) // We've already serialized this object, just use the serialized value instead of serializing it again and potentially causing a stack overflow
            {
                if (!UsbSerializationContext.CollectDependencies)
                {
                    targetObject.Value = serializedSourceObj;
                }

                return;
            }
            
            // Prefer to do in-place serialization if the array is already the correct size and exists so we don't potentially break references or cause odd behavior
            int expectedSize = classInfo.fields.Length + ImportedUdonSharpTypeSymbol.HEADER_SIZE;
            object[] serializedData = targetObject.Value as object[];
            
            if (serializedData == null || serializedData.Length != expectedSize)
            {
                serializedData = new object[expectedSize];

                if (!UsbSerializationContext.CollectDependencies)
                {
                    targetObject.Value = serializedData;
                }
            }
            
            // We need to add the serialized object to the map before we serialize the fields to handle circular references
            UsbSerializationContext.SerializedObjectMap[sourceObject] = serializedData;
            
            foreach (UdonSharpEditorCache.UdonFieldInfo fieldInfo in classInfo.fields)
            {
                FieldInfo cSharpFieldInfo = typeof(T).GetField(fieldInfo.fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                if (cSharpFieldInfo == null)
                {
                    UdonSharpUtils.LogWarning($"Could not find field {fieldInfo.fieldName} on type {typeof(T)}, Unity C# compile may need to run.");
                    continue;
                }
                
                if (cSharpFieldInfo.IsStatic || cSharpFieldInfo.IsLiteral)
                {
                    continue;
                }
                
                if (cSharpFieldInfo.FieldType != fieldInfo.fieldType)
                {
                    // This may be called through the field setup during compile which uses a dynamic assembly so the types won't match
                    if (!cSharpFieldInfo.FieldType.Assembly.FullName.StartsWith(UdonSharpCompilerV1.UdonSharpAssemblyNamePrefix, StringComparison.Ordinal) || cSharpFieldInfo.FieldType.FullName != fieldInfo.fieldType.FullName)
                    {
                        UdonSharpUtils.LogWarning($"Field type mismatch on field {fieldInfo.fieldName} in type {typeof(T)}; expected {cSharpFieldInfo.FieldType}, got {fieldInfo.fieldType}");
                        continue;
                    }
                }
                
                object fieldVal = cSharpFieldInfo.GetValue(sourceObject);

                if (fieldVal == null)
                {
                    continue;
                }
                
                int fieldIndex = fieldInfo.fieldIndex;
                Array fieldArray = serializedData;
                
                Serializer fieldSerializer = Serializer.CreatePooled(cSharpFieldInfo.FieldType);

                // Value types are wrapped in a 1-element array in U# to avoid boxing
                if (UdonSharpUtils.IsStrongBoxedType(fieldSerializer.GetUdonStorageType()))
                {
                    fieldArray = Array.CreateInstance(fieldSerializer.GetUdonStorageType(), 1);
                    serializedData[fieldIndex] = fieldArray;
                    
                    fieldIndex = 0;
                }

                IValueStorage fieldStorage = ValueStorageUtil.CreateStorage(fieldSerializer.GetUdonStorageType());
                fieldSerializer.WriteWeak(fieldStorage, fieldVal);
                fieldArray.SetValue(fieldStorage.Value, fieldIndex);
            }
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            if (sourceObject?.Value == null)
            {
                if (!UsbSerializationContext.CollectDependencies)
                {
                    targetObject = default;
                }

                return;
            }

            UdonSharpEditorCache.UdonClassInfo classInfo = UdonSharpEditorCache.Instance.GetSerializationInfo(typeof(T));
            
            if (classInfo == null)
            {
                UdonSharpUtils.LogWarning($"Could not find serialization info for type {typeof(T)}, Unity C# compile may need to run.");
                return;
            }

            object[] serializedData = (object[])sourceObject.Value;
            
            if (serializedData.Length != classInfo.fields.Length + ImportedUdonSharpTypeSymbol.HEADER_SIZE)
            {
                UdonSharpUtils.LogWarning($"Serialized data for type {typeof(T)} is invalid, expected {classInfo.fields.Length + ImportedUdonSharpTypeSymbol.HEADER_SIZE} elements, got {serializedData.Length}");
                return;
            }

            if (UsbSerializationContext.SerializedObjectMap.TryGetValue(serializedData, out object deserializedObj))
            {
                if (!UsbSerializationContext.CollectDependencies)
                {
                    targetObject = (T)deserializedObj;
                }

                return;
            }

            if (targetObject == null && !UsbSerializationContext.CollectDependencies)
            {
                // We can't use new T() because we don't know if the type has a parameterless constructor, so we depend on the serialization system to set up the object properly
                targetObject = (T)FormatterServices.GetUninitializedObject(typeof(T)); 
            }

            UsbSerializationContext.SerializedObjectMap[serializedData] = targetObject;
            
            foreach (UdonSharpEditorCache.UdonFieldInfo fieldInfo in classInfo.fields)
            {
                FieldInfo cSharpFieldInfo = typeof(T).GetField(fieldInfo.fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                if (cSharpFieldInfo == null)
                {
                    UdonSharpUtils.LogWarning($"Could not find field {fieldInfo.fieldName} on type {typeof(T)}, Unity C# compile may need to run.");
                    continue;
                }
                
                if (cSharpFieldInfo.IsStatic || cSharpFieldInfo.IsLiteral)
                {
                    continue;
                }

                if (cSharpFieldInfo.FieldType != fieldInfo.fieldType)
                {
                    // This may be called through the field setup during compile which uses a dynamic assembly so the types won't match
                    if (!cSharpFieldInfo.FieldType.Assembly.FullName.StartsWith(UdonSharpCompilerV1.UdonSharpAssemblyNamePrefix, StringComparison.Ordinal) || cSharpFieldInfo.FieldType.FullName != fieldInfo.fieldType.FullName)
                    {
                        UdonSharpUtils.LogWarning($"Field type mismatch on field {fieldInfo.fieldName} in type {typeof(T)}; expected {cSharpFieldInfo.FieldType}, got {fieldInfo.fieldType}");
                        continue;
                    }
                }

                int fieldIndex = fieldInfo.fieldIndex;
                Array fieldArray = serializedData;

                Serializer fieldSerializer = Serializer.CreatePooled(cSharpFieldInfo.FieldType);

                // Value types are wrapped in a 1-element array in U# to avoid boxing
                if (UdonSharpUtils.IsStrongBoxedType(fieldSerializer.GetUdonStorageType()))
                {
                    fieldArray = (Array)serializedData[fieldIndex];
                    fieldIndex = 0;
                }
                
                object fieldVal = null;
                
                IValueStorage fieldStorage = ValueStorageUtil.CreateStorage(fieldSerializer.GetUdonStorageType());
                fieldStorage.Value = fieldArray.GetValue(fieldIndex);
                fieldSerializer.ReadWeak(ref fieldVal, fieldStorage);

                if (!UsbSerializationContext.CollectDependencies)
                {
                    cSharpFieldInfo.SetValue(targetObject, fieldVal);
                }
            }
        }
    }
}