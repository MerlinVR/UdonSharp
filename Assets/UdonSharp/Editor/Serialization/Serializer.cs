
using System.Collections.Generic;

namespace UdonSharp.Serialization
{
    public abstract class Serializer
    {
        protected TypeSerializationMetadata typeMetadata;

        protected Serializer(TypeSerializationMetadata typeMetadata)
        {
            this.typeMetadata = typeMetadata;
        }

        // Serializers that will be checked against the type, this list is ordered specifically based on priority, do not arbitrarily reorder it
        private static readonly List<Serializer> typeCheckSerializers = new List<Serializer>()
        {
            new JaggedArraySerializer(null), 
            new ArraySerializer(null),
            new DefaultSerializer(null),
        };

        private static Dictionary<TypeSerializationMetadata, Serializer> typeSerializerDictionary = new Dictionary<TypeSerializationMetadata, Serializer>();

        public static Serializer CreatePooled(TypeSerializationMetadata typeMetadata)
        {
            if (typeMetadata == null)
                throw new System.ArgumentException("Type metadata cannot be null for serializer creation");

            Serializer serializer;
            if (!typeSerializerDictionary.TryGetValue(typeMetadata, out serializer))
            {
                serializer = Create(typeMetadata);
                typeSerializerDictionary.Add(typeMetadata, serializer);
            }

            return serializer;
        }

        public static Serializer Create(TypeSerializationMetadata typeMetadata)
        {
            if (typeMetadata == null)
                throw new System.ArgumentException("Type metadata cannot be null for serializer creation");

            foreach (Serializer checkSerializer in typeCheckSerializers)
            {
                if (checkSerializer.HandlesTypeSerialization(typeMetadata))
                {
                    return (Serializer)System.Activator.CreateInstance(checkSerializer.GetType(), new object[] { typeMetadata });
                }
            }

            throw new System.Exception($"Failed to initialize a valid serializer for {typeMetadata}");
        }

        /// <summary>
        /// Returns true if this serializer should be used for a given type, returns false otherwise.
        /// </summary>
        /// <param name="typeMetadata"></param>
        /// <returns></returns>
        public abstract bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata);

        /// <summary>
        /// Implements a conversion from an Udon value type to its equivalent C# type.
        /// For instance, in Udon, jagged arrays are stored as object[]'s which store more array types down to the base array type.
        /// These types need to be converted to their actual type, so a jagged array of int[][] would be converted from its storage type of object[] to an int[][]
        /// </summary>
        /// <param name="udonValue"></param>
        /// <param name="typeMetadata"></param>
        /// <returns></returns>
        public virtual object SerializeToCSharpType(object udonValue)
        {
            object targetObj = null;
            SerializeToCSharpTypeInPlace(ref targetObj, udonValue);
            return targetObj;
        }

        /// <summary>
        /// This goes in the opposite direction, it converts C# types to Udon types that can be stored
        /// </summary>
        /// <param name="cSharpValue"></param>
        /// <returns></returns>
        public virtual object SerializeToUdonType(object cSharpValue)
        {
            object targetObj = null;
            SerializeToUdonTypeInPlace(ref targetObj, cSharpValue);
            return targetObj;
        }

        /// <summary>
        /// Serializes the source C# object directly into the target Udon object and attempt to avoid creating new objects when possible.
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="sourceObject"></param>
        public abstract void SerializeToUdonTypeInPlace(ref object targetObject, object sourceObject);

        /// <summary>
        /// Serializes the source Udon object directly into the target C# object and attempt to avoid creating new objects when possible.
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="sourceObject"></param>
        public abstract void SerializeToCSharpTypeInPlace(ref object targetObject, object sourceObject);

        /// <summary>
        /// Verifies that this serializer is in the correct state to be using HandlesTypeSerialization()
        /// </summary>
        protected void VerifyTypeCheckSanity()
        {
            if (typeMetadata != null)
                throw new System.Exception("Cannot call HandlesTypeSerialization() on object");
        }

        /// <summary>
        /// Verifies that this serializer is in the correct state to be using the serialization methods
        /// </summary>
        protected void VerifySerializationSanity()
        {
            if (typeMetadata == null)
                throw new System.Exception("Serializer is not in correct state to serialize data");
        }
    }
}
