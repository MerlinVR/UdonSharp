
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Compiler.Udon;
using UdonSharp.Core;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UdonSharp.Compiler
{
    using SyntaxTree = Microsoft.CodeAnalysis.SyntaxTree;

    internal enum DiagnosticSeverity
    {
        Log,
        Warning,
        Error,
    }
    
    internal class ModuleBinding
    {
        public SyntaxTree tree;
        public string filePath;
        public string sourceText;
        public SemanticModel semanticModel; // Populated after Roslyn compile
        public AssemblyModule assemblyModule;
        public UdonSharpProgramAsset programAsset;
        public Type programClass;
        public MonoScript programScript;
        public BindContext binding;
        public string assembly;
    }
    
    internal class CompilationContext
    {
        internal class CompileDiagnostic
        {
            public DiagnosticSeverity Severity { get; }
            public Location Location { get; }
            public string Message { get; }

            public CompileDiagnostic(DiagnosticSeverity severity, Location location, string message)
            {
                Severity = severity;
                Location = location;
                Message = message;
            }
        }
        
        /// <summary>
        /// High level phase of the compiler
        /// </summary>
        public enum CompilePhase
        {
            Setup,
            RoslynCompile,
            /// <summary>
            /// Roslyn has run its compilation and error checking, we are now binding all symbol references and solving for dependencies.
            /// </summary>
            Bind,
            /// <summary>
            /// Emitting the assembly modules' uasm instructions and serialized heap values for each program
            /// </summary>
            Emit,
            Count,
        }
        
        public CompilePhase CurrentPhase { get; set; }
        
        public float PhaseProgress { get; set; }

        private int _errorCount;

        public int ErrorCount => _errorCount;
        
        public CSharpCompilation RoslynCompilation { get; set; }

        public ConcurrentBag<CompileDiagnostic> Diagnostics { get; } = new ConcurrentBag<CompileDiagnostic>();
        
        public ModuleBinding[] ModuleBindings { get; private set; }
        
        private ConcurrentDictionary<ITypeSymbol, TypeSymbol> _typeSymbolLookup = new ConcurrentDictionary<ITypeSymbol, TypeSymbol>();

        public UdonSharpCompileOptions Options { get; }
        
        private Dictionary<TypeSymbol, ImmutableArray<TypeSymbol>> _inheritedTypes;

        public CompilationContext(UdonSharpCompileOptions options)
        {
            Options = options;
        }

        public TypeSymbol GetTypeSymbol(ITypeSymbol type, AbstractPhaseContext context)
        {
            TypeSymbol typeSymbol = _typeSymbolLookup.GetOrAdd(type, (key) => TypeSymbolFactory.CreateSymbol(type, context));

            return typeSymbol;
        }

        public TypeSymbol GetUdonTypeSymbol(ITypeSymbol type, AbstractPhaseContext context)
        {
            if (!TypeSymbol.TryGetSystemType(type, out var systemType))
                throw new InvalidOperationException("foundType should not be null");
            
            systemType = UdonSharpUtils.UserTypeToUdonType(systemType);
            
            return GetTypeSymbol(systemType, context);
        }

        public TypeSymbol GetTypeSymbol(Type systemType, AbstractPhaseContext context)
        {
            int arrayDepth = 0;
            while (systemType.IsArray)
            {
                arrayDepth++;
                systemType = systemType.GetElementType();
            }
            
            ITypeSymbol typeSymbol = RoslynCompilation.GetTypeByMetadataName(systemType.FullName);

            for (int i = 0; i < arrayDepth; ++i)
                typeSymbol = RoslynCompilation.CreateArrayTypeSymbol(typeSymbol, 1);
            
            return GetTypeSymbol(typeSymbol, context);
        }

        public TypeSymbol GetTypeSymbol(SpecialType type, AbstractPhaseContext context)
        {
            return GetTypeSymbol(RoslynCompilation.GetSpecialType(type), context);
        }

        public Symbol GetSymbol(ISymbol sourceSymbol, AbstractPhaseContext context)
        {
            if (sourceSymbol == null)
                throw new NullReferenceException("Source symbol cannot be null");
            
            if (sourceSymbol is ITypeSymbol typeSymbol)
                return GetTypeSymbol(typeSymbol, context);

            if (sourceSymbol.ContainingType != null)
                return GetTypeSymbol(sourceSymbol.ContainingType, context).GetMember(sourceSymbol, context);

            throw new InvalidOperationException($"Could not get symbol for {sourceSymbol}");
        }

        public SemanticModel GetSemanticModel(SyntaxTree modelTree)
        {
            return RoslynCompilation.GetSemanticModel(modelTree);
        }

        public void AddDiagnostic(DiagnosticSeverity severity, SyntaxNode node, string message)
        {
            Diagnostics.Add(new CompileDiagnostic(severity, node?.GetLocation(), message));

            if (severity == DiagnosticSeverity.Error)
                Interlocked.Increment(ref _errorCount);
        }
        
        public void AddDiagnostic(DiagnosticSeverity severity, Location location, string message)
        {
            Diagnostics.Add(new CompileDiagnostic(severity, location, message));
            
            if (severity == DiagnosticSeverity.Error)
                Interlocked.Increment(ref _errorCount);
        }

        public ModuleBinding[] LoadSyntaxTreesAndCreateModules(IEnumerable<string> sourcePaths, string[] scriptingDefines)
        {
            ConcurrentBag<ModuleBinding> syntaxTrees = new ConcurrentBag<ModuleBinding>();

            Parallel.ForEach(sourcePaths, (currentSource) =>
            {
                string programSource = UdonSharpUtils.ReadFileTextSync(currentSource);

                var programSyntaxTree = CSharpSyntaxTree.ParseText(programSource, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithPreprocessorSymbols(scriptingDefines).WithLanguageVersion(LanguageVersion.CSharp7_3));

                syntaxTrees.Add(new ModuleBinding() { tree = programSyntaxTree, filePath = currentSource, sourceText = programSource });
            });
            
            ModuleBindings = syntaxTrees.ToArray();
            
            return ModuleBindings;
        }
        
        private static Dictionary<bool, IEnumerable<string>> _scriptPathCache = new Dictionary<bool, IEnumerable<string>>();

        public static IEnumerable<string> GetAllFilteredSourcePaths(bool isEditorBuild)
        {
            if (_scriptPathCache.TryGetValue(isEditorBuild, out var cachedPaths))
                return cachedPaths;
            
            HashSet<string> assemblySourcePaths = new HashSet<string>();

            foreach (UnityEditor.Compilation.Assembly asm in CompilationPipeline.GetAssemblies(isEditorBuild ? AssembliesType.Editor : AssembliesType.PlayerWithoutTestAssemblies))
            {
                if (asm.name == "Assembly-CSharp" || IsUdonSharpAssembly(asm.name))
                    assemblySourcePaths.UnionWith(asm.sourceFiles);
            }

            IEnumerable<string> paths =  UdonSharpSettings.FilterBlacklistedPaths(assemblySourcePaths);
            
            _scriptPathCache.Add(isEditorBuild, paths);

            return paths;
        }
        
        internal static void ResetAssemblyCaches()
        {
            _scriptPathCache.Clear();
            _udonSharpAssemblyNames = null;
            CompilerUdonInterface.ResetAssemblyCache(); 
        }
        
        public static IEnumerable<MonoScript> GetAllFilteredScripts(bool isEditorBuild)
        {
            return GetAllFilteredSourcePaths(isEditorBuild).Select(AssetDatabase.LoadAssetAtPath<MonoScript>).Where(e => e != null).ToArray();
        }

        private static HashSet<string> _udonSharpAssemblyNames;

        private static bool IsUdonSharpAssembly(string assemblyName)
        {
            if (_udonSharpAssemblyNames == null)
            {
                _udonSharpAssemblyNames = new HashSet<string>();
                foreach (UdonSharpAssemblyDefinition asmDef in CompilerUdonInterface.UdonSharpAssemblyDefinitions)
                {
                    _udonSharpAssemblyNames.Add(asmDef.sourceAssembly.name);
                }
            }

            return _udonSharpAssemblyNames.Contains(assemblyName);
        }

        private static List<MetadataReference> _metadataReferences;

        public static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            if (_metadataReferences != null) return _metadataReferences;
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            _metadataReferences = new List<MetadataReference>();

            foreach (var assembly in assemblies)
            {
                if (assembly.IsDynamic || assembly.Location.Length <= 0 ||
                    assembly.Location.StartsWith("data")) 
                    continue;
                
                if (assembly.GetName().Name == "Assembly-CSharp" ||
                    assembly.GetName().Name == "Assembly-CSharp-Editor")
                {
                    continue;
                }

                if (IsUdonSharpAssembly(assembly.GetName().Name))
                    continue;

                PortableExecutableReference executableReference = null;

                try
                {
                    executableReference = MetadataReference.CreateFromFile(assembly.Location);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unable to locate assembly {assembly.Location} Exception: {e}");
                }

                if (executableReference != null)
                    _metadataReferences.Add(executableReference);
            }

            return _metadataReferences;
        }
        
        public string TranslateLocationToFileName(Location location)
        {
            if (location == null) return null;
            
            SyntaxTree locationSyntaxTree = location.SourceTree;

            if (locationSyntaxTree == null) return null;

            ModuleBinding binding = ModuleBindings.FirstOrDefault(e => e.tree == locationSyntaxTree);

            if (binding == null) return null;

            return binding.filePath;
        }

        public class MethodExportLayout
        {
            public MethodSymbol Method { get; }
            
            public string ExportMethodName { get; }
            
            public string ReturnExportName { get; }
            
            public string[] ParameterExportNames { get; }

            public MethodExportLayout(MethodSymbol method, string exportMethodName, string returnExportName, string[] parameterExportNames)
            {
                Method = method;
                ExportMethodName = exportMethodName;
                ReturnExportName = returnExportName;
                ParameterExportNames = parameterExportNames;
            }
        }

        private class TypeLayout
        {
            private ImmutableDictionary<MethodSymbol, MethodExportLayout> MethodLayouts { get; }
            public ImmutableDictionary<string, int> SymbolCounters { get; }

            public TypeLayout(Dictionary<MethodSymbol, MethodExportLayout> methodLayouts, Dictionary<string, int> symbolCounters)
            {
                MethodLayouts = methodLayouts.ToImmutableDictionary();
                SymbolCounters = symbolCounters.ToImmutableDictionary();
            }
        }

        private object _layoutLock = new object();
        
        private Dictionary<MethodSymbol, MethodExportLayout> _layouts =
            new Dictionary<MethodSymbol, MethodExportLayout>();

        private Dictionary<TypeSymbol, TypeLayout> _builtLayouts = new Dictionary<TypeSymbol, TypeLayout>();

        private TypeSymbol _udonSharpBehaviourType;

        static string GetUniqueID(Dictionary<string, int> idLookup, string id)
        {
            if (!idLookup.TryGetValue(id, out var foundID))
            {
                idLookup.Add(id, 0);
            }

            idLookup[id] += 1;

            return $"__{foundID}_{id}";
        }

        private MethodExportLayout BuildMethodLayout(MethodSymbol methodSymbol, Dictionary<string, int> idLookup)
        {
            string methodName = methodSymbol.Name;
            string[] paramNames = new string[methodSymbol.Parameters.Length];
            string returnName = null;

            if (!methodSymbol.IsStatic && !CompilerUdonInterface.IsUdonEvent(methodSymbol) && CompilerUdonInterface.IsUdonEventName(methodName))
            {
                // If the user has declared an event with the same name as a built-in one but with different arguments, we imitate Unity here and complain for our built-in events
                throw new CompilerException($"Method with same name as built-in event '{methodSymbol}' cannot be declared with parameter types that do not match the event.", methodSymbol.RoslynSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation());
            }
            
            if (CompilerUdonInterface.IsUdonEvent(methodSymbol))
            {
                ImmutableArray<(string, Type)> paramArgs = CompilerUdonInterface.GetUdonEventArgs(methodName);
                methodName = CompilerUdonInterface.GetUdonEventName(methodName);

                for (int i = 0; i < paramNames.Length && i < paramArgs.Length; ++i)
                    paramNames[i] = paramArgs[i].Item1;
            }
            else
            {
                if (methodSymbol.Parameters.Length > 0) // Do not mangle 0 parameter methods as they may be called externally
                    methodName = GetUniqueID(idLookup, methodName);

                for (int i = 0; i < paramNames.Length; ++i)
                    paramNames[i] = GetUniqueID(idLookup, methodSymbol.Parameters[i].Name + "__param");
            }

            if (methodSymbol.ReturnType != null)
                returnName = GetUniqueID(idLookup, methodName + "__ret");

            MethodExportLayout exportLayout = new MethodExportLayout(methodSymbol, methodName, returnName, paramNames);
            
            _layouts.Add(methodSymbol, exportLayout);

            return exportLayout;
        }

        /// <summary>
        /// Builds a layout for a given type.
        /// First traverses all base types and builds their layouts when needed since the base type layouts inform the layout of derived types.
        /// </summary>
        private void BuildLayout(TypeSymbol typeSymbol, AbstractPhaseContext context)
        {
            Stack<TypeSymbol> typesToBuild = new Stack<TypeSymbol>();

            while (typeSymbol.BaseType != null && !_builtLayouts.ContainsKey(typeSymbol))
            {
                typesToBuild.Push(typeSymbol);
                if (typeSymbol == _udonSharpBehaviourType)
                    break;
                
                typeSymbol = typeSymbol.BaseType;
            }

            while (typesToBuild.Count > 0)
            {
                TypeSymbol currentBuildType = typesToBuild.Pop();

                Dictionary<string, int> idCounters;

                if (currentBuildType.BaseType != null &&
                    _builtLayouts.TryGetValue(currentBuildType.BaseType, out TypeLayout parentLayout))
                    idCounters = new Dictionary<string, int>(parentLayout.SymbolCounters);
                else
                    idCounters = new Dictionary<string, int>();

                Dictionary<MethodSymbol, MethodExportLayout> layouts =
                    new Dictionary<MethodSymbol, MethodExportLayout>();
                
                foreach (Symbol symbol in currentBuildType.GetMembers(context))
                {
                    if (symbol is MethodSymbol methodSymbol && 
                        (methodSymbol.OverridenMethod == null || 
                         methodSymbol.OverridenMethod.ContainingType == _udonSharpBehaviourType || 
                         methodSymbol.OverridenMethod.ContainingType.IsExtern))
                    {
                        layouts.Add(methodSymbol, BuildMethodLayout(methodSymbol, idCounters));
                    }
                }
                
                _builtLayouts.Add(currentBuildType, new TypeLayout(layouts, idCounters));
            }
        }

        /// <summary>
        /// Retrieves the method layout for a UdonSharpBehaviour method.
        /// This includes the method name, name of return variable, and name of parameter values.
        /// This is used internally by GetMethodLinkage in the EmitContext.
        /// This is also used when calling across UdonSharpBehaviours to determine what variables to set for parameters and such.
        /// </summary>
        /// <remarks>This method is thread safe and may be called safely from multiple Contexts at a time</remarks>
        public MethodExportLayout GetUsbMethodLayout(MethodSymbol method, AbstractPhaseContext context)
        {
            if (_udonSharpBehaviourType == null)
                _udonSharpBehaviourType = GetTypeSymbol(typeof(UdonSharpBehaviour), context);

            while (method.OverridenMethod != null &&
                   method.OverridenMethod.ContainingType != _udonSharpBehaviourType &&
                   !method.OverridenMethod.ContainingType.IsExtern)
                method = method.OverridenMethod;

            lock (_layoutLock)
            {
                if (_layouts.TryGetValue(method, out MethodExportLayout layout))
                    return layout;

                BuildLayout(method.ContainingType, context);

                return _layouts[method];
            }
        }

        public void BuildUdonBehaviourInheritanceLookup(IEnumerable<INamedTypeSymbol> rootTypes)
        {
            Dictionary<TypeSymbol, List<TypeSymbol>> inheritedTypeScratch = new Dictionary<TypeSymbol, List<TypeSymbol>>();

            TypeSymbol udonSharpBehaviourType = null;
            
            foreach (INamedTypeSymbol typeSymbol in rootTypes)
            {
                BindContext bindContext = new BindContext(this, typeSymbol, null);
                if (udonSharpBehaviourType == null)
                    udonSharpBehaviourType = bindContext.GetTypeSymbol(typeof(UdonSharpBehaviour));

                TypeSymbol rootTypeSymbol = bindContext.GetTypeSymbol(typeSymbol);

                TypeSymbol baseType = rootTypeSymbol.BaseType;

                while (baseType != udonSharpBehaviourType)
                {
                    if (!inheritedTypeScratch.TryGetValue(baseType, out List<TypeSymbol> inheritedTypeList))
                    {
                        inheritedTypeList = new List<TypeSymbol>();
                        inheritedTypeScratch.Add(baseType, inheritedTypeList);
                    }
                    
                    inheritedTypeList.Add(rootTypeSymbol);

                    baseType = baseType.BaseType;
                }
            }

            _inheritedTypes = new Dictionary<TypeSymbol, ImmutableArray<TypeSymbol>>();

            foreach (var typeLists in inheritedTypeScratch)
            {
                _inheritedTypes.Add(typeLists.Key, typeLists.Value.ToImmutableArray());
            }
        }

        public bool HasInheritedUdonSharpBehaviours(TypeSymbol baseType)
        {
            return _inheritedTypes.ContainsKey(baseType);
        }

        public ImmutableArray<TypeSymbol> GetInheritedTypes(TypeSymbol baseType)
        {
            if (_inheritedTypes.TryGetValue(baseType, out ImmutableArray<TypeSymbol> types))
                return types;
            
            return ImmutableArray<TypeSymbol>.Empty;
        }
    }
}
