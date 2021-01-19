
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
        
        internal static readonly ProxySerializationPolicy AllWithCreateUndo = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.CreateWithUndo };
        internal static readonly ProxySerializationPolicy AllWithCreate = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Create };

        [PublicAPI]
        public static readonly ProxySerializationPolicy Default = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Null, MaxSerializationDepth = 1 };

        [PublicAPI]
        public static readonly ProxySerializationPolicy RootOnly = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Null, MaxSerializationDepth = 1 };

        /// <summary>
        /// Copies all properties on all behaviours directly and indirectly referenced by the target behaviour recursively. 
        /// example: Calling this on the root node of a tree where each node is an UdonSharpBehaviour would copy all node data for every node on the tree
        /// </summary>
        [PublicAPI]
        public static readonly ProxySerializationPolicy All = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Null, MaxSerializationDepth = int.MaxValue };

        /// <summary>
        /// Does not run any copy operations, usually used if you want the GetUdonSharpComponent call to not copy any data
        /// </summary>
        [PublicAPI]
        public static readonly ProxySerializationPolicy NoSerialization = new ProxySerializationPolicy() { ChildProxyMode = ChildProxyCreateMode.Null, MaxSerializationDepth = 0 };

        private ProxySerializationPolicy()
        { }
    }
}
