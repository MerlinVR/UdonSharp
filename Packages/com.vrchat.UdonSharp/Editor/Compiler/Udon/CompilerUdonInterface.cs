
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;
using UdonSharpEditor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.EditorBindings;
using VRC.Udon.Graph;
using VRC.Udon.UAssembly.Assembler;
using VRC.Udon.UAssembly.Interfaces;

namespace UdonSharp.Compiler.Udon
{
    // [InitializeOnLoad]
    internal static class CompilerUdonInterface
    {
        private static HashSet<string> _nodeDefinitionLookup;

        private static Dictionary<string, string> _builtinEventLookup;
        private static Dictionary<string, ImmutableArray<(string, Type)>> _builtinEventArgumentsLookup;
        private static bool _cacheInitRan;
        private static bool _assemblyInitRan;
        private static readonly object _cacheInitLock = new object();
        
        private static ImmutableArray<System.Reflection.Assembly> _udonSharpAssemblies;

        public static ImmutableArray<System.Reflection.Assembly> UdonSharpAssemblies
        {
            get
            {
                AssemblyCacheInit();
                return _udonSharpAssemblies;
            }
            private set => _udonSharpAssemblies = value;
        }
        
        private static HashSet<System.Reflection.Assembly> ExternAssemblySet { get; set; }

        private static ImmutableArray<UdonSharpAssemblyDefinition> _udonSharpAssemblyDefinitions;

        public static ImmutableArray<UdonSharpAssemblyDefinition> UdonSharpAssemblyDefinitions
        {
            get
            {
                AssemblyCacheInit();
                return _udonSharpAssemblyDefinitions;
            }
            private set => _udonSharpAssemblyDefinitions = value;
        }
        
        private static UdonEditorInterface _editorInterfaceInstance;

        // static CompilerUdonInterface()
        // {
        //     AssemblyCacheInit();
        //     CacheInit();
        // }

        internal static void ResetAssemblyCache()
        {
            _assemblyInitRan = false;
        }

        internal static void CacheInit()
        {
            if (_cacheInitRan)
                return;

            lock (_cacheInitLock)
            {
                if (_cacheInitRan)
                    return;

                // Lock until editor interface initialized if it isn't to prevent race conditions setting up duplicate UdonEditorInterface for our use
                UdonEditorManager.Instance.GetNodeRegistries();
                
                // The heap size is determined by the symbol count + the unique extern string count
                _editorInterfaceInstance = new UdonEditorInterface();
                _editorInterfaceInstance.AddTypeResolver(new UdonBehaviourTypeResolver()); // todo: can be removed with SDK's >= VRCSDK-UDON-2020.06.15.14.08_Public

                _nodeDefinitionLookup = new HashSet<string>(_editorInterfaceInstance.GetNodeDefinitions().Select(e => e.fullName));

                _builtinEventLookup = new Dictionary<string, string>();
                _builtinEventArgumentsLookup = new Dictionary<string, ImmutableArray<(string, Type)>>();

                foreach (UdonNodeDefinition nodeDefinition in _editorInterfaceInstance.GetNodeDefinitions("Event_"))
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

                _cacheInitRan = true;
            }
        }

