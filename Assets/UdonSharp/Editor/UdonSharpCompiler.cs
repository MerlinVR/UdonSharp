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
using UnityEngine.Profiling;
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
            Profiler.BeginSample("UdonSharp Compile");

            System.Diagnostics.Stopwatch compileTimer = new System.Diagnostics.Stopwatch();
            compileTimer.Start();

            int totalErrorCount = 0;
            int moduleCounter = 0;

            try
            {
                List<ClassDefinition> classDefinitions = BuildClassDefinitions();
                if (classDefinitions == null)
                    totalErrorCount++;

                foreach (CompilationModule module in modules)
                {
                    EditorUtility.DisplayProgressBar("UdonSharp Compile",
                                                    $"Compiling {AssetDatabase.GetAssetPath(module.programAsset.sourceCsScript)}...",
                                                    Mathf.Clamp01((moduleCounter++ / (float)modules.Length) + Random.Range(0.01f, 1f / modules.Length))); // Make it look like we're doing work :D

                    int moduleErrorCount = module.Compile(classDefinitions);
                    totalErrorCount += moduleErrorCount;
                }

                if (totalErrorCount == 0)
                {
                    EditorUtility.DisplayProgressBar("UdonSharp Compile", "Assigning constants...", 1f);
                    int initializerErrorCount = AssignHeapConstants();
                    totalErrorCount += initializerErrorCount;

                    if (initializerErrorCount == 0)
                    {
                        foreach (CompilationModule module in modules)
                        {
                            module.programAsset.ApplyProgram();
                        }
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
            {
                if (modules.Length > 5)
                {
                    Debug.Log($"[UdonSharp] Compile of {modules.Length} scripts finished in {compileTimer.Elapsed.ToString("mm\\:ss\\.fff")}");
                }
                else
                {
                    Debug.Log($"[UdonSharp] Compile of script{(modules.Length > 1 ? "s" : "")} {string.Join(", ", modules.Select(e => Path.GetFileName(AssetDatabase.GetAssetPath(e.programAsset.sourceCsScript))))} finished in {compileTimer.Elapsed.ToString("mm\\:ss\\.fff")}");
                }
            }

            Profiler.EndSample();
        }

        public int AssignHeapConstants()
        {
            CompilationModule[] compiledModules = modules.Where(e => e.ErrorCount == 0).ToArray();

            foreach (CompilationModule module in compiledModules)
            {
                IUdonProgram program = module.programAsset.GetRealProgram();

                if (program != null)
                {
                    foreach (SymbolDefinition symbol in module.moduleSymbols.GetAllUniqueChildSymbols())
                    {
                        uint symbolAddress = program.SymbolTable.GetAddressFromSymbol(symbol.symbolUniqueName);

                        if (symbol.symbolDefaultValue != null)
                        {
                            program.Heap.SetHeapVariable(symbolAddress, symbol.symbolDefaultValue, symbol.symbolCsType);
                        }
                        else if (symbol.symbolCsType.IsArray &&
                                (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public))) // Initialize null array fields to a 0-length array like Unity does
                        {
                            program.Heap.SetHeapVariable(symbolAddress, System.Activator.CreateInstance(symbol.symbolCsType, new object[] { 0 }), symbol.symbolCsType);
                        }
                    }
                }
            }

            int fieldInitializerErrorCount = RunFieldInitalizers(compiledModules);

            foreach (CompilationModule module in compiledModules)
            {
                IUdonProgram program = module.programAsset.GetRealProgram();

                if (program != null)
                {
                    // Do not let users assign null to array fields, Unity does not allow this in its normal handling
                    foreach (SymbolDefinition symbol in module.moduleSymbols.GetAllUniqueChildSymbols())
                    {
                        uint symbolAddress = program.SymbolTable.GetAddressFromSymbol(symbol.symbolUniqueName);

                        if (symbol.symbolCsType.IsArray &&
                                (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public))) // Initialize null array fields to a 0-length array like Unity does
                        {
                            object currentArrayValue = program.Heap.GetHeapVariable(symbolAddress);

                            if (currentArrayValue == null)
                            {
                                program.Heap.SetHeapVariable(symbolAddress, System.Activator.CreateInstance(symbol.symbolCsType, new object[] { 0 }), symbol.symbolCsType);
                            }
                        }
                    }
                }
            }

            return fieldInitializerErrorCount;
        }

        private int RunFieldInitalizers(CompilationModule[] compiledModules)
        {
            CompilationModule[] modulesToInitialize = compiledModules.Where(e => e.fieldsWithInitializers.Count > 0).ToArray();
            
            // We don't need to run the costly compilation if the user hasn't defined any fields with initializers
            if (modulesToInitialize.Length == 0)
                return 0;

            int initializerErrorCount = 0;

            SyntaxTree[] initializerTrees = new SyntaxTree[modulesToInitialize.Length];
            StringBuilder[] codeStringBuilders = new StringBuilder[modulesToInitialize.Length];

            for (int moduleIdx = 0; moduleIdx < modulesToInitialize.Length; ++moduleIdx)
            {
                CompilationModule module = modulesToInitialize[moduleIdx];

                CodeCompileUnit compileUnit = new CodeCompileUnit();
                CodeNamespace ns = new CodeNamespace("FieldInitialzers");
                compileUnit.Namespaces.Add(ns);
                foreach (var resolverUsingNamespace in module.resolver.usingNamespaces)
                {
                    if (!string.IsNullOrEmpty(resolverUsingNamespace))
                        ns.Imports.Add(new CodeNamespaceImport(resolverUsingNamespace));
                }

                CodeTypeDeclaration _class = new CodeTypeDeclaration($"Initializer{moduleIdx}");
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
                        FieldDefinition fieldDef = module.compiledClassDefinition?.fieldDefinitions?.Find(e => (e.fieldSymbol.declarationType == SymbolDeclTypeFlags.Private || e.fieldSymbol.declarationType == SymbolDeclTypeFlags.Public) &&
                                                                                                                e.fieldSymbol.symbolOriginalName == variable.Identifier.ToString());

                        string typeQualifiedName = type.ToString();
                        if (fieldDef != null)
                        {
                            if (fieldDef.fieldSymbol.symbolCsType.Namespace.Length == 0)
                                typeQualifiedName = fieldDef.fieldSymbol.symbolCsType.Name;
                            else
                                typeQualifiedName = fieldDef.fieldSymbol.symbolCsType.Namespace + "." + fieldDef.fieldSymbol.symbolCsType.Name;
                        }

                        if (variable.Initializer != null)
                        {
                            string name = variable.Identifier.ToString();
                            if (isConst)
                            {
                                _class.Members.Add(new CodeSnippetTypeMember($"const {typeQualifiedName} {name} {variable.Initializer};"));
                            }
                            else
                            {
                                method.Statements.Add(new CodeSnippetStatement($"{typeQualifiedName} {name} {variable.Initializer};"));
                            }

                            method.Statements.Add(new CodeSnippetStatement(
                                $"program.Heap.SetHeapVariable(program.SymbolTable.GetAddressFromSymbol(\"{variable.Identifier}\"), {name});"));

                            count++;
                        }
                    }
                }

                StringBuilder sb = new StringBuilder();
                codeStringBuilders[moduleIdx] = sb;

                CSharpCodeProvider provider = new CSharpCodeProvider();
                using (StringWriter streamWriter = new StringWriter(sb))
                {
                    provider.GenerateCodeFromCompileUnit(compileUnit, streamWriter, new CodeGeneratorOptions());
                }

                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sb.ToString());

                initializerTrees[moduleIdx] = syntaxTree;
            }

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var references = new List<MetadataReference>();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!assemblies[i].IsDynamic && assemblies[i].Location.Length > 0)
                    references.Add(MetadataReference.CreateFromFile(assemblies[i].Location));
            }

            CSharpCompilation compilation = CSharpCompilation.Create(
                $"init{initAssemblyCounter++}",
                syntaxTrees: initializerTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var memoryStream = new MemoryStream())
            {
                EmitResult result = compilation.Emit(memoryStream);
                if (!result.Success)
                {
                    // todo: make these errors point to the correct source files
                    bool error = false;
                    foreach (Diagnostic diagnostic in result.Diagnostics)
                    {
                        if (diagnostic.Severity == DiagnosticSeverity.Error)
                        {
                            Debug.LogError(diagnostic);
                            error = true;
                            initializerErrorCount++;
                        }
                    }

                    if (error)
                        Debug.LogError($"Generated Source code: {string.Join("\n", codeStringBuilders.Select(e => e.ToString()))}");
                }
                else
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    Assembly assembly = Assembly.Load(memoryStream.ToArray());

                    for (int moduleIdx = 0; moduleIdx < modulesToInitialize.Length; ++moduleIdx)
                    {
                        CompilationModule module = modulesToInitialize[moduleIdx];
                        IUdonProgram program = module.programAsset.GetRealProgram();

                        System.Type cls = assembly.GetType($"FieldInitialzers.Initializer{moduleIdx}");
                        MethodInfo methodInfo = cls.GetMethod("DoInit", BindingFlags.Public | BindingFlags.Static);
                        methodInfo.Invoke(null, new[] { program });

                        foreach (var fieldDeclarationSyntax in module.fieldsWithInitializers)
                        {
                            foreach (var variable in fieldDeclarationSyntax.Declaration.Variables)
                            {
                                string varName = variable.Identifier.ToString();

                                object heapValue = program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(varName));

                                if (heapValue != null && UdonSharpUtils.IsUserDefinedType(heapValue.GetType()))
                                    UdonSharpUtils.LogBuildError($"Field: '{varName}' UdonSharp does not yet support field initializers on user-defined types or jagged arrays", AssetDatabase.GetAssetPath(module.programAsset.sourceCsScript), 0, 0);
                            }
                        }
                    }
                }
            }

            return initializerErrorCount;
        }

        private List<ClassDefinition> BuildClassDefinitions()
        {
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            List<UdonSharpProgramAsset> udonSharpPrograms = new List<UdonSharpProgramAsset>();

            foreach (string dataGuid in udonSharpDataAssets)
            {
                udonSharpPrograms.Add(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid)));
            }

            List<ClassDefinition> classDefinitions = new List<ClassDefinition>();

            foreach (UdonSharpProgramAsset udonSharpProgram in udonSharpPrograms)
            {
                if (udonSharpProgram.sourceCsScript == null)
                    continue;

                string sourcePath = AssetDatabase.GetAssetPath(udonSharpProgram.sourceCsScript);
                string programSource = File.ReadAllText(sourcePath);

                ResolverContext resolver = new ResolverContext();
                SymbolTable classSymbols = new SymbolTable(resolver, null);
                LabelTable classLabels = new LabelTable();
                
                SyntaxTree tree = CSharpSyntaxTree.ParseText(programSource);

                ClassVisitor classVisitor = new ClassVisitor(resolver, classSymbols, classLabels);

                try
                {
                    classVisitor.Visit(tree.GetRoot());
                }
                catch (System.Exception e)
                {
                    UdonSharpUtils.LogBuildError($"{e.GetType()}: {e.Message}", sourcePath.Replace("/", "\\"), 0, 0);

                    return null;
                }

                classVisitor.classDefinition.classScript = udonSharpProgram.sourceCsScript;
                classDefinitions.Add(classVisitor.classDefinition);
            }

            return classDefinitions;
        }
    }
}