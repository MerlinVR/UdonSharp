

using System.Collections.Generic;

namespace UdonSharp.Compiler
{
    [System.Serializable]
    public class FieldDefinition
    {
        public FieldDefinition(SymbolDefinition symbol)
        {
            fieldSymbol = symbol;
            fieldAttributes = new List<System.Attribute>();
        }

        public SymbolDefinition fieldSymbol;

        public List<System.Attribute> fieldAttributes;
        
        public UnityEditor.MonoScript userBehaviourSource;

        public T GetAttribute<T>() where T : System.Attribute
        {
            System.Type attributeType = typeof(T);

            foreach (var attribute in fieldAttributes)
            {
                if (attribute is T)
                    return (T)attribute;
            }

            return null;
        }

        public T[] GetAttributes<T>() where T : System.Attribute
        {
            System.Type attributeType = typeof(T);

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
        public List<MethodDefinition> methodDefinitions = new List<MethodDefinition>();
        public List<PropertyDefinition> propertyDefinitions = new List<PropertyDefinition>();
    }
}