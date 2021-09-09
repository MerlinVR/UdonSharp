
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Internal;
using UdonSharp.Lib.Internal;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Compilation;
using VRC.Udon.Common.Interfaces;
using Debug = UnityEngine.Debug;

namespace UdonSharp.Compiler
{
    public class UdonSharpCompilerV1
    {
        private static int _assemblyCounter;

        public void Compile(string filePath)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            var allPrograms = UdonSharpProgramAsset.GetAllUdonSharpPrograms();
            
            var rootProgramLookup = new Dictionary<string, UdonSharpProgramAsset>();
            foreach (var udonSharpProgram in allPrograms)
            {
                if (udonSharpProgram.isV1Root)
                    rootProgramLookup.Add(AssetDatabase.GetAssetPath(udonSharpProgram.sourceCsScript).Replace('\\', '/'), udonSharpProgram);
            }
            
            // var allSourcePaths = new HashSet<string>(UdonSharpProgramAsset.GetAllUdonSharpPrograms().Where(e => e.isV1Root).Select(e => AssetDatabase.GetAssetPath(e.sourceCsScript).Replace('\\', '/')));
            HashSet<string> allSourcePaths = new HashSet<string>(GetAllFilteredSourcePaths());

            if (filePath != null)
                allSourcePaths = new HashSet<string>(new [] {filePath});

            var syntaxTrees = LoadSyntaxTrees(allSourcePaths);

            int treeErrors = 0;

            foreach (ModuleBinding binding in syntaxTrees)
            {
                foreach (var diag in binding.tree.GetDiagnostics())
                {
                    if (diag.Severity == DiagnosticSeverity.Error)
                    {
                        Debug.LogError(diag);
                        treeErrors++;
                    }
                }
            }

            if (treeErrors > 0)
                return;

            List<ModuleBinding> rootTrees = new List<ModuleBinding>();
            List<ModuleBinding> derivitiveTrees = new List<ModuleBinding>();
            Dictionary<SyntaxTree, ModuleBinding> bindingLookup = new Dictionary<SyntaxTree, ModuleBinding>();
            
            foreach (ModuleBinding treeBinding in syntaxTrees)
            {
                if (rootProgramLookup.ContainsKey(treeBinding.filePath))
                {
                    rootTrees.Add(treeBinding);
                    treeBinding.programAsset = rootProgramLookup[treeBinding.filePath];
                }
                else
                    derivitiveTrees.Add(treeBinding);

                bindingLookup.Add(treeBinding.tree, treeBinding);
            }

            // Run compilation for the semantic views
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"UdonSharpRoslynCompileAssembly{_assemblyCounter++}",
                syntaxTrees: syntaxTrees.Select(e => e.tree),
                references: GetMetadataReferences(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            int compileErrors = 0;

            foreach (var diag in compilation.GetDiagnostics())
            {
                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    Debug.LogError($"Compile Error: {bindingLookup[diag.Location.SourceTree].filePath}: {diag}");
                    ++compileErrors;
                }
            }

            if (compileErrors > 0)
                return;

            foreach (var tree in syntaxTrees)
                tree.semanticModel = compilation.GetSemanticModel(tree.tree);

            ConcurrentBag<(INamedTypeSymbol, ModuleBinding)> rootUdonSharpTypes = new ConcurrentBag<(INamedTypeSymbol, ModuleBinding)>();

