
namespace UdonSharp.Compiler
{
    [System.Flags]
    public enum PropertyDeclFlags
    {
        None = 0,
        Public = 1,
        Private = 2,
    }

    public class BackingFieldDefinition
    {
        public System.Type type;
        public bool synced;
        public UdonSyncMode syncMode;
        public string backingFieldName;
        public SymbolDefinition fieldSymbol;
    }

    public class SetterDefinition
    {
        public PropertyDeclFlags declarationFlags;
        public System.Type type;
        public string symbolName;
        public SymbolDefinition paramSymbol;
        public BackingFieldDefinition backingField;
        public string accessorName;
        public JumpLabel entryPoint;
        public JumpLabel userCallStart;
        public JumpLabel returnPoint;
        public JumpLabel returnSymbol;
    }

    public class GetterDefinition
    {
        public PropertyDeclFlags declarationFlags;
        public System.Type type;
        public BackingFieldDefinition backingField;
        public string accessorName;
        public JumpLabel entryPoint;
        public JumpLabel userCallStart;
        public JumpLabel returnPoint;
        public SymbolDefinition returnSymbol;
    }

    public class PropertyDefinition
    {
        public PropertyDeclFlags declarationFlags;
        public System.Type type;
        public string originalPropertyName;
        public SetterDefinition setter;
        public GetterDefinition getter;
    }
}