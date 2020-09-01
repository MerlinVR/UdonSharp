
using JetBrains.Annotations;

namespace UdonSharpEditor
{
    public class ProxySerializationPolicy
    {
        public enum ChildProxyCreateMode
        {
            Null, // Leaves null references in the place of child proxies
            Create, // Creates child proxies
            CreateWithUndo, // Creates child proxies with undo step
        }

        public ChildProxyCreateMode ChildProxyMode { get; private set; } = ChildProxyCreateMode.Create;
        public int MaxSerializationDepth { get; private set; } = int.MaxValue;
        
        internal static readonly ProxySerializationPolicy AllWithUndo = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.CreateWithUndo };

        [PublicAPI]
        public static readonly ProxySerializationPolicy Default = new ProxySerializationPolicy();

        [PublicAPI]
        public static readonly ProxySerializationPolicy RootOnly = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Null, MaxSerializationDepth = 1 };

        [PublicAPI]
        public static readonly ProxySerializationPolicy NoSerialization = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Null, MaxSerializationDepth = 0 };

        private ProxySerializationPolicy()
        { }
    }
}
