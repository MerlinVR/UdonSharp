using System;
using UdonSharp.Compiler.Udon;
using UnityEngine;

namespace UdonSharp.Serialization
{
    public class UserEnumSerializer<T> : Serializer<T>
    {
        public UserEnumSerializer(TypeSerializationMetadata typeMetadata)
            : base(typeMetadata)
        {
        }

        public override Type GetUdonStorageType()
        {
            return typeof(T).GetEnumUnderlyingType();
        }

        public override bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return typeMetadata.cSharpType.IsEnum && !CompilerUdonInterface.IsExternType(typeMetadata.cSharpType);
        }

        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            VerifySerializationSanity();

            if (sourceObject == null)
            {
                Debug.LogError($"Field for {typeof(T)} does not exist");
                return;
            }

            targetObject = (T)Enum.ToObject(typeof(T), sourceObject.Value);
        }

        public override void Write(IValueStorage targetObject, in T sourceObject)
        {
            VerifySerializationSanity();
            if (targetObject == null)
            {
                Debug.LogError($"Field for {typeof(T)} does not exist");
                return;
            }

            targetObject.Value = Convert.ChangeType(sourceObject, GetUdonStorageType());
        }

        protected override Serializer MakeSerializer(TypeSerializationMetadata typeMetadata)
        {
            VerifyTypeCheckSanity();

            return (Serializer)Activator.CreateInstance(typeof(UserEnumSerializer<>).MakeGenericType(typeMetadata.cSharpType), typeMetadata);
        }
    }
}

