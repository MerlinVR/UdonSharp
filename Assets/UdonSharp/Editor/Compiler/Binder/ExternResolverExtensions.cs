using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal static class ExternResolverExtensions
    {
        static Dictionary<string, System.Reflection.Assembly> assemblyNameLookup;
        static bool ranInit = false;
        static object initLock = new object();

        static void InitResolverExtensions()
        {
            if (ranInit)
                return;

            lock (initLock)
            {
                if (ranInit)
                    return;

                assemblyNameLookup = new Dictionary<string, System.Reflection.Assembly>();

                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.IsDynamic)
                    {
                        string assemblyFileName = Path.GetFileName(assembly.Location);

                        if (!string.IsNullOrWhiteSpace(assemblyFileName) && !assemblyNameLookup.ContainsKey(assemblyFileName))
                            assemblyNameLookup.Add(assemblyFileName, assembly);
                    }
                }

                ranInit = true;
            }
        }

        public static bool IsExternType(this INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.Locations.First().IsInMetadata;
        }

        public static System.Type GetExternType(this INamedTypeSymbol typeSymbol)
        {
            if (!IsExternType(typeSymbol))
                return null;

            InitResolverExtensions();

            ModuleMetadata module = typeSymbol.Locations.First().MetadataModule.GetMetadata();

            string assemblyName = module.Name;

            System.Reflection.Assembly assembly = assemblyNameLookup[assemblyName];

            return assembly.GetType(typeSymbol.ToString());
        }

        public static MethodInfo GetExternMethod(this IMethodSymbol methodSymbol)
        {
            List<System.Type> parameterTypes = new List<Type>();

            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
                parameterTypes.Add(GetExternType((INamedTypeSymbol)parameter.Type));

            System.Type callingType = methodSymbol.ContainingType.GetExternType();

            MethodInfo foundMethod = callingType.GetMethods(BindingFlags.Public | (methodSymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance)).FirstOrDefault(e => e.Name == methodSymbol.Name && Enumerable.SequenceEqual(parameterTypes, e.GetParameters().Select(p => p.ParameterType)));

            return foundMethod;
        }

        public static bool IsUdonSharpBehaviour(this INamedTypeSymbol typeSymbol)
        {
            while (typeSymbol != null)
            {
                System.Type externType = GetExternType(typeSymbol);

                if (externType == typeof(UdonSharpBehaviour))
                    return true;

                typeSymbol = typeSymbol.BaseType;
            }

            return false;
        }
    }
}
