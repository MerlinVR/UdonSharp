using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Serialization
{
    /// <summary>
    /// Version for type serialization.
    /// ONLY add versions where the comment below dictates, if you add them anywhere else, you will break serialization.
    /// </summary>
    [System.Serializable]
    public enum TypeSerializationVersion
    {
        Default,
        V1Serialization,

        // Add new versions before this line
        // DO NOT touch these and don't add anything after them
        __MostRecentVerIntnl,
        LatestVer = __MostRecentVerIntnl - 1,
    }

    [System.Serializable]
    public class FieldSerializationMetadata
    {
        /// <summary>
        /// The name of the field, used for mapping fields between changes in serialized data
        /// </summary>
        public string fieldName;

        /// <summary>
        /// The index that the field is currently stored in inside the parent type
        /// If the index is negative, it means that this field is stored directly in a UdonBehaviour and does not need to know its corresponding index since it can be looked up by name
        /// </summary>
        public int fieldStorageIdx = -1;

        /// <summary>
        /// The type data for this field
        /// </summary>
        public TypeSerializationMetadata fieldTypeMetadata;

        public override bool Equals(object obj)
        {
            var metadata = obj as FieldSerializationMetadata;
            return metadata != null &&
                   fieldStorageIdx == metadata.fieldStorageIdx &&
                   fieldName == metadata.fieldName &&
                   EqualityComparer<TypeSerializationMetadata>.Default.Equals(fieldTypeMetadata, metadata.fieldTypeMetadata);
        }

        // todo: this hash code should be built on construction
        public override int GetHashCode()
        {
            var hashCode = -1973968215;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(fieldName);
            hashCode = hashCode * -1521134295 + fieldStorageIdx.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<TypeSerializationMetadata>.Default.GetHashCode(fieldTypeMetadata);
            return hashCode;
        }
    }

    /// <summary>
    /// Contains type information for a serialized Udon type. 
    /// This will be necessary for keeping backwards compatibility when types get fields moved around, deleted, or have their types changed.
    /// Assumes use of Odin serializer to serialize things that Unity can't serialize by default.
    /// There may be multiple metadatas defined for the same type, since objects may have been saved and serialized in different formats due to changes in the underlying script.
    /// </summary>
    [System.Serializable]
    public class TypeSerializationMetadata
    {
        /// <summary>
        /// The version of the serialized data, allows backwards compatibility with changes in type serialization.
        /// </summary>
        public TypeSerializationVersion version = TypeSerializationVersion.LatestVer;

        /// <summary>
        /// The underlying C# type which will often differ from the Udon storage type. This is what is used for the type's C# interface.
        /// </summary>
        public System.Type cSharpType;

        /// <summary>
        /// The type that this type is stored as in Udon programs. For custom user-defined classes that aren't behaviours this will always be `object[]`
        /// For fields in behaviours this will usually be the most exact type that something can be stored as in Udon
        /// </summary>
        public System.Type udonStorageType;

        /// <summary>
        /// Used to describe the format of array element data. Will be null if this isn't an array
        /// </summary>
        public TypeSerializationMetadata arrayElementMetadata;

        /// <summary>
        /// All serialized fields stored by this type instance
        /// </summary>
        public FieldSerializationMetadata[] fieldData;

        public TypeSerializationMetadata()
        { }

        public TypeSerializationMetadata(System.Type cSharpType)
        {
            SetToType(cSharpType);
        }

        public override bool Equals(object obj)
        {
            var metadata = obj as TypeSerializationMetadata;
            return metadata != null &&
                   version == metadata.version &&
                   EqualityComparer<Type>.Default.Equals(cSharpType, metadata.cSharpType) &&
                   EqualityComparer<Type>.Default.Equals(udonStorageType, metadata.udonStorageType) &&
                   EqualityComparer<FieldSerializationMetadata[]>.Default.Equals(fieldData, metadata.fieldData);
        }

        // todo: this hash code should be built on construction
        public override int GetHashCode()
        {
            var hashCode = -2022933226;
            hashCode = hashCode * -1521134295 + version.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(cSharpType);
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(udonStorageType);
            hashCode = hashCode * -1521134295 + EqualityComparer<FieldSerializationMetadata[]>.Default.GetHashCode(fieldData);
            return hashCode;
        }

        public override string ToString()
        {
            return $"Serialization metadata - C# T:{cSharpType}, U# T: {udonStorageType}";
        }

        public void SetToType(System.Type type)
        {
            cSharpType = type;
            if (cSharpType != null && cSharpType.IsArray)
            {
                System.Type elementType = cSharpType;
                while (elementType.IsArray)
                    elementType = elementType.GetElementType();

                arrayElementMetadata = new TypeSerializationMetadata(elementType);
            }

            udonStorageType = UdonSharpUtils.UserTypeToUdonType(cSharpType);
        }
    }
}
