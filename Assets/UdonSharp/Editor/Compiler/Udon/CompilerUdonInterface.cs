
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Symbols;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor;
using VRC.Udon.Graph;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace UdonSharp.Compiler.Udon
{
    internal static class CompilerUdonInterface
    {
        private static HashSet<string> _nodeDefinitionLookup;

        private static Dictionary<string, string> _builtinEventLookup;
        private static Dictionary<string, ImmutableArray<(string, Type)>> _builtinEventArgumentsLookup;
        private static bool _cacheInitRan;
        private static readonly object _cacheInitLock = new object();
        public static ImmutableArray<System.Reflection.Assembly> UdonSharpAssemblies { get; private set; }

        public static void CacheInit()
        {
            if (_cacheInitRan)
                return;

            lock (_cacheInitLock)
            {
                if (_cacheInitRan)
                    return;

                _nodeDefinitionLookup = new HashSet<string>(UdonEditorManager.Instance.GetNodeDefinitions().Select(e => e.fullName));

                _builtinEventLookup = new Dictionary<string, string>();
                _builtinEventArgumentsLookup = new Dictionary<string, ImmutableArray<(string, Type)>>();

                foreach (UdonNodeDefinition nodeDefinition in UdonEditorManager.Instance.GetNodeDefinitions("Event_"))
                {
                    if (nodeDefinition.fullName == "Event_Custom")
                        continue;
                    
                    string eventNameStr = nodeDefinition.fullName.Substring(6);
                    char[] eventName = eventNameStr.ToCharArray();
                    eventName[0] = char.ToLowerInvariant(eventName[0]);

                    if (!_builtinEventLookup.ContainsKey(eventNameStr))
                    {
                        string lowerCasedEventName = new string(eventName);
                        _builtinEventLookup.Add(eventNameStr, "_" + lowerCasedEventName);
                        (string, Type)[] args = new (string, Type)[nodeDefinition.Outputs.Count];

                        for (int i = 0; i < args.Length; ++i)
                        {
                            UdonNodeParameter parameter = nodeDefinition.Outputs[i];
                            string paramNameUpperCased = char.ToUpperInvariant(parameter.name[0]) + parameter.name.Substring(1);

                            args[i] = (lowerCasedEventName + paramNameUpperCased, parameter.type);
                        }
                        
                        _builtinEventArgumentsLookup.Add(eventNameStr, args.ToImmutableArray());
                    }
                    else
                        Debug.LogWarning($"Duplicate event node {nodeDefinition.fullName} found");
                }

                string[] assemblyDefinitionPaths = AssetDatabase.FindAssets($"t:{nameof(UdonSharpAssemblyDefinition)}").Select(AssetDatabase.GUIDToAssetPath).ToArray();
                List<System.Reflection.Assembly> assemblies = new List<System.Reflection.Assembly>();

                System.Reflection.Assembly[] allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (string definitionPath in assemblyDefinitionPaths)
                {
                    var assemblyDefinition = AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(definitionPath);
                    if (assemblyDefinition == null || assemblyDefinition.sourceAssembly == null) 
                        continue;
                    
                    var sourceAssembly = assemblyDefinition.sourceAssembly;

                    foreach (var assembly in allAssemblies)
                    {
                        if (assembly.IsDynamic || assembly.Location.Length <= 0 ||
                            assembly.Location.StartsWith("data")) 
                            continue;

                        if (assembly.GetName().Name != sourceAssembly.name) 
                            continue;
                            
                        assemblies.Add(assembly);
                        break;
                    }
                }

                UdonSharpAssemblies = assemblies.ToImmutableArray();

                _cacheInitRan = true;
            }
        }

        public static bool IsValidUdonExtern(string externStr)
        {
            CacheInit();
            return _nodeDefinitionLookup.Contains(externStr);
        }

        public static bool IsUdonEvent(string eventName)
        {
            CacheInit();
            return _builtinEventLookup.ContainsKey(eventName);
        }

        public static string GetUdonEventName(string eventName)
        {
            CacheInit();
            
            if (!_builtinEventLookup.TryGetValue(eventName, out var udonEventName))
                throw new ArgumentException("Event must be an Udon event");

            return udonEventName;
        }

        public static ImmutableArray<(string, Type)> GetUdonEventArgs(string eventName)
        {
            CacheInit();

            if (_builtinEventArgumentsLookup.TryGetValue(eventName, out ImmutableArray<(string, Type)> foundArgs))
                return foundArgs;

            return ImmutableArray<(string, Type)>.Empty;
        }

        public static string GetUdonTypeName(TypeSymbol externSymbol)
        {
            if (externSymbol is TypeParameterSymbol)
                return "T";
            
            return GetUdonTypeName(externSymbol.UdonType.SystemType);
        }

        internal enum FieldAccessorType
        {
            Get,
            Set,
        }
        
        public static string GetUdonAccessorName(Symbol symbol, TypeSymbol fieldType, FieldAccessorType accessorType)
        {
            Type containingType = UdonSharpUtils.RemapBaseType(symbol.RoslynSymbol.ContainingType.GetExternType());

            string functionNamespace = SanitizeTypeName(containingType.FullName).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver").Replace("UdonSharpUdonSharpBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
            string methodName = $"__{(accessorType == FieldAccessorType.Get ? "get" : "set")}_{symbol.Name.Trim('_')}";

            string paramStr = $"__{GetUdonTypeName(fieldType)}";

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}";

            if (!IsExposedToUdon(finalFunctionSig))
            {
                throw new Exception($"Accessor {finalFunctionSig} is not exposed in Udon");
            }

            return finalFunctionSig;
        }

        public static string SanitizeTypeName(string typeName)
        {
            return typeName.Replace(",", "")
                .Replace(".", "")
                .Replace("[]", "Array")
                .Replace("&", "Ref")
                .Replace("+", "");
        }

        public static string GetMethodTypeName(TypeSymbol type)
        {
            Type methodSourceType = type.UdonType.SystemType;

            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            return CompilerUdonInterface.SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
        }

        private static string GetUdonTypeName(Type externType)
        {
            string externTypeName = externType.GetNameWithoutGenericArity();
            while (externType.IsArray || externType.IsByRef)
            {
                externType = externType.GetElementType();
            }

            string typeNamespace = externType.Namespace;

            // Handle nested type names (+ sign in names)
            if (externType.DeclaringType != null)
            {
                string declaringTypeNamespace = "";

                Type declaringType = externType.DeclaringType;

                while (declaringType != null)
                {
                    declaringTypeNamespace = $"{externType.DeclaringType.Name}.{declaringTypeNamespace}";
                    declaringType = declaringType.DeclaringType;
                }

                typeNamespace += $".{declaringTypeNamespace}";
            }

            if (externTypeName == "T" || externTypeName == "T[]")
                typeNamespace = "";
        
            string fullTypeName = SanitizeTypeName($"{typeNamespace}.{externTypeName}");

            foreach (Type genericType in externType.GetGenericArguments())
            {
                fullTypeName += GetUdonTypeName(genericType);
            }

            // Seems like Udon does shortening for this specific type somewhere
            if (fullTypeName == "SystemCollectionsGenericListT")
            {
                fullTypeName = "ListT";
            }
            else if (fullTypeName == "SystemCollectionsGenericIEnumerableT")
            {
                fullTypeName = "IEnumerableT";
            }

            // fullTypeName = fullTypeName.Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            return fullTypeName;
        }

        public static bool IsExposedToUdon(string signature)
        {
            CacheInit();
            return _nodeDefinitionLookup.Contains(signature);
        }
    }
}
