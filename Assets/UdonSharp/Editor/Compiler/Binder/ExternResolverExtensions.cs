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

        public static bool IsExternType(this ITypeSymbol typeSymbol)
        {
            return typeSymbol.Locations.FirstOrDefault()?.IsInMetadata ?? false;
        }

        private static readonly SymbolDisplayFormat _externFullTypeFormat =
            new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        public static Type GetExternType(this ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
                return namedType.GetExternType();
            if (typeSymbol is IArrayTypeSymbol arrayType)
                return arrayType.GetExternType();

            throw new ArgumentException("Must give a named type, or an array type");
        }
        
        public static Type GetExternType(this INamedTypeSymbol typeSymbol)
        {
            if (!IsExternType(typeSymbol))
                return null;

            InitResolverExtensions();

            ModuleMetadata module = typeSymbol.Locations.First().MetadataModule.GetMetadata();
            
            System.Reflection.Assembly assembly = assemblyNameLookup[module.Name];
            string typeName = typeSymbol.ToDisplayString(_externFullTypeFormat);
            if (typeSymbol.IsGenericType)
                typeName += $"`{typeSymbol.TypeArguments.Length}";
                
            Type foundType = assembly.GetType(typeName);

            if (foundType == null)
                throw new InvalidOperationException("foundType should not be null");

            return foundType;
        }
        
        public static Type GetExternType(this IArrayTypeSymbol typeSymbol)
        {
            InitResolverExtensions();

            int arrayDepth = 0;
            while (true)
            {
                ++arrayDepth;
                
                if (typeSymbol.ElementType.TypeKind != TypeKind.Array)
                    break;

                typeSymbol = (IArrayTypeSymbol) typeSymbol.ElementType;
            }

            if (!IsExternType(typeSymbol.ElementType))
                return null;

            Type foundType = GetExternType((INamedTypeSymbol) typeSymbol.ElementType);

            while (arrayDepth > 0)
            {
                foundType = foundType.MakeArrayType();
                arrayDepth--;
            }

            return foundType;
        }

        public static MethodInfo GetExternMethod(this IMethodSymbol methodSymbol)
        {
            List<Type> parameterTypes = new List<Type>();

            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
                parameterTypes.Add(GetExternType((INamedTypeSymbol)parameter.Type));

            Type callingType = methodSymbol.ContainingType.GetExternType();

            MethodInfo foundMethod = callingType.GetMethods(BindingFlags.Public | (methodSymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance)).FirstOrDefault(e => e.Name == methodSymbol.Name && Enumerable.SequenceEqual(parameterTypes, e.GetParameters().Select(p => p.ParameterType)));

            return foundMethod;
        }

        public static bool IsUdonSharpBehaviour(this INamedTypeSymbol typeSymbol)
        {
            while (typeSymbol != null)
            {
                Type externType = GetExternType(typeSymbol);

                if (externType == typeof(UdonSharpBehaviour))
                    return true;

                typeSymbol = typeSymbol.BaseType;
            }

            return false;
        }
    }
}