            Parallel.ForEach(rootTrees, module =>
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

            Dictionary<INamedTypeSymbol, ModuleBinding>
                moduleLookup = new Dictionary<INamedTypeSymbol, ModuleBinding>();
            foreach (var pair in rootUdonSharpTypes)
                moduleLookup.Add(pair.Item1, pair.Item2);

            CompilationContext compilationContext = new CompilationContext(compilation);

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Bind;

            BindAllPrograms(rootUdonSharpTypes, compilationContext);

            compilationContext.CurrentPhase = CompilationContext.CompilePhase.Emit;

            foreach (var (rootTypeSymbol, moduleBinding) in rootUdonSharpTypes)
            {
                AssemblyModule assemblyModule = new AssemblyModule(compilationContext);
                moduleBinding.assemblyModule = assemblyModule;
                
                EmitContext moduleEmitContext = new EmitContext(assemblyModule, rootTypeSymbol);

                moduleEmitContext.RootTable.CreateReflectionValue(CompilerConstants.UsbTypeIDHeapKey,
                    moduleEmitContext.GetTypeSymbol(SpecialType.System_Int64),
                    UdonSharpInternalUtility.GetTypeID(rootTypeSymbol.ToDisplayString(
                        new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle
                            .NameAndContainingTypesAndNamespaces))));
                
                moduleEmitContext.Emit();

                Dictionary<string, FieldDefinition> fieldDefinitions = new Dictionary<string, FieldDefinition>();

                foreach (FieldSymbol symbol in moduleEmitContext.DeclaredFields)
                {
                    if (!symbol.Type.TryGetSystemType(out var symbolSystemType))
                        Debug.LogError($"Could not get type for field {symbol.Name}");
                    
                    // Debug.Log($"Field {symbol.Name}, type: {symbolSystemType}");
                    
                    fieldDefinitions.Add(symbol.Name, new FieldDefinition(symbolSystemType, symbol.Type.UdonType.SystemType, symbol.SyncMode, symbol.IsSerialized, symbol.SymbolAttributes.ToList()));
                }

                moduleBinding.programAsset.fieldDefinitions = fieldDefinitions;
            }

            foreach (ModuleBinding rootBinding in rootUdonSharpTypes.Select(e => e.Item2))
            {
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
                
                rootBinding.programAsset.ApplyProgram();
                
                UdonSharpEditorCache.Instance.SetUASMStr(rootBinding.programAsset, generatedUasm);
                UdonSharpEditorCache.Instance.UpdateSourceHash(rootBinding.programAsset, rootBinding.sourceText);
                
                EditorUtility.SetDirty(rootBinding.programAsset);
            }

            Debug.Log($"Ran compile in {timer.Elapsed.TotalSeconds * 1000.0:F3}ms");
        }

        IEnumerable<string> GetAllFilteredSourcePaths()
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

        class ModuleBinding
        {
            public SyntaxTree tree;
            public string filePath;
            public string sourceText;
            public SemanticModel semanticModel; // Populated after Roslyn compile
            public AssemblyModule assemblyModule;
            public UdonSharpProgramAsset programAsset;
            public BindContext binding;
        }

        IEnumerable<ModuleBinding> LoadSyntaxTrees(IEnumerable<string> sourcePaths)
        {
            ConcurrentBag<ModuleBinding> syntaxTrees = new ConcurrentBag<ModuleBinding>();

            bool isEditorBuild = true;
            string[] defines = UdonSharpUtils.GetProjectDefines(isEditorBuild);

            Parallel.ForEach(sourcePaths, (currentSource) =>
            {
                string programSource = UdonSharpUtils.ReadFileTextSync(currentSource);

                var programSyntaxTree = CSharpSyntaxTree.ParseText(programSource, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithPreprocessorSymbols(defines).WithLanguageVersion(LanguageVersion.CSharp7_3));

                syntaxTrees.Add(new ModuleBinding() { tree = programSyntaxTree, filePath = currentSource, sourceText = programSource });
            });

            return syntaxTrees;
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

        void BindAllPrograms(IEnumerable<(INamedTypeSymbol, ModuleBinding)> bindings, CompilationContext compilationContext)
        {
            HashSet<TypeSymbol> symbolsToBind = new HashSet<TypeSymbol>();

            foreach (var rootTypeSymbol in bindings)
            {
                BindContext bindContext = new BindContext(compilationContext, rootTypeSymbol.Item1);
                bindContext.Bind();

                rootTypeSymbol.Item2.binding = bindContext;
                
                symbolsToBind.UnionWith(bindContext.GetTypeSymbol(rootTypeSymbol.Item1).CollectReferencedUnboundTypes(bindContext));
            }

            while (symbolsToBind.Count > 0)
            {
                HashSet<TypeSymbol> newSymbols = new HashSet<TypeSymbol>();
                
                foreach (TypeSymbol symbolToBind in symbolsToBind)
                {
                    if (!symbolToBind.IsBound)
                    {
                        BindContext bindContext = new BindContext(compilationContext, symbolToBind.RoslynSymbol);

                        bindContext.Bind();

                        newSymbols.UnionWith(symbolToBind.CollectReferencedUnboundTypes(bindContext));
                    }
                }

                symbolsToBind = newSymbols;
            }
        }
    }
}