        internal static void AssemblyCacheInit()
        {
            if (_assemblyInitRan)
                return;

            lock (_cacheInitLock)
            {
                if (_assemblyInitRan)
                    return;
                
                string[] assemblyDefinitionPaths = AssetDatabase.FindAssets($"t:{nameof(UdonSharpAssemblyDefinition)}").Select(AssetDatabase.GUIDToAssetPath).ToArray();
                List<System.Reflection.Assembly> assemblies = new List<System.Reflection.Assembly>();
                List<UdonSharpAssemblyDefinition> assemblyDefinitions = new List<UdonSharpAssemblyDefinition>();

                System.Reflection.Assembly[] allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (string definitionPath in assemblyDefinitionPaths)
                {
                    UdonSharpAssemblyDefinition assemblyDefinition = AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(definitionPath);
                    if (assemblyDefinition == null || assemblyDefinition.sourceAssembly == null) 
                        continue;
                    
                    assemblyDefinitions.Add(assemblyDefinition);
                    
                    AssemblyDefinitionAsset sourceAssembly = assemblyDefinition.sourceAssembly;

                    foreach (System.Reflection.Assembly assembly in allAssemblies)
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

                System.Reflection.Assembly cSharpAssembly = allAssemblies.FirstOrDefault(e => e.GetName().Name == "Assembly-CSharp");

                if (cSharpAssembly != null)
                {
                    assemblies.Add(cSharpAssembly);
                }
                else
                {
                    UdonSharpUtils.LogWarning("No Assembly-CSharp assembly found");
                }

                UdonSharpAssemblies = assemblies.ToImmutableArray();
                UdonSharpAssemblyDefinitions = assemblyDefinitions.ToImmutableArray();
                HashSet<System.Reflection.Assembly> udonSharpAssemblySet = new HashSet<System.Reflection.Assembly>(_udonSharpAssemblies);

                HashSet<System.Reflection.Assembly> externAssemblies = new HashSet<System.Reflection.Assembly>();

                foreach (System.Reflection.Assembly assembly in allAssemblies)
                {
                    if (assembly.IsDynamic || assembly.Location.Length <= 0 || 
                        assembly.Location.StartsWith("data")) 
                        continue;

                    if (!udonSharpAssemblySet.Contains(assembly))
                        externAssemblies.Add(assembly);
                }

                ExternAssemblySet = externAssemblies;

                _assemblyInitRan = true;
            }
        }

        private static ConcurrentBag<(IUAssemblyAssembler, UdonSharp.HeapFactory)> _usableAssemblers = new ConcurrentBag<(IUAssemblyAssembler, UdonSharp.HeapFactory)>();
        private static readonly FieldInfo _typeResolverGroupField = typeof(UdonEditorInterface).GetField("_typeResolverGroup", BindingFlags.NonPublic | BindingFlags.Instance);

        public static IUdonProgram Assemble(string assembly, uint heapSize)
        {
            CacheInit();

            IUAssemblyAssembler assembler;
            UdonSharp.HeapFactory factory;
            
            if (_usableAssemblers.TryTake(out var foundAssembler))
            {
                assembler = foundAssembler.Item1;
                factory = foundAssembler.Item2;
            }
            else
            {
                factory = new HeapFactory();
                assembler = new UAssemblyAssembler(factory, (IUAssemblyTypeResolver)_typeResolverGroupField.GetValue(_editorInterfaceInstance));
            }

            factory.FactoryHeapSize = heapSize;
            IUdonProgram program = assembler.Assemble(assembly);
            
            _usableAssemblers.Add((assembler, factory));

            return program;
        }

        public static bool IsExternType(Type type)
        {
            AssemblyCacheInit();
            return ExternAssemblySet.Contains(type.Assembly);
        }

        public static bool IsUdonEventName(string name)
        {
            CacheInit();
            
            return _builtinEventLookup.ContainsKey(name);
        }

        public static bool IsUdonEvent(MethodSymbol method)
        {
            CacheInit();
            
            // ReSharper disable once InvokeAsExtensionMethod
            if (_builtinEventLookup.ContainsKey(method.Name) &&
                !method.Parameters.Any(e => e.IsByRef)) // Builtin events should never have out/ref params
            {
                if (method.Parameters.Length == 0 || // Avoid breaking older programs that omit the argument for these events even though they shouldn't. This also somewhat mirrors Unity's behavior as it will fire an event named OnTriggerEnter() without any parameters.
                    Enumerable.SequenceEqual(method.Parameters.Select(e => e.Type.UdonType.SystemType), GetUdonEventArgs(method.Name).Select(e => e.Item2)))
                {
                    return true;
                }
            }

            return false;
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
        
        public static string GetUdonAccessorName(FieldSymbol symbol, FieldAccessorType accessorType)
        {
            if (!TypeSymbol.TryGetSystemType(symbol.RoslynSymbol.ContainingType, out Type containingType))
                throw new InvalidOperationException("Containing type must be a valid extern");
            
            containingType = UdonSharpUtils.RemapBaseType(containingType);

            string functionNamespace = SanitizeTypeName(containingType.FullName).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver").Replace("UdonSharpUdonSharpBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
            string methodName = $"__{(accessorType == FieldAccessorType.Get ? "get" : "set")}_{symbol.Name.Trim('_')}";

            string paramStr = $"__{GetUdonTypeName(symbol.Type)}";

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}";

            if (!IsExposedToUdon(finalFunctionSig))
            {
                throw new Exception($"Accessor {finalFunctionSig} is not exposed in Udon");
            }

            return finalFunctionSig;
        }
        
        public static string GetUdonAccessorName(FieldInfo fieldInfo, FieldAccessorType accessorType)
        {
            Type containingType = fieldInfo.DeclaringType;
            
            containingType = UdonSharpUtils.RemapBaseType(containingType);

            string functionNamespace = SanitizeTypeName(containingType.FullName).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver").Replace("UdonSharpUdonSharpBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
            string methodName = $"__{(accessorType == FieldAccessorType.Get ? "get" : "set")}_{fieldInfo.Name.Trim('_')}";

            string paramStr = $"__{GetUdonTypeName(fieldInfo.FieldType)}";

            return $"{functionNamespace}.{methodName}{paramStr}";
        }

        internal static string GetUdonMethodName(ExternMethodSymbol methodSymbol, AbstractPhaseContext context)
        {
            Type methodSourceType = methodSymbol.ContainingType.UdonType.SystemType;
            IMethodSymbol roslynSymbol = methodSymbol.RoslynSymbol;

            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            string functionNamespace = SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            string methodName = $"__{methodSymbol.Name.Trim('_').TrimStart('.')}";
            ImmutableArray<IParameterSymbol> parameters = roslynSymbol.Parameters;

            string paramStr = "";
            
            if (parameters.Length > 0)
            {
                paramStr = "_"; // Arg separator
            
                foreach (IParameterSymbol parameter in parameters)
                {
                    paramStr += $"_{GetUdonTypeName(context.GetTypeSymbol(parameter.Type))}";
                    if (parameter.RefKind != RefKind.None)
                        paramStr += "Ref";
                }
            }
            else if (methodSymbol.IsConstructor)
                paramStr = "__";

            string returnStr = "";

            if (!methodSymbol.IsConstructor)
            {
                TypeSymbol returnType = context.GetTypeSymbol(roslynSymbol.ReturnType);

                if (returnType.IsExtern &&
                    returnType.RoslynSymbol?.ContainingNamespace?.ToString() == "VRC.SDKBase")
                    returnStr = $"__{GetUdonTypeName(((ExternTypeSymbol)returnType).SystemType)}";
                else
                    returnStr = $"__{GetUdonTypeName(returnType)}";
            }
            else
            {
                returnStr = $"__{GetUdonTypeName(methodSymbol.ContainingType)}";
            }

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}{returnStr}";

            return finalFunctionSig;
        }
        
