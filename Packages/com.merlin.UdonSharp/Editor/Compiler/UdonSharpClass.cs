
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler
{
    [Serializable]
    public class FieldDefinition
    {
        public FieldDefinition(string name, Type userType, Type systemType, UdonSyncMode? syncMode, bool isSerialized, List<Attribute> attributes)
        {
            Name = name;
            UserType = userType;
            SystemType = systemType;
            SyncMode = syncMode;
            IsSerialized = isSerialized;
            _fieldAttributes = attributes;
        }
        
        [field: SerializeField]
        public string Name { get; }
        
        [field: SerializeField]
        public Type UserType { get; }
        
        [field: SerializeField]
        public Type SystemType { get; }
        
        [field: SerializeField]
        public UdonSyncMode? SyncMode { get; }
        
        [field: SerializeField]
        public bool IsSerialized { get; }
        
        [SerializeField]
        private List<Attribute> _fieldAttributes;

        public T GetAttribute<T>() where T : Attribute
        {
            foreach (var attribute in _fieldAttributes)
            {
                if (attribute is T attributeT)
                    return attributeT;
            }

            return null;
        }

        public T[] GetAttributes<T>() where T : Attribute
        {
            List<T> attributes = new List<T>();

            foreach (var attribute in _fieldAttributes)
            {
                if (attribute is T attributeT)
                    attributes.Add(attributeT);
            }

            return attributes.ToArray();
        }
    }
}