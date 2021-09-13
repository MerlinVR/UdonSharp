
// #define SINGLE_THREAD_BUILD

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Internal;
using UdonSharp.Lib.Internal;
using UdonSharp.Serialization;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Compilation;
using VRC.Udon.Common.Interfaces;
using Debug = UnityEngine.Debug;

namespace UdonSharp.Compiler
{
    public class UdonSharpCompileOptions
    {
        public bool IsEditorBuild { get; set; } = true;
        // public bool BuildDebugInfo { get; set; } = true;
        public bool DisableLogging { get; set; } = false;
    }
    
    [InitializeOnLoad]
    public class UdonSharpCompilerV1
    {
        private static int _assemblyCounter;
        private const int MAX_PARALLELISM = 6;

        private class CompileJob
        {
            public Task Task { get; set; }
            public CompilationContext Context { get; set; }
            public UdonSharpCompileOptions CompileOptions { get; set; }
            public Stopwatch CompileTimer { get; set; }
        }
        
        private static CompileJob CurrentJob { get; set; }
        private static bool _compileQueued;
        private static UdonSharpCompileOptions _queuedOptions;

        static UdonSharpCompilerV1()
        {
            EditorApplication.update += EditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        private static void OnPlayStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode || 
                stateChange == PlayModeStateChange.ExitingPlayMode)
                WaitForCompile();
        }

        private static void EditorUpdate()
        {
            TickCompile();
        }

        private static void TickCompile()
        {
            if (CurrentJob == null) return;
            
            if (!CurrentJob.Task.IsCompleted)
            {
                var currentPhase = CurrentJob.Context.CurrentPhase;
                float phaseProgress = CurrentJob.Context.PhaseProgress;

                float totalProgress = (phaseProgress / (int) CompilationContext.CompilePhase.Count) +
                                      ((int) currentPhase / (float)(int)CompilationContext.CompilePhase.Count);
                
                UdonSharpUtils.ShowAsyncProgressBar("U#: " + currentPhase, totalProgress);
                return;
            }

            if (!CurrentJob.CompileOptions.DisableLogging)
            {
                foreach (var diagnostic in CurrentJob.Context.Diagnostics)
                {
                    string filePath = "";
                    if (diagnostic.Location != null)
                        filePath = CurrentJob.Context.TranslateLocationToFileName(diagnostic.Location);
                    LinePosition? linePosition = diagnostic.Location?.GetLineSpan().StartLinePosition;

                    int line = (linePosition?.Line ?? 0) + 1;
                    int character = (linePosition?.Character ?? 0) + 1;

                    string fileStr = $"{filePath ?? "Unknown File"}({line},{character})";

                    string logStr = $"{fileStr}: {diagnostic.Message}";

                    switch (diagnostic.Severity)
                    {
                        case DiagnosticSeverity.Error:
                            UdonSharpUtils.LogBuildError(diagnostic.Message, filePath, line, character);
                            break;
                        case DiagnosticSeverity.Warning:
                            Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] {logStr}");
                            break;
                        case DiagnosticSeverity.Log:
                            Debug.Log($"[<color=#0c824c>UdonSharp</color>] {logStr}");
                            break;
                    }
                }
            }

            if (CurrentJob.Context.ErrorCount > 0)
            {
                // Debug.LogError($"[<color=#FF00FF>UdonSharp</color>] Compile Failed!");
                
                CleanupCompile();
                return;
            }
                
            foreach (ModuleBinding rootBinding in CurrentJob.Context.ModuleBindings)
            {
                if (rootBinding.programAsset == null) 
                    continue;
                
                rootBinding.programAsset.ApplyProgram();
                
                UdonSharpEditorCache.Instance.SetUASMStr(rootBinding.programAsset, rootBinding.assembly);
                UdonSharpEditorCache.Instance.UpdateSourceHash(rootBinding.programAsset, rootBinding.sourceText);
                EditorUtility.SetDirty(rootBinding.programAsset);
            }
            
            UdonSharpEditorManager.RunPostBuildSceneFixup();
            
            Debug.Log($"[<color=#0c824c>UdonSharp</color>] Compile of {CurrentJob.Context.ModuleBindings.Length} scripts finished in {CurrentJob.CompileTimer.Elapsed:mm\\:ss\\.fff}");
            
            CleanupCompile();

