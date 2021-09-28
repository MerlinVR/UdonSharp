
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
using UdonSharp.Compiler.Udon;
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
    using SyntaxTree = Microsoft.CodeAnalysis.SyntaxTree;

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

            int scriptCount = CurrentJob.Context.ModuleBindings.Count(e => e.programAsset != null);
            Debug.Log($"[<color=#0c824c>UdonSharp</color>] Compile of {scriptCount} script{(scriptCount != 1 ? "s" : "")} finished in {CurrentJob.CompileTimer.Elapsed:mm\\:ss\\.fff}");
            
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
            
            Localization.Loc.InitLocalization();
            CompilerUdonInterface.CacheInit();

            var allPrograms = UdonSharpProgramAsset.GetAllUdonSharpPrograms();

            if (!ValidateProgramAssetCollisions(allPrograms))
                return;
            
            var rootProgramLookup = new Dictionary<string, UdonSharpProgramAsset>();
            foreach (var udonSharpProgram in allPrograms)
            {
                if (udonSharpProgram.sourceCsScript == null)
                {
                    Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] Source C# script on {udonSharpProgram} is null", udonSharpProgram);
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(udonSharpProgram.sourceCsScript);
                
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] Source C# script on {udonSharpProgram} is null", udonSharpProgram);
                    continue;
                }
                
                rootProgramLookup.Add(assetPath.Replace('\\', '/'), udonSharpProgram);
            }
            
            // var allSourcePaths = new HashSet<string>(UdonSharpProgramAsset.GetAllUdonSharpPrograms().Where(e => e.isV1Root).Select(e => AssetDatabase.GetAssetPath(e.sourceCsScript).Replace('\\', '/')));
            HashSet<string> allSourcePaths = new HashSet<string>(GetAllFilteredSourcePaths(options.IsEditorBuild));

            if (!ValidateUdonSharpBehaviours(allPrograms, allSourcePaths))
                return;

            CompilationContext compilationContext = new CompilationContext();
            string[] defines = UdonSharpUtils.GetProjectDefines(options.IsEditorBuild);

            EditorApplication.LockReloadAssemblies();

            var compileTask = new Task(() => Compile(compilationContext, rootProgramLookup, allSourcePaths, defines));
            CurrentJob = new CompileJob() { Context = compilationContext, Task = compileTask, CompileTimer = Stopwatch.StartNew(), CompileOptions = options };
            
            compileTask.Start();
        }

        private static bool ValidateProgramAssetCollisions(UdonSharpProgramAsset[] allProgramAssets)
        {
            Dictionary<MonoScript, List<UdonSharpProgramAsset>> scriptToAssetMap = new Dictionary<MonoScript, List<UdonSharpProgramAsset>>();

            foreach (UdonSharpProgramAsset programAsset in allProgramAssets)
            {
                if (programAsset == null || programAsset.sourceCsScript == null)
                    continue;

                // Add program asset to map to check if there are any duplicate program assets that point to the same script
                List<UdonSharpProgramAsset> programAssetList;
                if (!scriptToAssetMap.TryGetValue(programAsset.sourceCsScript, out programAssetList))
                {
                    programAssetList = new List<UdonSharpProgramAsset>();
                    scriptToAssetMap.Add(programAsset.sourceCsScript, programAssetList);
                }

                programAssetList.Add(programAsset);
            }

            int errorCount = 0;
            
            foreach (var scriptAssetMapping in scriptToAssetMap)
            {
                if (scriptAssetMapping.Value.Count > 1)
                {
                    Debug.LogError($"[<color=#FF00FF>UdonSharp</color>] Script {Path.GetFileName(AssetDatabase.GetAssetPath(scriptAssetMapping.Key))} is referenced by {scriptAssetMapping.Value.Count} UdonSharpProgramAssets, scripts should only be referenced by 1 program asset.\n" +
                                     "Referenced program assets:\n" +
                                     string.Join(",\n", scriptAssetMapping.Value.Select(AssetDatabase.GetAssetPath)));

                    errorCount++;
                }
            }

            return errorCount == 0;
        }

        private static bool ValidateUdonSharpBehaviours(UdonSharpProgramAsset[] allProgramAssets, HashSet<string> allSourcePaths)
        {
            bool succeeded = true;
            
            foreach (var programAsset in allProgramAssets)
            {
                if (programAsset.sourceCsScript == null)
                    continue;

                string sourcePath = AssetDatabase.GetAssetPath(programAsset.sourceCsScript);
                
                if (string.IsNullOrEmpty(sourcePath))
                    continue;

                if (!allSourcePaths.Contains(sourcePath))
                {
                    succeeded = false;
                    Debug.LogError($"[<color=#FF00FF>UdonSharp</color>] Script '{sourcePath}' does not belong to a U# assembly, have you made a U# assembly definition for the assembly the script is a part of?", programAsset.sourceCsScript);
                }
            }

            return succeeded;
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
                        if (!classType.IsAbstract && classType.Name != Path.GetFileNameWithoutExtension(module.filePath))
                        {
                            compilationContext.AddDiagnostic(DiagnosticSeverity.Error, classType.DeclaringSyntaxReferences.First().GetSyntax(), "UdonSharpBehaviour classes must have the same name as their containing .cs file");
                            return;
                        }
                        
                        if (classType.IsAbstract && module.programAsset != null && classType.Name == Path.GetFileNameWithoutExtension(module.filePath))
                        {
                            compilationContext.AddDiagnostic(DiagnosticSeverity.Error, classType.DeclaringSyntaxReferences.First().GetSyntax(), "Abstract U# behaviours cannot have an associated U# program asset");
                            return;
                        }
                        
                        if (classType.IsAbstract)
                            continue;
                        
                        rootUdonSharpTypes.Add((classType, module));
                    }
                }
            });

            (INamedTypeSymbol, ModuleBinding)[] rootTypes = rootUdonSharpTypes.ToArray();

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Bind;

            BindAllPrograms(rootTypes, compilationContext);

            if (compilationContext.ErrorCount > 0) return;

            System.Reflection.Assembly assembly = null;
            try
            {
                using (new UdonSharpUtils.UdonSharpAssemblyLoadStripScope())
                    assembly = System.Reflection.Assembly.Load(builtAssembly);
            }
            catch (Exception e)
            {
                compilationContext.AddDiagnostic(DiagnosticSeverity.Error, (Location)null, e.ToString());
            }
            
            if (compilationContext.ErrorCount > 0) return;
            
            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Emit;
            
            EmitAllPrograms(rootTypes, compilationContext, assembly);
        }

        private static IEnumerable<string> GetAllFilteredSourcePaths(bool isEditorBuild)
        {
            HashSet<string> assemblySourcePaths = new HashSet<string>();

            foreach (UnityEditor.Compilation.Assembly asm in CompilationPipeline.GetAssemblies(isEditorBuild ? AssembliesType.Editor : AssembliesType.PlayerWithoutTestAssemblies))
            {
                if (asm.name == "Assembly-CSharp" || IsUdonSharpAssembly(asm.name))
                    assemblySourcePaths.UnionWith(asm.sourceFiles);
            }

            return UdonSharpSettings.FilterBlacklistedPaths(assemblySourcePaths);
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

            Dictionary<TypeSymbol, HashSet<Symbol>> symbolsToBind = new Dictionary<TypeSymbol, HashSet<Symbol>>();
            object referencedSymbolsLock = new object();

            int currentIterationDivisor = 2;
            compilationContext.PhaseProgress = 0f;

            var bindSet = symbolsToBind;

        #if SINGLE_THREAD_BUILD
            foreach (var rootTypeSymbol in bindings)
        #else
            Parallel.ForEach(bindings, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM},rootTypeSymbol =>
        #endif
            {
                if (compilationContext.ErrorCount > 0)
                    return;
                
                BindContext bindContext = new BindContext(compilationContext, rootTypeSymbol.Item1, Array.Empty<Symbol>());
                
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

                var referencedTypes = bindContext.GetTypeSymbol(rootTypeSymbol.Item1).CollectReferencedUnboundSymbols(bindContext, Array.Empty<Symbol>());

                lock (referencedSymbolsLock)
                {
                    UnionSymbols(referencedTypes, bindSet);
                    compilationContext.PhaseProgress += (1f / bindings.Length) / currentIterationDivisor;
                }
            }
        #if !SINGLE_THREAD_BUILD
            );
        #endif
            
            while (symbolsToBind.Count > 0)
            {
                currentIterationDivisor *= 2;
                
                Dictionary<TypeSymbol, HashSet<Symbol>> newSymbols = new Dictionary<TypeSymbol, HashSet<Symbol>>();
                
            #if SINGLE_THREAD_BUILD
                foreach (var typeSymbol in symbolsToBind)
            #else
                Parallel.ForEach(symbolsToBind, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM}, typeSymbol =>
            #endif
                {
                    if (compilationContext.ErrorCount > 0)
                        return;
                    
                    BindContext bindContext = new BindContext(compilationContext, typeSymbol.Key.RoslynSymbol, typeSymbol.Value);
                    
                    try
                    {
                        bindContext.Bind();
                    }
                    catch (Exception e)
                    {
                        compilationContext.AddDiagnostic(DiagnosticSeverity.Error, bindContext.CurrentNode, e.ToString());
                        return;
                    }
                    
                    var referencedSymbols = typeSymbol.Key.CollectReferencedUnboundSymbols(bindContext, typeSymbol.Value);

                    lock (referencedSymbolsLock)
                    {
                        UnionSymbols(referencedSymbols, newSymbols);
                        compilationContext.PhaseProgress += (1f / symbolsToBind.Count) / currentIterationDivisor;
                    }
                }
            #if !SINGLE_THREAD_BUILD
                );
            #endif

                symbolsToBind = newSymbols;
            }
            
            PrintStageTime("U# Bind", bindTimer);
        }

        private static void UnionSymbols(Dictionary<TypeSymbol, HashSet<Symbol>> mergeSet, Dictionary<TypeSymbol, HashSet<Symbol>> bindSet)
        {
            foreach (var referencedTypeSymbols in mergeSet)
            {
                if (bindSet.TryGetValue(referencedTypeSymbols.Key, out var typeSymbols))
                    typeSymbols.UnionWith(referencedTypeSymbols.Value);
                else
                    bindSet.Add(referencedTypeSymbols.Key, referencedTypeSymbols.Value);
            }
        }

        private static void EmitAllPrograms((INamedTypeSymbol, ModuleBinding)[] bindings, CompilationContext compilationContext, System.Reflection.Assembly assembly)
        {
            Stopwatch emitTimer = Stopwatch.StartNew();

            int progressCounter = 0;
            int bindingCount = bindings.Length;
            
        #if SINGLE_THREAD_BUILD
            foreach (var binding in bindings)
        #else
            Parallel.ForEach(bindings, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM * 2 }, binding => 
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

                BehaviourSyncMode syncMode = BehaviourSyncMode.Any;
                UdonBehaviourSyncModeAttribute syncModeAttribute = moduleEmitContext.EmitType.GetAttribute<UdonBehaviourSyncModeAttribute>();

                if (syncModeAttribute != null)
                    syncMode = syncModeAttribute.behaviourSyncMode;

                moduleBinding.programAsset.behaviourSyncMode = syncMode;
                    
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

                try
                {
                    AssembleProgram(binding, assembly);
                }
                catch (Exception e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, moduleEmitContext.CurrentNode, e.ToString());
                }
            }
        #if !SINGLE_THREAD_BUILD
            );
        #endif
            
            PrintStageTime("U# Emit + Udon Assembly", emitTimer);
        }

        private static readonly object _assembleLock = new object();

        private static void AssembleProgram((INamedTypeSymbol, ModuleBinding) binding,
            System.Reflection.Assembly assembly)
        {
            lock (_assembleLock)
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

                UdonSharpEditorManager.ConstructorWarningsDisabled = true;
                object component;
                
                try
                {
                    component = Activator.CreateInstance(asmType);
                }
                finally
                {
                    UdonSharpEditorManager.ConstructorWarningsDisabled = false;
                }

                foreach (FieldInfo field in asmType.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                              BindingFlags.Instance))
                {
                    uint valAddress = program.SymbolTable.GetAddressFromSymbol(field.Name.Replace("<", "_").Replace(">", "_"));

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
                    else if (UdonSharpUtils.IsUserDefinedType(fieldValue.GetType()))
                    {
                        Serializer serializer = Serializer.CreatePooled(fieldValue.GetType());

                        IValueStorage typeStorage = (IValueStorage)Activator.CreateInstance(typeof(SimpleValueStorage<>).MakeGenericType(serializer.GetUdonStorageType()), null);
                        serializer.WriteWeak(typeStorage, fieldValue);

                        program.Heap.SetHeapVariable(valAddress, typeStorage.Value, typeStorage.Value.GetType());
                    }
                    else
                    {
                        program.Heap.SetHeapVariable(valAddress, fieldValue, field.FieldType);
                    }
                }

                rootBinding.assembly = generatedUasm;
            }
        }
    }
}
