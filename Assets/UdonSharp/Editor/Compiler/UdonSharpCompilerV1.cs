
using System;
using System.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Compiler
{
    public class UdonSharpCompilerV1
    {
        private static int _assemblyCounter;

        public void Compile(string filePath)
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
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
                
                moduleEmitContext.Emit();

                Dictionary<string, FieldDefinition> fieldDefinitions = new Dictionary<string, FieldDefinition>();

                foreach (FieldSymbol symbol in moduleEmitContext.DeclaredFields)
                {
                    if (symbol.IsConst)
                        continue;
                    
                    if (!symbol.Type.TryGetSystemType(out var symbolSystemType))
                        Debug.LogError($"Could not get type for field {symbol.Name}");
                    
                    // Debug.Log($"Field {symbol.Name}, type: {symbolSystemType}");
                    
                    fieldDefinitions.Add(symbol.Name, new FieldDefinition(symbolSystemType, symbol.Type.UdonType.SystemType, null, symbol.SymbolAttributes.ToList()));
                }

                moduleBinding.programAsset.fieldDefinitions = fieldDefinitions;
            }

            foreach (ModuleBinding rootBinding in rootUdonSharpTypes.Select(e => e.Item2))
            {
                List<Value> assemblyValues = rootBinding.assemblyModule.RootTable.GetAllUniqueChildValues();
                string generatedUasm = rootBinding.assemblyModule.BuildUasmStr(null);
                
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
                if (asm.name != "Assembly-CSharp") // We only want the root Unity script assembly for user scripts at the moment
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
        
        static List<MetadataReference> metadataReferences;

        static List<MetadataReference> GetMetadataReferences()
        {
            if (metadataReferences == null)
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                metadataReferences = new List<MetadataReference>();

                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (!assemblies[i].IsDynamic && assemblies[i].Location.Length > 0 && !assemblies[i].Location.StartsWith("data"))
                    {
                        System.Reflection.Assembly assembly = assemblies[i];

                        if (assembly.GetName().Name == "Assembly-CSharp" ||
                            assembly.GetName().Name == "Assembly-CSharp-Editor")
                        {
                            continue;
                        }

                        PortableExecutableReference executableReference = null;

                        try
                        {
                            executableReference = MetadataReference.CreateFromFile(assembly.Location);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"Unable to locate assembly {assemblies[i].Location} Exception: {e}");
                        }

                        if (executableReference != null)
                            metadataReferences.Add(executableReference);
                    }
                }
            }

            return metadataReferences;
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
