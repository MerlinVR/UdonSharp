
using JetBrains.Annotations;

namespace UdonSharpEditor
{
    public class ProxySerializationPolicy
    {
        public int MaxSerializationDepth { get; private set; } = int.MaxValue;
        
        /// <summary>
        /// If the policy should collect UnityEngine.Object dependencies. This makes any read/write operations a no-op on behaviours and instead just collects the referenced objects.
        /// </summary>
        public bool CollectDependencies { get; private set; }
        
        /// <summary>
        /// Forces use of the heap rather than the public variables.
        /// Needed because on post process scene can happen while the editor is in play mode, but we need to setup behaviours with heap variables.
        /// </summary>
        public bool IsPreBuildSerialize { get; private set; }
        
        internal static readonly ProxySerializationPolicy CollectRootDependencies = new ProxySerializationPolicy() { MaxSerializationDepth = 1, CollectDependencies = true };
        internal static readonly ProxySerializationPolicy PreBuildSerialize = new ProxySerializationPolicy() { MaxSerializationDepth = 1, IsPreBuildSerialize = true };

        [PublicAPI]
        public static readonly ProxySerializationPolicy Default = new ProxySerializationPolicy() { MaxSerializationDepth = 1 };

        [PublicAPI]
        public static readonly ProxySerializationPolicy RootOnly = new ProxySerializationPolicy() { MaxSerializationDepth = 1 };

        /// <summary>
        /// Copies all properties on all behaviours directly and indirectly referenced by the target behaviour recursively. 
        /// example: Calling this on the root node of a tree where each node is an UdonSharpBehaviour would copy all node data for every node on the tree
        /// </summary>
        [PublicAPI]
        public static readonly ProxySerializationPolicy All = new ProxySerializationPolicy() { MaxSerializationDepth = int.MaxValue };

        /// <summary>
        /// Does not run any copy operations, usually used if you want the GetUdonSharpComponent call to not copy any data
        /// </summary>
        [PublicAPI]
        public static readonly ProxySerializationPolicy NoSerialization = new ProxySerializationPolicy() { MaxSerializationDepth = 0 };

        private ProxySerializationPolicy()
        { }
    }
}
