

using System.Collections.Generic;

namespace UdonSharp
{
    public class FieldDefinition
    {
        public FieldDefinition(SymbolDefinition symbol)
        {
            fieldSymbol = symbol;
        }

        public SymbolDefinition fieldSymbol;
    }

    public class ClassDefinition
    {
        // Methods and fields should *not* be reflected off of this type, it is not guaranteed to be up to date
        public System.Type userClassType;

        public List<FieldDefinition> fieldDefinitions = new List<FieldDefinition>();
        public List<MethodDefinition> methodDefinitions = new List<MethodDefinition>();
    }
}