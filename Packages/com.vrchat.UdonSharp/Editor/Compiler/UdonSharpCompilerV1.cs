
// #define SINGLE_THREAD_BUILD

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using UdonSharp.Core;
using UdonSharp.Internal;
using UdonSharp.Lib.Internal;
using UdonSharp.Serialization;
using UdonSharpEditor;
using UnityEditor;
using VRC.Udon;
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
        public bool ConcurrentBuild { get; set; } = true;
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
                CompilationContext.CompilePhase currentPhase = CurrentJob.Context.CurrentPhase;
                float phaseProgress = CurrentJob.Context.PhaseProgress;

                float totalProgress = (phaseProgress / (int) CompilationContext.CompilePhase.Count) +
                                      ((int) currentPhase / (float)(int)CompilationContext.CompilePhase.Count);
                
                UdonSharpUtils.ShowAsyncProgressBar($"U#: {currentPhase}", totalProgress);
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
                            UdonSharpUtils.LogWarning(logStr);
                            break;
                        case DiagnosticSeverity.Log:
                            UdonSharpUtils.Log(logStr);
                            break;
                    }
                }
            }
            
            // Translate the diagnostic types and apply them to the cache, todo: consider merging the two structures
            UdonSharpEditorCache.CompileDiagnostic[] diagnostics = new UdonSharpEditorCache.CompileDiagnostic[CurrentJob.Context.Diagnostics.Count];
            CompilationContext.CompileDiagnostic[] compileDiagnostics = CurrentJob.Context.Diagnostics.ToArray();

            for (int i = 0; i < diagnostics.Length; ++i)
            {
                LinePosition diagLine = compileDiagnostics[i]?.Location?.GetLineSpan().StartLinePosition ?? LinePosition.Zero;
                
                diagnostics[i] = new UdonSharpEditorCache.CompileDiagnostic()
                {
                    severity = compileDiagnostics[i].Severity,
                    message = compileDiagnostics[i].Message,
                    file = CurrentJob.Context.TranslateLocationToFileName(compileDiagnostics[i].Location) ?? "",
                    line = diagLine.Line,
                    character = diagLine.Character,
                };
            }

            UdonSharpEditorCache.Instance.LastCompileDiagnostics = diagnostics;
            
            if (CurrentJob.Task.IsFaulted)
            {
                UdonSharpUtils.LogError("internal compiler error, dumping exceptions. Please report to Merlin");

                if (CurrentJob.Task.Exception != null)
                {
                    foreach (Exception innerException in CurrentJob.Task.Exception.InnerExceptions)
                    {
                        UdonSharpUtils.LogError(innerException);
                    }
                }

                CleanupCompile();
                return;
            }

            if (CurrentJob.Context.ErrorCount > 0)
            {
                CleanupCompile();
                return;
            }
                
            foreach (ModuleBinding rootBinding in CurrentJob.Context.ModuleBindings)
            {
                if (rootBinding.programAsset == null) 
                    continue;
                
                rootBinding.programAsset.ApplyProgram();
                
                UdonSharpEditorCache.Instance.SetUASMStr(rootBinding.programAsset, rootBinding.assembly);
                
                rootBinding.programAsset.CompiledVersion = UdonSharpProgramVersion.CurrentVersion;
                EditorUtility.SetDirty(rootBinding.programAsset);
            }
            
            try
            {
                UdonSharpEditorCache.Instance.RehashAllScripts();
                UdonSharpEditorManager.RunPostBuildSceneFixup();
            }
            catch (Exception e)
            {
                UdonSharpUtils.LogError($"Exception while running post build fixup:\n{e}");
                CleanupCompile();
                return;
            }

            UdonSharpEditorCache.Instance.LastBuildType = CurrentJob.CompileOptions.IsEditorBuild
                ? UdonSharpEditorCache.DebugInfoType.Editor
                : UdonSharpEditorCache.DebugInfoType.Client;
            
            int scriptCount = CurrentJob.Context.ModuleBindings.Count(e => e.programAsset != null);
            UdonSharpUtils.Log($"Compile of {scriptCount} script{(scriptCount != 1 ? "s" : "")} finished in {CurrentJob.CompileTimer.Elapsed:mm\\:ss\\.fff}");
            
            CleanupCompile();

            if (_compileQueued)
            {
                Compile(_queuedOptions);
                _compileQueued = false;
                _queuedOptions = null;
            }
        }

        internal static void WaitForCompile()
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
            WaitForCompile();
            Compile(options);
            WaitForCompile();
        }

        /// <summary>
        /// Info that we need to pass from the main thread because the Unity APIs to access the info don't work off the main thread.
        /// </summary>
        private class ProgramAssetInfo
        {
            public UdonSharpProgramAsset programAsset;
            public Type scriptClass;
        }

        [PublicAPI]
        public static void Compile(UdonSharpCompileOptions options = null)
        {
            if (options == null)
                options = new UdonSharpCompileOptions();

            if (UdonSharpUtils.DoesUnityProjectHaveCompileErrors())
            {
                UdonSharpUtils.LogError("All Unity C# compiler errors must be resolved before running an UdonSharp compile.");
                return;
            }
            
            if (CurrentJob != null)
            {
                _compileQueued = true;
                _queuedOptions = options;
                return;
            }
            
            Localization.Loc.InitLocalization();

            UdonSharpProgramAsset[] allPrograms = UdonSharpProgramAsset.GetAllUdonSharpPrograms();

            bool hasError = !ValidateProgramAssetCollisions(allPrograms);
            
            Dictionary<string, ProgramAssetInfo> rootProgramLookup = new Dictionary<string, ProgramAssetInfo>();
            foreach (UdonSharpProgramAsset udonSharpProgram in allPrograms)
            {
                if (udonSharpProgram.sourceCsScript == null)
                {
                    UdonSharpUtils.LogError($"Source C# script on {udonSharpProgram} is null", udonSharpProgram);
                    hasError = true;
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(udonSharpProgram.sourceCsScript);
                
                if (string.IsNullOrEmpty(assetPath))
                {
                    UdonSharpUtils.LogError($"Source C# script on {udonSharpProgram} is null", udonSharpProgram);
                    hasError = true;
                    continue;
                }
                
                if (hasError)
                    continue;
                
                rootProgramLookup.Add(assetPath.Replace('\\', '/'), new ProgramAssetInfo() { programAsset = udonSharpProgram ? udonSharpProgram : null, scriptClass = udonSharpProgram != null ? udonSharpProgram.GetClass() : null });
            }

            if (hasError)
            {
                UdonSharpEditorCache.Instance.LastCompileDiagnostics = new []
                    {
                        new UdonSharpEditorCache.CompileDiagnostic()
                        {
                            file = "",
                            message = "Compile validation failed, check console output for details.",
                            severity = DiagnosticSeverity.Error,
                        } 
                    };
                
                return;
            }
            
            // var allSourcePaths = new HashSet<string>(UdonSharpProgramAsset.GetAllUdonSharpPrograms().Where(e => e.isV1Root).Select(e => AssetDatabase.GetAssetPath(e.sourceCsScript).Replace('\\', '/')));
            HashSet<string> allSourcePaths = new HashSet<string>(CompilationContext.GetAllFilteredSourcePaths(options.IsEditorBuild));

            if (!ValidateUdonSharpBehaviours(allPrograms, allSourcePaths))
                return;

            CompilationContext compilationContext = new CompilationContext(options);
            string[] defines = UdonSharpUtils.GetProjectDefines(options.IsEditorBuild);

            EditorApplication.LockReloadAssemblies();
            
            CompilerUdonInterface.AssemblyCacheInit();

            Task compileTask = new Task(() => Compile(compilationContext, rootProgramLookup, allSourcePaths, defines));
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
                if (!scriptToAssetMap.TryGetValue(programAsset.sourceCsScript, out var programAssetList))
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
                    UdonSharpUtils.LogError($"Script {Path.GetFileName(AssetDatabase.GetAssetPath(scriptAssetMapping.Key))} is referenced by {scriptAssetMapping.Value.Count} UdonSharpProgramAssets, scripts can only be referenced by 1 program asset.\n" +
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
                    UdonSharpUtils.LogError($"Script '{sourcePath}' does not belong to a U# assembly, have you made a U# assembly definition for the assembly the script is a part of?", programAsset.sourceCsScript);
                }
            }

            return succeeded;
        }

        private static void Compile(CompilationContext compilationContext, IReadOnlyDictionary<string, ProgramAssetInfo> rootProgramLookup, IEnumerable<string> allSourcePaths, string[] scriptingDefines)
        {
            Stopwatch setupTimer = Stopwatch.StartNew();
            
            CompilerUdonInterface.CacheInit();
            
            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Setup;
            ModuleBinding[] syntaxTrees = compilationContext.LoadSyntaxTreesAndCreateModules(allSourcePaths, scriptingDefines);

            foreach (ModuleBinding binding in syntaxTrees)
            {
                foreach (Diagnostic diag in binding.tree.GetDiagnostics())
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
                    ProgramAssetInfo info = rootProgramLookup[treeBinding.filePath];
                    treeBinding.programAsset = info.programAsset;
                    treeBinding.programClass = info.scriptClass;
                    // ReSharper disable once Unity.NoNullPropagation
                    treeBinding.programScript = treeBinding?.programAsset?.sourceCsScript;
                    
                    rootTrees.Add(treeBinding);
                }
            }
            
            PrintStageTime("U# Setup", setupTimer);
            
            Stopwatch roslynCompileTimer = Stopwatch.StartNew();

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.RoslynCompile;
            
            // Run compilation for the semantic views
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"UdonSharpRoslynCompileAssembly{_assemblyCounter++}",
                syntaxTrees.Select(e => e.tree),
                CompilationContext.GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: compilationContext.Options.ConcurrentBuild));

            PrintStageTime("Roslyn Compile", roslynCompileTimer);

            compilationContext.RoslynCompilation = compilation;

            byte[] builtAssembly = null;
            
            Stopwatch roslynEmitTimer = Stopwatch.StartNew();
            
            using (MemoryStream memoryStream = new MemoryStream())
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

            foreach (ModuleBinding tree in syntaxTrees)
                tree.semanticModel = compilation.GetSemanticModel(tree.tree);

            ConcurrentBag<(INamedTypeSymbol, ModuleBinding)> rootUdonSharpTypes = new ConcurrentBag<(INamedTypeSymbol, ModuleBinding)>();
            HashSet<INamedTypeSymbol> typesWithAssociatedProgramAssets = new HashSet<INamedTypeSymbol>();
            object programTypesLock = new object();

            Parallel.ForEach(rootTrees, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM}, module =>
            {
                SemanticModel model = module.semanticModel;
                SyntaxTree tree = model.SyntaxTree;

                INamedTypeSymbol udonSharpBehaviourDeclaration = null;

                foreach (ClassDeclarationSyntax classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(classDecl) is INamedTypeSymbol classType && classType.IsUdonSharpBehaviour())
                    {
                        if (!classType.IsAbstract && classType.Name != Path.GetFileNameWithoutExtension(module.filePath))
                        {
                            compilationContext.AddDiagnostic(DiagnosticSeverity.Error, classType.DeclaringSyntaxReferences.First().GetSyntax(), "UdonSharpBehaviour classes must have the same name as their containing .cs file");
                            return;
                        }

                        if (!ReferenceEquals(module.programScript, null) &&
                            ReferenceEquals(module.programClass, null))
                        {
                            compilationContext.AddDiagnostic(DiagnosticSeverity.Error, classType.DeclaringSyntaxReferences.First().GetSyntax(), "Could not retrieve C# class, make sure the MonoBehaviour class name matches the name of the .cs file and Unity has had a chance to compile the C# script.");
                            return;
                        }
                        
                        if (!ReferenceEquals(module.programAsset, null) && classType.Name == Path.GetFileNameWithoutExtension(module.filePath))
                        {
                            if (classType.IsAbstract)
                            {
                                compilationContext.AddDiagnostic(DiagnosticSeverity.Error, classType.DeclaringSyntaxReferences.First().GetSyntax(), "Abstract U# behaviours cannot have an associated U# program asset");
                                return;
                            }
                            
                            if (classType.IsGenericType)
                            {
                                compilationContext.AddDiagnostic(DiagnosticSeverity.Error, classType.DeclaringSyntaxReferences.First().GetSyntax(), "Generic U# behaviours cannot have an associated U# program asset");
                                return;
                            }
                        }
                        
                        if (classType.IsAbstract || classType.IsGenericType)
                            continue;

                        // If there are multiple UdonSharpBehaviours declared in the same behaviour, they need to be partial classes of the same class
                        // We'll skip adding them as roots in that case
                        if (udonSharpBehaviourDeclaration == null)
                        {
                            udonSharpBehaviourDeclaration = classType;
                            rootUdonSharpTypes.Add((classType, module));
                        }
                    }
                }

                if (!ReferenceEquals(module.programAsset, null) && udonSharpBehaviourDeclaration == null)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, tree.GetRoot(), "Script with U# program asset referencing it must have an UdonSharpBehaviour definition; scripts without UdonSharpBehaviour definitions should not have an associated UdonSharpProgramAsset");
                    return;
                }

                // Allow multiple partial classes of the same UdonSharpBehaviour type in one file, but still check if people have multiple of the partial class with program assets associated
                if (udonSharpBehaviourDeclaration?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is ClassDeclarationSyntax declarationSyntax && 
                    declarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    lock (programTypesLock)
                    {
                        if (typesWithAssociatedProgramAssets.Contains(udonSharpBehaviourDeclaration))
                        {
                            compilationContext.AddDiagnostic(DiagnosticSeverity.Error, udonSharpBehaviourDeclaration.DeclaringSyntaxReferences.First().GetSyntax(), "Partial U# behaviours cannot have multiple program assets, choose 1 primary program asset for partial U# behaviours");
                            return;
                        }

                        typesWithAssociatedProgramAssets.Add(udonSharpBehaviourDeclaration);
                    }
                }
            });

            (INamedTypeSymbol, ModuleBinding)[] rootTypes = rootUdonSharpTypes.ToArray();

            compilationContext.BuildUdonBehaviourInheritanceLookup(rootTypes.Select(e => e.Item1));
            
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

        private static void BindAllPrograms((INamedTypeSymbol, ModuleBinding)[] bindings, CompilationContext compilationContext)
        {
            Stopwatch bindTimer = Stopwatch.StartNew();

            Dictionary<TypeSymbol, HashSet<Symbol>> symbolsToBind = new Dictionary<TypeSymbol, HashSet<Symbol>>();
            object referencedSymbolsLock = new object();

            int currentIterationDivisor = 2;
            compilationContext.PhaseProgress = 0f;

            Dictionary<TypeSymbol, HashSet<Symbol>> bindSet = symbolsToBind;

        #if SINGLE_THREAD_BUILD
            foreach (var rootTypeSymbol in bindings)
        #else
            Parallel.ForEach(bindings, new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLELISM},rootTypeSymbol => 
        #endif
            {
                if (compilationContext.ErrorCount > 0)
                    return;
                
                BindContext bindContext = new BindContext(compilationContext, rootTypeSymbol.Item1, Array.Empty<Symbol>());
                Dictionary<TypeSymbol, HashSet<Symbol>> referencedTypes;
                
                try
                {
                    bindContext.Bind();

                    rootTypeSymbol.Item2.binding = bindContext;
                    referencedTypes = bindContext.GetTypeSymbol(rootTypeSymbol.Item1).CollectReferencedUnboundSymbols(bindContext, Array.Empty<Symbol>());
                }
                catch (CompilerException e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, e.Location ?? bindContext.CurrentNode?.GetLocation(), e.Message);
                    return;
                }
                catch (Exception e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, bindContext.CurrentNode, e.ToString());
                    return;
                }

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
                    Dictionary<TypeSymbol, HashSet<Symbol>> referencedSymbols;
                    
                    try
                    {
                        bindContext.Bind();
                        referencedSymbols = typeSymbol.Key.CollectReferencedUnboundSymbols(bindContext, typeSymbol.Value);
                    }
                    catch (CompilerException e)
                    {
                        compilationContext.AddDiagnostic(DiagnosticSeverity.Error, e.Location ?? bindContext.CurrentNode?.GetLocation(), e.Message);
                        return;
                    }
                    catch (Exception e)
                    {
                        compilationContext.AddDiagnostic(DiagnosticSeverity.Error, bindContext.CurrentNode, e.ToString());
                        return;
                    }

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

                long typeID = UdonSharpInternalUtility.GetTypeID(typeName);
                
                moduleEmitContext.RootTable.CreateReflectionValue(CompilerConstants.UsbTypeIDHeapKey,
                    moduleEmitContext.GetTypeSymbol(SpecialType.System_Int64), typeID);
                moduleEmitContext.RootTable.CreateReflectionValue(CompilerConstants.UsbTypeNameHeapKey,
                    moduleEmitContext.GetTypeSymbol(SpecialType.System_String), typeName);

                TypeSymbol udonSharpBehaviourType = moduleEmitContext.GetTypeSymbol(typeof(UdonSharpBehaviour));
                
                if (moduleEmitContext.EmitType.BaseType != udonSharpBehaviourType ||
                    compilationContext.HasInheritedUdonSharpBehaviours(moduleEmitContext.EmitType))
                {
                    List<long> baseTypeArr = new List<long>();

                    TypeSymbol currentType = moduleEmitContext.EmitType;

                    while (currentType != udonSharpBehaviourType)
                    {
                        baseTypeArr.Add(UdonSharpInternalUtility.GetTypeID(TypeSymbol.GetFullTypeName(currentType.RoslynSymbol)));
                        currentType = currentType.BaseType;
                    }

                    // Array of base types inclusive of the root type
                    moduleEmitContext.RootTable.CreateReflectionValue(CompilerConstants.UsbTypeIDArrayHeapKey,
                        moduleEmitContext.GetTypeSymbol(typeof(long).MakeArrayType()), baseTypeArr.ToArray());
                }

                try
                {
                    moduleEmitContext.Emit();
                }
                catch (CompilerException e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, e.Location ?? moduleEmitContext.CurrentNode?.GetLocation(), e.Message);
                }
                catch (Exception e)
                {
                    compilationContext.AddDiagnostic(DiagnosticSeverity.Error, moduleEmitContext.CurrentNode, e.ToString());
                    return;
                }

                BehaviourSyncMode syncMode = BehaviourSyncMode.Any;
                UdonBehaviourSyncModeAttribute syncModeAttribute = null;

                TypeSymbol currentTypeSymbol = moduleEmitContext.EmitType;

                while (currentTypeSymbol != udonSharpBehaviourType && syncModeAttribute == null)
                {
                    syncModeAttribute = currentTypeSymbol.GetAttribute<UdonBehaviourSyncModeAttribute>();
                    currentTypeSymbol = currentTypeSymbol.BaseType;
                }

                if (syncModeAttribute != null)
                    syncMode = syncModeAttribute.behaviourSyncMode;

                moduleBinding.programAsset.behaviourSyncMode = syncMode;
                    
                Dictionary<string, FieldDefinition> fieldDefinitions = new Dictionary<string, FieldDefinition>();

                foreach (FieldSymbol symbol in moduleEmitContext.DeclaredFields)
                {
                    if (!symbol.Type.TryGetSystemType(out Type symbolSystemType))
                        UdonSharpUtils.LogError($"Could not get type for field {symbol.Name}");
                    
                    CheckSyncCompatibility(symbol, compilationContext, moduleEmitContext);

                    fieldDefinitions.Add(symbol.Name, new FieldDefinition(symbol.Name, symbolSystemType, symbol.Type.UdonType.SystemType, symbol.SyncMode, symbol.IsSerialized, symbol.SymbolAttributes.ToList()));
                }

                if (compilationContext.ErrorCount > 0)
                {
                    return;
                }

                moduleBinding.programAsset.fieldDefinitions = fieldDefinitions;

                Interlocked.Increment(ref progressCounter);
                compilationContext.PhaseProgress = progressCounter / (float) bindingCount;
                
                if (moduleEmitContext.DebugInfo != null)
                    UdonSharpEditorCache.Instance.SetDebugInfo(moduleBinding.programAsset, CurrentJob.CompileOptions.IsEditorBuild ? UdonSharpEditorCache.DebugInfoType.Editor : UdonSharpEditorCache.DebugInfoType.Client, moduleEmitContext.DebugInfo);

                moduleBinding.programAsset.scriptID = typeID;

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

        private static void CheckSyncCompatibility(FieldSymbol field, CompilationContext context, EmitContext emitContext)
        {
            UdonSyncedAttribute fieldSyncAttribute = field.GetAttribute<UdonSyncedAttribute>();

            if (fieldSyncAttribute == null)
            {
                return;
            }

            TypeSymbol fieldType = field.Type;

            if (!fieldType.IsExtern)
            {
                if (fieldType.IsEnum || (fieldType.IsArray && fieldType.ElementType.IsEnum))
                {
                    fieldType = fieldType.UdonType;
                }
                else
                {
                    context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"Cannot sync type '{fieldType}'");
                    return;
                }
            }
            
            if (!UdonNetworkTypes.CanSync(fieldType.UdonType.SystemType))
            {
                context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' type '{fieldType}' is not supported by Udon sync");
            }
            
            UdonBehaviourSyncModeAttribute syncModeAttribute = emitContext.EmitType.GetAttribute<UdonBehaviourSyncModeAttribute>();
            
            switch (syncModeAttribute?.behaviourSyncMode ?? BehaviourSyncMode.Any)
            {
                case BehaviourSyncMode.None:
                case BehaviourSyncMode.NoVariableSync:
                    context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' cannot be synced on an UdonBehaviour with sync mode None");
                    break;
                case BehaviourSyncMode.Continuous:
                    if (fieldType.IsArray)
                    {
                        context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' is an array which is not supported by Continuous sync, switch the behaviour to use Manual sync if you want to sync array fields");
                    }

                    goto default;
                case BehaviourSyncMode.Manual:
                    if (fieldSyncAttribute.NetworkSyncType == UdonSyncMode.Linear)
                    {
                        context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' has linear interpolation sync mode which cannot be used with Manual sync, use Continuous sync if you want linear interpolation on synced values");
                    }
                    
                    if (fieldSyncAttribute.NetworkSyncType == UdonSyncMode.Smooth)
                    {
                        context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' has smooth interpolation sync mode which cannot be used with Manual sync, use Continuous sync if you want smooth interpolation on synced values");
                    }
                    
                    goto default;
                case BehaviourSyncMode.Any:
                    goto default;
                default:
                    
                    if (fieldSyncAttribute.NetworkSyncType == UdonSyncMode.Linear && !UdonNetworkTypes.CanSyncLinear(fieldType.UdonType.SystemType))
                    {
                        context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' type '{fieldType}' is not supported for linear sync");
                    }
                    else if (fieldSyncAttribute.NetworkSyncType == UdonSyncMode.Smooth && !UdonNetworkTypes.CanSyncSmooth(fieldType.UdonType.SystemType))
                    {
                        context.AddDiagnostic(DiagnosticSeverity.Error, field.RoslynSymbol.Locations.First(), $"'{field.Name}' type '{fieldType}' is not supported for smooth sync");
                    }
                    break;
            }
        }

        private static readonly object _assembleLock = new object();

        private static void AssembleProgram((INamedTypeSymbol, ModuleBinding) binding,
            System.Reflection.Assembly assembly)
        {
            INamedTypeSymbol rootTypeSymbol = binding.Item1;
            ModuleBinding rootBinding = binding.Item2;
            List<Value> assemblyValues = rootBinding.assemblyModule.RootTable.GetAllUniqueChildValues();
            string generatedUasm = rootBinding.assemblyModule.BuildUasmStr();

            rootBinding.programAsset.AssembleCsProgram(generatedUasm, rootBinding.assemblyModule.GetHeapSize());
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
            
            lock (_assembleLock)
            {
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

                while (asmType != typeof(UdonSharpBehaviour))
                {
                    foreach (FieldInfo field in asmType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        uint valAddress = program.SymbolTable.GetAddressFromSymbol(UdonSharpUtils.UnmanglePropertyFieldName(field.Name));

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
                        // We set synced strings to an empty string by default
                        else if (field.FieldType == typeof(string) &&
                                 field.GetValue(component) == null &&
                                 field.GetCustomAttribute<UdonSyncedAttribute>() != null)
                        {
                            program.Heap.SetHeapVariable(valAddress, "");
                        }
                        else
                        {
                            program.Heap.SetHeapVariable(valAddress, fieldValue, field.FieldType);
                        }
                    }
                    
                    asmType = asmType.BaseType;
                }

                rootBinding.assembly = generatedUasm;
            }
        }
    }
}
