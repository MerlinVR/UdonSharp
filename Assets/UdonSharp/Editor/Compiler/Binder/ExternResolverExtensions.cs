using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal static class ExternResolverExtensions
    {
        private static Dictionary<string, System.Reflection.Assembly> _assemblyNameLookup;
        private static bool _ranInit;
        private static readonly object _initLock = new object();

        private static void InitResolverExtensions()
        {
            if (_ranInit)
                return;

            lock (_initLock)
            {
                if (_ranInit)
                    return;

                _assemblyNameLookup = new Dictionary<string, System.Reflection.Assembly>();

                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.IsDynamic)
                    {
                        string assemblyFileName = Path.GetFileName(assembly.Location);

                        if (!string.IsNullOrWhiteSpace(assemblyFileName) && !_assemblyNameLookup.ContainsKey(assemblyFileName))
                            _assemblyNameLookup.Add(assemblyFileName, assembly);
                    }
                }

                _ranInit = true;
            }
        }

        public static System.Reflection.Assembly GetAssemblyFromMetadata(this ModuleMetadata metadata)
        {
            InitResolverExtensions();

            if (metadata == null)
                return null;

            return _assemblyNameLookup.TryGetValue(metadata.Name, out var asm) ? asm : null;
        }

        public static System.Reflection.Assembly GetExternAssembly(this INamedTypeSymbol typeSymbol)
        {
            if (!typeSymbol.IsExternType())
                return null;

            return typeSymbol.Locations.FirstOrDefault()?.MetadataModule?.GetMetadata().GetAssemblyFromMetadata();
        }

        public static bool IsExternType(this ITypeSymbol typeSymbol)
        {
            return typeSymbol.Locations.FirstOrDefault()?.IsInMetadata ?? false;
        }

        public static bool IsUdonSharpBehaviour(this INamedTypeSymbol typeSymbol)
        {
            while (typeSymbol != null)
            {
                if (!TypeSymbol.TryGetSystemType(typeSymbol, out var externType))
                    return false;
                
                if (externType == typeof(UdonSharpBehaviour))
                    return true;

                typeSymbol = typeSymbol.BaseType;
            }

            return false;
        }
    }
}
