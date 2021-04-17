
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UdonSharp.Compiler.Binder;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UdonSharp.Compiler
{
    public class UdonSharpCompilerV1
    {
        static int assemblyCounter;

        public void Compile()
        {
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            var rootProgramPaths = new HashSet<string>(UdonSharpProgramAsset.GetAllUdonSharpPrograms().Where(e => e.isV1Root).Select(e => AssetDatabase.GetAssetPath(e.sourceCsScript).Replace('\\', '/')));
            //var rootProgramPaths = new HashSet<string>(UdonSharpProgramAsset.GetAllUdonSharpPrograms().Select(e => AssetDatabase.GetAssetPath(e.sourceCsScript).Replace('\\', '/')));
            HashSet<string> allSourcePaths = new HashSet<string>(GetAllFilteredSourcePaths());

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
                if (rootProgramPaths.Contains(treeBinding.filePath))
                    rootTrees.Add(treeBinding);
                else
                    derivitiveTrees.Add(treeBinding);

                bindingLookup.Add(treeBinding.tree, treeBinding);
            }

            // Run compilation for the semantic views
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"UdonSharpRoslynCompileAssembly{assemblyCounter++}",
                syntaxTrees: syntaxTrees.Select(e => e.tree),
                references: GetMetadataReferences(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            int compileErrors = 0;

            foreach (var diag in compilation.GetDiagnostics())
            {
                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    Debug.LogError($"Compile Error: {diag}");
                    ++compileErrors;
                }
            }

            if (compileErrors > 0)
                return;

            foreach (var tree in syntaxTrees)
                tree.semanticModel = compilation.GetSemanticModel(tree.tree);

            ConcurrentBag<INamedTypeSymbol> rootUdonSharpTypes = new ConcurrentBag<INamedTypeSymbol>();

            Parallel.ForEach(rootTrees, module =>
            {
                SemanticModel model = module.semanticModel;
                SyntaxTree tree = model.SyntaxTree;

                int udonSharpBehaviourCount = 0;

                foreach (ClassDeclarationSyntax classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(classDecl) is INamedTypeSymbol classType && classType.IsUdonSharpBehaviour())
                    {
                        rootUdonSharpTypes.Add(classType);
                        udonSharpBehaviourCount++;
                    }
                }
            });

            IEnumerable<INamedTypeSymbol> uniqueRootSymbols = rootUdonSharpTypes.Distinct();

            CompilationContext compilationContext = new CompilationContext(compilation);

            foreach (INamedTypeSymbol rootTypeSymbol in uniqueRootSymbols)
            {
                BindModule typeBinder = new BindModule(compilationContext, compilationContext.GetTypeSymbol(rootTypeSymbol));

                typeBinder.Bind();
            }

            Debug.Log($"Ran compile in {timer.Elapsed.TotalSeconds * 1000.0:F3}ms");
        }

        IEnumerable<string> GetAllFilteredSourcePaths()
        {
            var allScripts = UdonSharpSettings.FilterBlacklistedPaths(Directory.GetFiles("Assets/", "*.cs", SearchOption.AllDirectories));

            HashSet<string> assemblySourcePaths = new HashSet<string>();

            foreach (UnityEditor.Compilation.Assembly asm in CompilationPipeline.GetAssemblies(AssembliesType.Editor).Union(CompilationPipeline.GetAssemblies(AssembliesType.Player)))
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
        }

        IEnumerable<ModuleBinding> LoadSyntaxTrees(IEnumerable<string> sourcePaths)
        {
            ConcurrentBag<ModuleBinding> syntaxTrees = new ConcurrentBag<ModuleBinding>();

            bool isEditorBuild = true;
            string[] defines = UdonSharpUtils.GetProjectDefines(isEditorBuild);

            Parallel.ForEach(sourcePaths, (currentSource) =>
            {
                string programSource = UdonSharpUtils.ReadFileTextSync(currentSource);

#pragma warning disable CS1701 // Warning about System.Collections.Immutable versions potentially not matching
                Microsoft.CodeAnalysis.SyntaxTree programSyntaxTree = CSharpSyntaxTree.ParseText(programSource, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithPreprocessorSymbols(defines));
#pragma warning restore CS1701

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
    }
}