            if (_compileQueued)
            {
                Compile(_queuedOptions);
                _compileQueued = false;
                _queuedOptions = null;
            }
        }

        private static void WaitForCompile()
        {
            if (CurrentJob == null) return;
            
            if (!CurrentJob.Task.IsCompleted)
                CurrentJob.Task.Wait();
            
            TickCompile();
        }

        private static void CleanupCompile()
        {
            UdonSharpUtils.ClearAsyncProgressBar();
            
            EditorApplication.UnlockReloadAssemblies();

            CurrentJob = null;
        }

        private static void PrintStageTime(string stageName, Stopwatch stopwatch)
        {
            // Debug.Log($"{stageName}: {stopwatch.Elapsed.TotalSeconds * 1000.0}ms");
        }

        [PublicAPI]
        public static void CompileSync(UdonSharpCompileOptions options = null)
        {
            Compile(options);
            WaitForCompile();
        }

        [PublicAPI]
        public static void Compile(UdonSharpCompileOptions options = null)
        {
            if (options == null)
                options = new UdonSharpCompileOptions();
            
            if (CurrentJob != null)
            {
                _compileQueued = true;
                _queuedOptions = options;
                return;
            }

            EditorApplication.LockReloadAssemblies();
            
            Localization.Loc.InitLocalization();
            var allPrograms = UdonSharpProgramAsset.GetAllUdonSharpPrograms();
            
            var rootProgramLookup = new Dictionary<string, UdonSharpProgramAsset>();
            foreach (var udonSharpProgram in allPrograms)
            {
                if (udonSharpProgram.isV1Root)
                    rootProgramLookup.Add(AssetDatabase.GetAssetPath(udonSharpProgram.sourceCsScript).Replace('\\', '/'), udonSharpProgram);
            }
            
            // var allSourcePaths = new HashSet<string>(UdonSharpProgramAsset.GetAllUdonSharpPrograms().Where(e => e.isV1Root).Select(e => AssetDatabase.GetAssetPath(e.sourceCsScript).Replace('\\', '/')));
            HashSet<string> allSourcePaths = new HashSet<string>(GetAllFilteredSourcePaths());

            CompilationContext compilationContext = new CompilationContext();
            string[] defines = UdonSharpUtils.GetProjectDefines(options.IsEditorBuild);

            var compileTask = new Task(() => Compile(compilationContext, rootProgramLookup, allSourcePaths, defines));
            CurrentJob = new CompileJob() { Context = compilationContext, Task = compileTask, CompileTimer = Stopwatch.StartNew(), CompileOptions = options };
            
            compileTask.Start();
        }

        private static void Compile(CompilationContext compilationContext, Dictionary<string, UdonSharpProgramAsset> rootProgramLookup, IEnumerable<string> allSourcePaths, string[] scriptingDefines)
        {
            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Setup;
            var syntaxTrees = compilationContext.LoadSyntaxTreesAndCreateModules(allSourcePaths, scriptingDefines);

            foreach (ModuleBinding binding in syntaxTrees)
            {
                foreach (var diag in binding.tree.GetDiagnostics())
                {
                    if (diag.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Error) continue;
                    
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, diag.Location, $"{diag.Severity.ToString().ToLower()} {diag.Id}: {diag.GetMessage()}");
                }
            }

            if (compilationContext.ErrorCount > 0)
                return;

            List<ModuleBinding> rootTrees = new List<ModuleBinding>();
            
            foreach (ModuleBinding treeBinding in syntaxTrees)
            {
                if (rootProgramLookup.ContainsKey(treeBinding.filePath))
                {
                    rootTrees.Add(treeBinding);
                    treeBinding.programAsset = rootProgramLookup[treeBinding.filePath];
                }
            }
            
            Stopwatch roslynCompileTimer = Stopwatch.StartNew();

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.RoslynCompile;
            
            // Run compilation for the semantic views
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"UdonSharpRoslynCompileAssembly{_assemblyCounter++}",
                syntaxTrees.Select(e => e.tree),
                GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            PrintStageTime("Roslyn Compile", roslynCompileTimer);

            compilationContext.RoslynCompilation = compilation;

            byte[] builtAssembly = null;
            
            Stopwatch roslynEmitTimer = Stopwatch.StartNew();
            
            using (var memoryStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(memoryStream);
                if (emitResult.Success)
                {
                    builtAssembly = memoryStream.ToArray();
                }
                else
                {
                    foreach (Diagnostic diag in emitResult.Diagnostics)
                    {
                        if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        {
                            compilationContext.AddDiagnostic(DiagnosticSeverity.Error, diag.Location, $"{diag.Severity.ToString().ToLower()} {diag.Id}: {diag.GetMessage()}");
                        }
                    }
                }
            }
            
            PrintStageTime("Roslyn Emit", roslynEmitTimer);

            if (compilationContext.ErrorCount > 0)
                return;

            foreach (var tree in syntaxTrees)
                tree.semanticModel = compilation.GetSemanticModel(tree.tree);

            ConcurrentBag<(INamedTypeSymbol, ModuleBinding)> rootUdonSharpTypes = new ConcurrentBag<(INamedTypeSymbol, ModuleBinding)>();

            Parallel.ForEach(rootTrees, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM}, module =>
            {
                SemanticModel model = module.semanticModel;
                SyntaxTree tree = model.SyntaxTree;

                foreach (ClassDeclarationSyntax classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(classDecl) is INamedTypeSymbol classType && classType.IsUdonSharpBehaviour())
                    {
                        rootUdonSharpTypes.Add((classType, module));
                    }
                }
            });

            (INamedTypeSymbol, ModuleBinding)[] rootTypes = rootUdonSharpTypes.ToArray();

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Bind;

            BindAllPrograms(rootTypes, compilationContext);

            if (compilationContext.ErrorCount > 0) return;

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Emit;
            
            EmitAllPrograms(rootTypes, compilationContext);
            
            if (compilationContext.ErrorCount > 0) return;
            
            UdonSharpEditorManager.ConstructorWarningsDisabled = true;

            try
            {
                System.Reflection.Assembly assembly; 

                using (new UdonSharpUtils.UdonSharpAssemblyLoadStripScope())
                    assembly = System.Reflection.Assembly.Load(builtAssembly);

                compilationContext.CurrentPhase = CompilationContext.CompilePhase.Assemble;

                AssembleAllPrograms(rootTypes, assembly, compilationContext);
            }
            catch (Exception e)
            {
                compilationContext.AddDiagnostic(DiagnosticSeverity.Error, (Location)null, e.ToString());
            }
            
            UdonSharpEditorManager.ConstructorWarningsDisabled = false;
        }

        private static IEnumerable<string> GetAllFilteredSourcePaths()
        {
            var allScripts = UdonSharpSettings.FilterBlacklistedPaths(Directory.GetFiles("Assets/", "*.cs", SearchOption.AllDirectories));

            HashSet<string> assemblySourcePaths = new HashSet<string>();

            foreach (UnityEditor.Compilation.Assembly asm in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (asm.name != "Assembly-CSharp" && !IsUdonSharpAssembly(asm.name)) // We only want the root Unity script assembly for user scripts at the moment
                    assemblySourcePaths.UnionWith(asm.sourceFiles);
            }
            
            List<string> filteredPaths = new List<string>();

            foreach (string path in allScripts)
            {
                if (!assemblySourcePaths.Contains(path))
                    filteredPaths.Add(path);
            }

            return filteredPaths;
        }

        private static List<UdonSharpAssemblyDefinition> _udonSharpAssemblies;
        private static List<UdonSharpAssemblyDefinition> GetUdonSharpAssemblyDefinitions()
        {
            if (_udonSharpAssemblies != null)
                return _udonSharpAssemblies;

            _udonSharpAssemblies = AssetDatabase.FindAssets($"t:{nameof(UdonSharpAssemblyDefinition)}")
                                                .Select(e => AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(AssetDatabase.GUIDToAssetPath(e)))
                                                .ToList();

            return _udonSharpAssemblies;
        }

        private static HashSet<string> _udonSharpAssemblyNames;

        private static bool IsUdonSharpAssembly(string assemblyName)
        {
            if (_udonSharpAssemblyNames == null)
            {
                _udonSharpAssemblyNames = new HashSet<string>();
                foreach (UdonSharpAssemblyDefinition asmDef in GetUdonSharpAssemblyDefinitions())
                {
                    _udonSharpAssemblyNames.Add(asmDef.sourceAssembly.name);
                }
            }

            return _udonSharpAssemblyNames.Contains(assemblyName);
        }

        private static List<MetadataReference> _metadataReferences;

        private static IEnumerable<MetadataReference> GetMetadataReferences()
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

        private static void BindAllPrograms((INamedTypeSymbol, ModuleBinding)[] bindings, CompilationContext compilationContext)
        {
            Stopwatch bindTimer = Stopwatch.StartNew();
            
            HashSet<TypeSymbol> symbolsToBind = new HashSet<TypeSymbol>();
            object hashSetLock = new object();

            int currentIterationDivisor = 2;
            compilationContext.PhaseProgress = 0f;

        #if SINGLE_THREAD_BUILD
            foreach (var rootTypeSymbol in bindings)
            {
                BindContext bindContext = new BindContext(compilationContext, rootTypeSymbol.Item1);
                bindContext.Bind();
            
                rootTypeSymbol.Item2.binding = bindContext;
                
                symbolsToBind.UnionWith(bindContext.GetTypeSymbol(rootTypeSymbol.Item1).CollectReferencedUnboundTypes(bindContext));
            }
        #else
            Parallel.ForEach(bindings, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM},rootTypeSymbol =>
            {
                if (compilationContext.ErrorCount > 0)
                    return;
                
                BindContext bindContext = new BindContext(compilationContext, rootTypeSymbol.Item1);
                
                try
                {
                    bindContext.Bind();
                }
                catch (Exception e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, bindContext.CurrentNode, e.ToString());
                    return;
                }

                rootTypeSymbol.Item2.binding = bindContext;

                var referencedTypes = bindContext.GetTypeSymbol(rootTypeSymbol.Item1)
                    .CollectReferencedUnboundTypes(bindContext).ToArray();

                lock (hashSetLock)
                {
                    // ReSharper disable once AccessToModifiedClosure
                    symbolsToBind.UnionWith(referencedTypes);
                    compilationContext.PhaseProgress += (1f / bindings.Length) / currentIterationDivisor;
                }
            });
        #endif
            
            while (symbolsToBind.Count > 0)
            {
                currentIterationDivisor *= 2;
                
                HashSet<TypeSymbol> newSymbols = new HashSet<TypeSymbol>();
                
            #if SINGLE_THREAD_BUILD
                foreach (TypeSymbol symbolToBind in symbolsToBind)
                {
                    if (!symbolToBind.IsBound)
                    {
                        BindContext bindContext = new BindContext(compilationContext, symbolToBind.RoslynSymbol);
                
                        bindContext.Bind();
                
                        newSymbols.UnionWith(symbolToBind.CollectReferencedUnboundTypes(bindContext));
                    }
                }
            #else
                Parallel.ForEach(symbolsToBind.Where(e => !e.IsBound), new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM}, typeSymbol =>
                {
                    if (compilationContext.ErrorCount > 0)
                        return;
                    
                    BindContext bindContext = new BindContext(compilationContext, typeSymbol.RoslynSymbol);
                    
                    try
                    {
                        bindContext.Bind();
                    }
                    catch (Exception e)
                    {
                        compilationContext.AddDiagnostic(DiagnosticSeverity.Error, bindContext.CurrentNode, e.ToString());
                        return;
                    }
                    
                    var referencedSymbols = typeSymbol.CollectReferencedUnboundTypes(bindContext).ToArray();

                    lock (hashSetLock)
                    {
                        newSymbols.UnionWith(referencedSymbols);
                        compilationContext.PhaseProgress += (1f / symbolsToBind.Count) / currentIterationDivisor;
                    }
                });
            #endif

                symbolsToBind = newSymbols;
            }
            
            PrintStageTime("U# Bind", bindTimer);
        }

        private static void EmitAllPrograms((INamedTypeSymbol, ModuleBinding)[] bindings, CompilationContext compilationContext)
        {
            Stopwatch emitTimer = Stopwatch.StartNew();

            int progressCounter = 0;
            int bindingCount = bindings.Length;
            
        #if SINGLE_THREAD_BUILD
            foreach (var binding in bindings)
        #else
            Parallel.ForEach(bindings, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM}, binding => 
        #endif
            {
                if (compilationContext.ErrorCount > 0)
                    return;
                
                INamedTypeSymbol rootTypeSymbol = binding.Item1;
                ModuleBinding moduleBinding = binding.Item2;
                AssemblyModule assemblyModule = new AssemblyModule(compilationContext);
                moduleBinding.assemblyModule = assemblyModule;
                
                EmitContext moduleEmitContext = new EmitContext(assemblyModule, rootTypeSymbol);

                string typeName = TypeSymbol.GetFullTypeName(rootTypeSymbol);
                
                moduleEmitContext.RootTable.CreateReflectionValue(CompilerConstants.UsbTypeIDHeapKey,
                    moduleEmitContext.GetTypeSymbol(SpecialType.System_Int64), UdonSharpInternalUtility.GetTypeID(typeName));
                moduleEmitContext.RootTable.CreateReflectionValue(CompilerConstants.UsbTypeNameHeapKey,
                    moduleEmitContext.GetTypeSymbol(SpecialType.System_String), typeName);

                try
                {
                    moduleEmitContext.Emit();
                }
                catch (Exception e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, moduleEmitContext.CurrentNode, e.ToString());
                    return;
                }

                Dictionary<string, FieldDefinition> fieldDefinitions = new Dictionary<string, FieldDefinition>();

                foreach (FieldSymbol symbol in moduleEmitContext.DeclaredFields)
                {
                    if (!symbol.Type.TryGetSystemType(out var symbolSystemType))
                        Debug.LogError($"Could not get type for field {symbol.Name}");
                    
                    fieldDefinitions.Add(symbol.Name, new FieldDefinition(symbol.Name, symbolSystemType, symbol.Type.UdonType.SystemType, symbol.SyncMode, symbol.IsSerialized, symbol.SymbolAttributes.ToList()));
                }

                moduleBinding.programAsset.fieldDefinitions = fieldDefinitions;

                Interlocked.Increment(ref progressCounter);
                compilationContext.PhaseProgress = progressCounter / (float) bindingCount;
                
                if (moduleEmitContext.DebugInfo != null)
                    UdonSharpEditorCache.Instance.SetDebugInfo(moduleBinding.programAsset, CurrentJob.CompileOptions.IsEditorBuild ? UdonSharpEditorCache.DebugInfoType.Editor : UdonSharpEditorCache.DebugInfoType.Client, moduleEmitContext.DebugInfo);
            }
        #if !SINGLE_THREAD_BUILD
            );
        #endif
            
            PrintStageTime("U# Emit", emitTimer);
        }

        private static void AssembleAllPrograms((INamedTypeSymbol, ModuleBinding)[] bindings, System.Reflection.Assembly assembly, CompilationContext context)
        {
            Stopwatch assembleTimer = Stopwatch.StartNew();

            int progressCounter = 0;
            
            // #if SINGLE_THREAD_BUILD
            // #else
            //     Parallel.ForEach(bindings, binding => 
            // #endif
            // Can't thread because assembly relies on state from the editor interface internally and constructing an editor interface for each thread is way worse :<
            foreach (var binding in bindings)
            {
                INamedTypeSymbol rootTypeSymbol = binding.Item1;
                ModuleBinding rootBinding = binding.Item2;
                List<Value> assemblyValues = rootBinding.assemblyModule.RootTable.GetAllUniqueChildValues();
                string generatedUasm = rootBinding.assemblyModule.BuildUasmStr();
                
                rootBinding.programAsset.SetUdonAssembly(generatedUasm);
                rootBinding.programAsset.AssembleCsProgram(rootBinding.assemblyModule.GetHeapSize());
                rootBinding.programAsset.SetUdonAssembly("");

                IUdonProgram program = rootBinding.programAsset.GetRealProgram();
                
                foreach (Value val in assemblyValues)
                {
                    if (val.DefaultValue == null) continue;
                    uint valAddress = program.SymbolTable.GetAddressFromSymbol(val.UniqueID);
                    program.Heap.SetHeapVariable(valAddress, val.DefaultValue, val.UdonType.SystemType);
                }

                string typeName = TypeSymbol.GetFullTypeName(rootTypeSymbol);

                Type asmType = assembly.GetType(typeName);

                object component = Activator.CreateInstance(asmType);

                foreach (FieldInfo field in asmType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    uint valAddress = program.SymbolTable.GetAddressFromSymbol(field.Name);

                    object fieldValue = field.GetValue(component);

                    if (fieldValue == null)
                        continue;
                    
                    if (UdonSharpUtils.IsUserJaggedArray(fieldValue.GetType()))
                    {
                        Serializer serializer = Serializer.CreatePooled(fieldValue.GetType());

                        SimpleValueStorage<object[]> arrayStorage = new SimpleValueStorage<object[]>();
                        serializer.WriteWeak(arrayStorage, fieldValue);

                        program.Heap.SetHeapVariable<object[]>(valAddress, arrayStorage.Value);
                    }
                    else
                    {
                        program.Heap.SetHeapVariable(valAddress, fieldValue, field.FieldType);
                    }
                }

                rootBinding.assembly = generatedUasm;

                context.PhaseProgress = progressCounter++ / (float) bindings.Length;
            }
            // #if !SINGLE_THREAD_BUILD
            //     );
            // #endif
        
            PrintStageTime("Assemble", assembleTimer);
        }
    }
}
