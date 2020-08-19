
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
            new JaggedArraySerializer<object>(null), 
            new ArraySerializer<object>(null),
            new UdonSharpBaseBehaviourSerializer(null),
            new UdonSharpBehaviourSerializer<UdonSharpBehaviour>(null),
            new UnityObjectSerializer<UnityEngine.Object>(null),
            //new SystemObjectSerializer(null),
            new DefaultSerializer<object>(null),
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

        public static Serializer<T> CreatePooled<T>()
        {
            return (Serializer<T>)CreatePooled(typeof(T));
        }

        static TypeSerializationMetadata lookupPooledTypeData = new TypeSerializationMetadata();

        public static Serializer CreatePooled(System.Type type)
        {
            lookupPooledTypeData.SetToType(type);

            Serializer serializer;
            if (!typeSerializerDictionary.TryGetValue(lookupPooledTypeData, out serializer))
            {
                TypeSerializationMetadata typeMetadata = new TypeSerializationMetadata(type);
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
                    return checkSerializer.MakeSerializer(typeMetadata);
                }
            }

            throw new System.Exception($"Failed to initialize a valid serializer for {typeMetadata}");
        }

        protected abstract Serializer MakeSerializer(TypeSerializationMetadata typeMetadata);

        /// <summary>
        /// Returns true if this serializer should be used for a given type, returns false otherwise.
        /// </summary>
        /// <param name="typeMetadata"></param>
        /// <returns></returns>
        public abstract bool HandlesTypeSerialization(TypeSerializationMetadata typeMetadata);

        /// <summary>
        /// Serializes the source C# object directly into the target Udon object and attempt to avoid creating new objects when possible.
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="sourceObject"></param>
        public abstract void WriteWeak(IValueStorage targetObject, object sourceObject);

        /// <summary>
        /// Serializes the source Udon object directly into the target C# object and attempt to avoid creating new objects when possible.
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="sourceObject"></param>
        public abstract void ReadWeak(ref object targetObject, IValueStorage sourceObject);

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

        public abstract System.Type GetUdonStorageType();
    }

    public abstract class Serializer<T> : Serializer
    {
        protected Serializer(TypeSerializationMetadata typeMetadata) : base(typeMetadata)
        {
        }

        public abstract void Write(IValueStorage targetObject, in T sourceObject);

        public override void WriteWeak(IValueStorage targetObject, object sourceObject)
        {
            T sourceObj = (T)sourceObject;
            Write(targetObject, in sourceObj);
        }

        public abstract void Read(ref T targetObject, IValueStorage sourceObject);

        public override void ReadWeak(ref object targetObject, IValueStorage sourceObject)
        {
            T outObj = default;
            Read(ref outObj, sourceObject);
            targetObject = outObj;
        }

        public virtual void Serialize(IValueStorage targetStorage, in T sourceObject)
        {
            Write(targetStorage, in sourceObject);
        }

        public virtual T Deserialize(IValueStorage sourceObject)
        {
            T output = default(T);
            Read(ref output, sourceObject);
            return output;
        }
    }
}