        // Any changes to the above symbol based method should be ported to this
        public static string GetUdonMethodName(MethodBase methodInfo)
        {
            Type methodSourceType = methodInfo.DeclaringType;
            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            string functionNamespace = SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            string methodName = $"__{methodInfo.Name.Trim('_').TrimStart('.')}";
            ParameterInfo[] parameters = methodInfo.GetParameters();

            string paramStr = "";
            
            if (parameters.Length > 0)
            {
                paramStr = "_"; // Arg separator
            
                foreach (ParameterInfo parameter in parameters)
                {
                    paramStr += $"_{GetUdonTypeName(parameter.ParameterType)}";
                }
            }
            else if (methodInfo.IsConstructor)
                paramStr = "__";

            string returnStr = "";

            if (!methodInfo.IsConstructor)
            {
                returnStr = $"__{GetUdonTypeName(((MethodInfo)methodInfo).ReturnType)}";
            }
            else
            {
                returnStr = $"__{GetUdonTypeName(methodSourceType)}";
            }

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}{returnStr}";

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

            return SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
        }

        private static ConcurrentDictionary<Type, string> _typeNameMap = new ConcurrentDictionary<Type, string>();

        public static string GetUdonTypeName(Type externType)
        {
            if (_typeNameMap.TryGetValue(externType, out string foundTypeName))
                return foundTypeName;

            Type originalType = externType;
            
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

            _typeNameMap.TryAdd(originalType, fullTypeName);
            
            return fullTypeName;
        }

        public static bool IsExposedToUdon(string signature)
        {
            CacheInit();
            return _nodeDefinitionLookup.Contains(signature);
        }
    }
}
