using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    public class UdonSharpCompiler
    {
        private CompilationModule[] modules;

        private static int initAssemblyCounter = 0;

        public UdonSharpCompiler(UdonSharpProgramAsset programAsset)
        {
            modules = new CompilationModule[] { new CompilationModule(programAsset) };
        }

        public UdonSharpCompiler(UdonSharpProgramAsset[] programAssets)
        {
            modules = programAssets.Select(e => new CompilationModule(e)).ToArray();
        }

        public void Compile()
        {
            System.Diagnostics.Stopwatch compileTimer = new System.Diagnostics.Stopwatch();
            compileTimer.Start();

            int totalErrorCount = 0;
            int moduleCounter = 0;

            try
            {
                foreach (CompilationModule module in modules)
                {
                    EditorUtility.DisplayProgressBar("UdonSharp Compile",
                                                    $"Compiling {AssetDatabase.GetAssetPath(module.programAsset.sourceCsScript)}...",
                                                    Mathf.Clamp01((moduleCounter++ / (float)modules.Length) + Random.Range(0.01f, 0.2f))); // Make it look like we're doing work :D

                    int moduleErrorCount = module.Compile();
                    totalErrorCount += moduleErrorCount;

                    if (moduleErrorCount == 0)
                    {
                        AssignHeapConstants(module);

                        EditorUtility.SetDirty(module.programAsset);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            compileTimer.Stop();

            EditorUtility.ClearProgressBar();

            if (totalErrorCount == 0)
                Debug.Log($"[UdonSharp] Compile of script{(modules.Length > 1 ? "s" : "")} {string.Join(", ", modules.Select(e => Path.GetFileName(AssetDatabase.GetAssetPath(e.programAsset.sourceCsScript))))} finished in {compileTimer.Elapsed.ToString("mm\\:ss\\.fff")}");
        }

        public void AssignHeapConstants(CompilationModule module)
        {
            IUdonProgram program = module.programAsset.GetRealProgram();

            if (program != null)
            {
                foreach (SymbolDefinition symbol in module.moduleSymbols.GetAllUniqueChildSymbols())
                {
                    if (symbol.symbolDefaultValue != null)
                    {
                        uint symbolAddress = program.SymbolTable.GetAddressFromSymbol(symbol.symbolUniqueName);

                        program.Heap.SetHeapVariable(symbolAddress, symbol.symbolDefaultValue, symbol.symbolCsType);
                    }
                }

                RunFieldInitalizers(module);
            }
        }

        private void RunFieldInitalizers(CompilationModule module)
        {
            IUdonProgram program = module.programAsset.GetRealProgram();

            // We don't need to run the costly compilation if the user hasn't defined any fields with initializers
            if (module.fieldsWithInitializers.Count == 0)
                return;

            CodeCompileUnit compileUnit = new CodeCompileUnit();
            CodeNamespace ns = new CodeNamespace("FieldInitialzers");
            compileUnit.Namespaces.Add(ns);
            foreach (var resolverUsingNamespace in module.resolver.usingNamespaces)
            {
                if (!string.IsNullOrEmpty(resolverUsingNamespace))
                    ns.Imports.Add(new CodeNamespaceImport(resolverUsingNamespace));
            }

            CodeTypeDeclaration _class = new CodeTypeDeclaration("Initializer");
            ns.Types.Add(_class);
            CodeMemberMethod method = new CodeMemberMethod();
            _class.Members.Add(method);
            method.Attributes = MemberAttributes.Public | MemberAttributes.Static;
            method.ReturnType = new CodeTypeReference(typeof(void));
            method.Name = "DoInit";
            method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IUdonProgram), "program"));

            foreach (var fieldDeclarationSyntax in module.fieldsWithInitializers)
            {
                var type = fieldDeclarationSyntax.Declaration.Type;
                int count = 0;
                bool isConst = fieldDeclarationSyntax.Modifiers.Any(t => t.ToString() == "const");
                foreach (var variable in fieldDeclarationSyntax.Declaration.Variables)
                {
                    if (variable.Initializer != null)
                    {
                        string name = variable.Identifier.ToString();
                        if (isConst)
                        {
                            _class.Members.Add(new CodeSnippetTypeMember($"const {type} {name} {variable.Initializer};"));
                        }
                        else
                        {
                            method.Statements.Add(new CodeSnippetStatement($"{type} {name} {variable.Initializer};"));
                        }
                        
                        method.Statements.Add(new CodeSnippetStatement(
                            $"program.Heap.SetHeapVariable(program.SymbolTable.GetAddressFromSymbol(\"{variable.Identifier}\"), {name});"));

                        count++;
                    }
                }
            }

            CSharpCodeProvider provider = new CSharpCodeProvider();
            StringBuilder sb = new StringBuilder();
            using (StringWriter streamWriter = new StringWriter(sb))
            {
                provider.GenerateCodeFromCompileUnit(compileUnit, streamWriter, new CodeGeneratorOptions());
            }

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sb.ToString());

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var references = new List<MetadataReference>();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!assemblies[i].IsDynamic && assemblies[i].Location.Length > 0)
                    references.Add(MetadataReference.CreateFromFile(assemblies[i].Location));
            }

            CSharpCompilation compilation = CSharpCompilation.Create(
                $"init{initAssemblyCounter++}",
                syntaxTrees: new[] {syntaxTree},
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var memoryStream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(memoryStream);
                if (!result.Success)
                {
                    bool error = false;
                    foreach (Diagnostic diagnostic in result.Diagnostics)
                    {
                        if (diagnostic.Severity == DiagnosticSeverity.Error)
                        {
                            Debug.LogError(diagnostic);
                            error = true;
                        }
                    }

                    if (error)
                        Debug.LogError($"Generated Source code: {sb}");
                }
                else
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    Assembly assembly = Assembly.Load(memoryStream.ToArray());
                    var cls = assembly.GetType("FieldInitialzers.Initializer");
                    MethodInfo methodInfo = cls.GetMethod("DoInit", BindingFlags.Public | BindingFlags.Static);
                    methodInfo.Invoke(null, new[] {program});
                }
            }
        }
    }
}