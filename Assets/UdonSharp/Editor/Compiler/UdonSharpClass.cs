

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler
{
    [System.Serializable]
    public class FieldDefinition
    {
        public FieldDefinition(string name, Type userType, Type systemType, UdonSyncMode? syncMode, bool isSerialized, List<Attribute> attributes)
        {
            Name = name;
            UserType = userType;
            SystemType = systemType;
            SyncMode = syncMode;
            IsSerialized = isSerialized;
            fieldAttributes = attributes;
        }
        
        [field: SerializeField]
        public string Name { get; }
        [field: SerializeField]
        public System.Type UserType { get; }
        [field: SerializeField]
        public System.Type SystemType { get; }
        [field: SerializeField]
        public UdonSyncMode? SyncMode { get; }
        [field: SerializeField]
        public bool IsSerialized { get; }

        public List<Attribute> fieldAttributes;
        
        public UnityEditor.MonoScript userBehaviourSource;

        public T GetAttribute<T>() where T : System.Attribute
        {
            foreach (var attribute in fieldAttributes)
            {
                if (attribute is T)
                    return (T)attribute;
            }

            return null;
        }

        public T[] GetAttributes<T>() where T : System.Attribute
        {
            List<T> attributes = new List<T>();

            foreach (var attribute in fieldAttributes)
            {
                if (attribute is T)
                    attributes.Add((T)attribute);
            }

            return attributes.ToArray();
        }
    }

    public class ClassDefinition
    {
        // Methods and fields should *not* be reflected off of this type, it is not guaranteed to be up to date
        public System.Type userClassType;
        public UnityEditor.MonoScript classScript;

        public List<FieldDefinition> fieldDefinitions = new List<FieldDefinition>();
    }
}