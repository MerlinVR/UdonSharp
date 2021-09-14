
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

namespace UdonSharp.Compiler
{
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
            /// <summary>
            /// Linking jump points for methods, linking other behaviour's field/method addresses, and building vtables
            /// </summary>
            // Link,
            /// <summary>
            /// Running uasm assembler to generate bytecode that's usable by Udon and writing modified program asssets
            /// </summary>
            // Assemble,
            /// <summary>
            /// Linking behaviours to the UdonSharpRuntime manager object in the current scene
            /// </summary>
            // SceneLink,
            /// <summary>
            /// Validating that behaviours in the current scene are in a correct state
            /// </summary>
            Validation,
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

        public TypeSymbol GetTypeSymbol(ITypeSymbol type, AbstractPhaseContext context)
        {
            TypeSymbol typeSymbol = _typeSymbolLookup.GetOrAdd(type, (key) => TypeSymbolFactory.CreateSymbol(type, context));

            return typeSymbol;
        }

        public TypeSymbol GetUdonTypeSymbol(ITypeSymbol type, AbstractPhaseContext context)
        {
            Type systemType;
                
            if (type.TypeKind == TypeKind.Array)
                systemType = UdonSharpUtils.UserTypeToUdonType(((IArrayTypeSymbol) type).GetExternType());
            else
                systemType = UdonSharpUtils.UserTypeToUdonType(((INamedTypeSymbol) type).GetExternType());
            
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
            
            if (CompilerUdonInterface.IsUdonEvent(methodName))
            {
                var paramArgs = CompilerUdonInterface.GetUdonEventArgs(methodName);
                methodName = CompilerUdonInterface.GetUdonEventName(methodName);

                for (int i = 0; i < paramNames.Length; ++i)
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
                        (methodSymbol.OverridenMethod == null || methodSymbol.OverridenMethod.ContainingType == _udonSharpBehaviourType))
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
                   method.OverridenMethod.ContainingType != _udonSharpBehaviourType)
                method = method.OverridenMethod;

            lock (_layoutLock)
            {
                if (_layouts.TryGetValue(method, out MethodExportLayout layout))
                    return layout;

                BuildLayout(method.ContainingType, context);

                return _layouts[method];
            }
        }
    }
}
