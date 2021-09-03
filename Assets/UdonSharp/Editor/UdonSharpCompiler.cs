
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp;
using UdonSharp.Serialization;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Compiler
{
    public class UdonSharpCompiler
    {
        public class CompileError
        {
            public MonoScript script;
            public string errorStr;
            public int lineIdx;
            public int charIdx;
        }

        public class CompileTaskResult
        {
            public UdonSharpProgramAsset programAsset;
            public string compiledAssembly;
            public uint symbolCount;
            public List<CompileError> compileErrors = new List<CompileError>();
        }

        private CompilationModule[] modules;
        private bool isEditorBuild = true;

        public delegate void CompileCallback(UdonSharpProgramAsset[] compiledProgramAssets);

        [PublicAPI] public static event CompileCallback beforeCompile;
        [PublicAPI] public static event CompileCallback afterCompile;

        private static int initAssemblyCounter = 0;

        public UdonSharpCompiler(UdonSharpProgramAsset programAsset, bool editorBuild = true)
        {
            modules = new CompilationModule[] { new CompilationModule(programAsset) };
            isEditorBuild = editorBuild;
        }

        public UdonSharpCompiler(UdonSharpProgramAsset[] programAssets, bool editorBuild = true)
        {
            modules = programAssets.Where(e => e.sourceCsScript != null).Select(e => new CompilationModule(e)).ToArray();
            isEditorBuild = editorBuild;
        }

        void CheckProgramAssetCollisions(UdonSharpProgramAsset[] programs)
        {
            EditorUtility.DisplayProgressBar("UdonSharp Compile", "Validating Program Assets...", 0f);

            Dictionary<MonoScript, List<UdonSharpProgramAsset>> scriptToAssetMap = new Dictionary<MonoScript, List<UdonSharpProgramAsset>>();

            foreach (UdonSharpProgramAsset programAsset in programs)
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
            
            foreach (var scriptAssetMapping in scriptToAssetMap)
            {
                if (scriptAssetMapping.Value.Count > 1)
                {
                    Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] Script {Path.GetFileName(AssetDatabase.GetAssetPath(scriptAssetMapping.Key))} is referenced by {scriptAssetMapping.Value.Count} UdonSharpProgramAssets, scripts should only be referenced by 1 program asset. This will cause issues.\n" +
                        "Referenced program assets:\n" +
                        string.Join(",\n", scriptAssetMapping.Value.Select(e => AssetDatabase.GetAssetPath(e))));
                }
            }
        }

        public void Compile()
        {
            Profiler.BeginSample("UdonSharp Compile");

            System.Diagnostics.Stopwatch compileTimer = new System.Diagnostics.Stopwatch();
            compileTimer.Start();

            int totalErrorCount = 0;

            try
            {
                EditorUtility.DisplayProgressBar("UdonSharp Compile", "Initializing...", 0f);

                UdonSharpProgramAsset[] allPrograms = UdonSharpProgramAsset.GetAllUdonSharpPrograms();
                List<(UdonSharpProgramAsset, string)> programAssetsAndPaths = new List<(UdonSharpProgramAsset, string)>();

                foreach (UdonSharpProgramAsset programAsset in allPrograms)
                {
                    if (programAsset == null)
                        continue;

                    if (programAsset.sourceCsScript == null)
                    {
                        Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] Program asset '{AssetDatabase.GetAssetPath(programAsset)}' is missing a source C# script");
                        continue;
                    }

                    programAssetsAndPaths.Add((programAsset, AssetDatabase.GetAssetPath(programAsset.sourceCsScript)));

                    programAsset.compileErrors.Clear(); // Clear compile errors to keep them from stacking if not resolved
                }

                CheckProgramAssetCollisions(allPrograms);

                UdonSharpProgramAsset[] programAssetsToCompile = modules.Select(e => e.programAsset).Where(e => e != null && e.sourceCsScript != null).ToArray();
                
                EditorUtility.DisplayProgressBar("UdonSharp Compile", "Executing pre-build events...", 0f);

                try
                {
                    beforeCompile?.Invoke(programAssetsToCompile);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Exception thrown by pre compile listener\n{e}");
                }

                EditorUtility.DisplayProgressBar("UdonSharp Compile", "Parsing Syntax Trees...", 0f);

                object syntaxTreeLock = new object();
                List<(UdonSharpProgramAsset, Microsoft.CodeAnalysis.SyntaxTree)> programsAndSyntaxTrees = new List<(UdonSharpProgramAsset, Microsoft.CodeAnalysis.SyntaxTree)>();
                Dictionary<UdonSharpProgramAsset, (string, Microsoft.CodeAnalysis.SyntaxTree)> syntaxTreeSourceLookup = new Dictionary<UdonSharpProgramAsset, (string, Microsoft.CodeAnalysis.SyntaxTree)>();

                string[] defines = UdonSharpUtils.GetProjectDefines(isEditorBuild);

                Parallel.ForEach(programAssetsAndPaths, (currentProgram) =>
                {
                    string programSource = UdonSharpUtils.ReadFileTextSync(currentProgram.Item2);

#pragma warning disable CS1701 // Warning about System.Collections.Immutable versions potentially not matching
                    Microsoft.CodeAnalysis.SyntaxTree programSyntaxTree = CSharpSyntaxTree.ParseText(programSource, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithPreprocessorSymbols(defines));
#pragma warning restore CS1701

                    lock (syntaxTreeLock)
                    {
                        programsAndSyntaxTrees.Add((currentProgram.Item1, programSyntaxTree));
                        syntaxTreeSourceLookup.Add(currentProgram.Item1, (programSource, programSyntaxTree));
                    }
                });

                foreach (var syntaxTree in programsAndSyntaxTrees)
                {
                    foreach (Diagnostic diagnostic in syntaxTree.Item2.GetDiagnostics())
                    {
                        if (diagnostic.Severity == DiagnosticSeverity.Error)
                        {
                            totalErrorCount++;

                            LinePosition linePosition = diagnostic.Location.GetLineSpan().StartLinePosition;

                            string errorMessage = UdonSharpUtils.LogBuildError($"error {diagnostic.Descriptor.Id}: {diagnostic.GetMessage()}", AssetDatabase.GetAssetPath(syntaxTree.Item1.sourceCsScript), linePosition.Line, linePosition.Character);
                            syntaxTree.Item1.compileErrors.Add(errorMessage);
                        }
                    }
                }

                List<ClassDefinition> classDefinitions = null;

                // Bind stage
                if (totalErrorCount == 0)
                {
                    EditorUtility.DisplayProgressBar("UdonSharp Compile", "Building class definitions...", 0f);

                    classDefinitions = BindPrograms(programsAndSyntaxTrees);

                    if (classDefinitions == null)
                        totalErrorCount++;
                }

                // Compile stage
                if (totalErrorCount == 0)
                {
#if UDONSHARP_DEBUG // Single threaded compile
                    List<CompileTaskResult> compileTasks = new List<CompileTaskResult>();

                    for (int i = 0; i < modules.Length; ++i)
                    {
                        CompilationModule module = modules[i];
                        var sourceTree = syntaxTreeSourceLookup[module.programAsset];
                    
                        EditorUtility.DisplayProgressBar("UdonSharp Compile",
                                                         $"Compiling scripts ({i}/{modules.Length})...",
                                                         Mathf.Clamp01((i / ((float)modules.Length + 1f))));

                        compileTasks.Add(module.Compile(classDefinitions, sourceTree.Item2, sourceTree.Item1, isEditorBuild));
                    }
#else
                    List<Task<CompileTaskResult>> compileTasks = new List<Task<CompileTaskResult>>();

                    foreach (CompilationModule module in modules)
                    {
                        var sourceTree = syntaxTreeSourceLookup[module.programAsset];

                        compileTasks.Add(Task.Factory.StartNew(() => module.Compile(classDefinitions, sourceTree.Item2, sourceTree.Item1, isEditorBuild)));
                    }
#endif

                    int totalTaskCount = compileTasks.Count;

                    while (compileTasks.Count > 0)
                    {
#if UDONSHARP_DEBUG
                        CompileTaskResult compileResult = compileTasks.Last();
                        compileTasks.RemoveAt(compileTasks.Count - 1);
#else
                        Task<CompileTaskResult> compileResultTask = Task.WhenAny(compileTasks).Result;
                        compileTasks.Remove(compileResultTask);

                        CompileTaskResult compileResult = compileResultTask.Result;
#endif

                        if (compileResult.compileErrors.Count == 0)
                        {
                            compileResult.programAsset.SetUdonAssembly(compileResult.compiledAssembly);
                            bool assembled = compileResult.programAsset.AssembleCsProgram(compileResult.symbolCount);
                            compileResult.programAsset.SetUdonAssembly("");
                            UdonSharpEditorCache.Instance.SetUASMStr(compileResult.programAsset, compileResult.compiledAssembly);

                            if (!assembled)
                            {
                                FieldInfo assemblyError = typeof(VRC.Udon.Editor.ProgramSources.UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance);
                                string error = (string)assemblyError.GetValue(compileResult.programAsset);

                                totalErrorCount++;

                                if (!string.IsNullOrEmpty(error))
                                    UdonSharpUtils.LogBuildError(error, AssetDatabase.GetAssetPath(compileResult.programAsset.sourceCsScript), 0, 0);
                                else
                                    UdonSharpUtils.LogBuildError("Failed to assemble program", AssetDatabase.GetAssetPath(compileResult.programAsset.sourceCsScript), 0, 0);
                            }
                        }
                        else
                        {
                            foreach (CompileError error in compileResult.compileErrors)
                            {
                                string errorMessage = UdonSharpUtils.LogBuildError(error.errorStr,
                                                                                   AssetDatabase.GetAssetPath(error.script).Replace("/", "\\"),
                                                                                   error.lineIdx,
                                                                                   error.charIdx);

                                compileResult.programAsset.compileErrors.Add(errorMessage);
                            }

                            totalErrorCount += compileResult.compileErrors.Count;
                        }

                        int processedTaskCount = totalTaskCount - compileTasks.Count;

#if !UDONSHARP_DEBUG
                        EditorUtility.DisplayProgressBar("UdonSharp Compile",
                                                         $"Compiling scripts ({processedTaskCount}/{totalTaskCount})...",
                                                         Mathf.Clamp01((processedTaskCount / ((float)totalTaskCount + 1f))));
#endif
                    }

                    if (totalErrorCount == 0)
                    {
                        EditorUtility.DisplayProgressBar("UdonSharp Compile", "Assigning constants...", 1f);
                        int initializerErrorCount = AssignHeapConstants();
                        totalErrorCount += initializerErrorCount;
                    }

                    if (totalErrorCount == 0)
                    {
                        foreach (CompilationModule module in modules)
                        {
                            module.programAsset.ApplyProgram();
                            UdonSharpEditorCache.Instance.UpdateSourceHash(module.programAsset, syntaxTreeSourceLookup[module.programAsset].Item1);
                        }

                        EditorUtility.DisplayProgressBar("UdonSharp Compile", "Post Build Scene Fixup", 1f);
                        UdonSharpEditorCache.Instance.LastBuildType = isEditorBuild ? UdonSharpEditorCache.DebugInfoType.Editor : UdonSharpEditorCache.DebugInfoType.Client;
                        UdonSharpEditorManager.RunPostBuildSceneFixup();
                    }
                }

                if (totalErrorCount > 0)
                {
                    foreach (CompilationModule module in modules)
                    {
                        UdonSharpEditorCache.Instance.ClearSourceHash(module.programAsset);
                    }
                }
                
                EditorUtility.DisplayProgressBar("UdonSharp Compile", "Executing post-build events...", 1f);

                try
                {
                    afterCompile?.Invoke(programAssetsToCompile);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Exception thrown by post compile listener\n{e}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Profiler.EndSample();
            }

            compileTimer.Stop();

            if (totalErrorCount == 0)
            {
                if (modules.Length > 5)
                {
                    Debug.Log($"[<color=#0c824c>UdonSharp</color>] Compile of {modules.Length} scripts finished in {compileTimer.Elapsed.ToString("mm\\:ss\\.fff")}");
                }
                else
                {
                    Debug.Log($"[<color=#0c824c>UdonSharp</color>] Compile of script{(modules.Length > 1 ? "s" : "")} {string.Join(", ", modules.Select(e => Path.GetFileName(AssetDatabase.GetAssetPath(e.programAsset.sourceCsScript))))} finished in {compileTimer.Elapsed.ToString("mm\\:ss\\.fff")}");
                }
            }
        }

        List<ClassDefinition> BindPrograms(List<(UdonSharpProgramAsset, Microsoft.CodeAnalysis.SyntaxTree)> allPrograms)
        {
            List<BindTaskResult> bindTaskResults = new List<BindTaskResult>();

            List<ClassDefinitionBinder> classBinders = new List<ClassDefinitionBinder>();

            foreach (var programAssetAndTree in allPrograms)
            {
                classBinders.Add(new ClassDefinitionBinder(programAssetAndTree.Item1, programAssetAndTree.Item2));
            }

#if UDONSHARP_DEBUG // Single threaded bind
            List<BindTaskResult> bindTasks = new List<BindTaskResult>();

            foreach (ClassDefinitionBinder binder in classBinders)
            {
                bindTasks.Add(binder.BuildClassDefinition());
            }
#else
            List<Task<BindTaskResult>> bindTasks = new List<Task<BindTaskResult>>();

            foreach (ClassDefinitionBinder binder in classBinders)
            {
                bindTasks.Add(Task.Factory.StartNew(() => binder.BuildClassDefinition()));
            }
#endif

            int errorCount = 0;
            List<ClassDefinition> classDefinitions = new List<ClassDefinition>();

            while (bindTasks.Count > 0)
            {
#if UDONSHARP_DEBUG
                BindTaskResult bindResult = bindTasks.Last();
                bindTasks.RemoveAt(bindTasks.Count - 1);
#else
                Task<BindTaskResult> bindResultTask = Task.WhenAny(bindTasks).Result;
                bindTasks.Remove(bindResultTask);

                BindTaskResult bindResult = bindResultTask.Result;
#endif

                if (bindResult.compileErrors.Count == 0)
                {
                    classDefinitions.Add(bindResult.classDefinition);

                    if (bindResult.sourceScript.GetClass() == null && 
                        bindResult.classDefinition.userClassType.Name != Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(bindResult.sourceScript)))
                    {
                        Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] {AssetDatabase.GetAssetPath(bindResult.sourceScript)}: Class name does not match file name, Unity requires that both names match exactly for the editor to work properly.", bindResult.sourceScript);
                    }
                }
                else
                {
                    errorCount++;

                    foreach (CompileError bindError in bindResult.compileErrors)
                    {
                        string buildError = UdonSharpUtils.LogBuildError(bindError.errorStr, AssetDatabase.GetAssetPath(bindResult.sourceScript), bindError.lineIdx, bindError.charIdx);
                        bindResult.programAsset.compileErrors.Add(buildError);
                    }
                }
            }

            if (errorCount == 0)
                return classDefinitions;

            return null;
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
                        else if (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public)) // Initialize null array fields to a 0-length array like Unity does
                        {
                            if (symbol.symbolCsType.IsArray)
                                program.Heap.SetHeapVariable(symbolAddress, System.Activator.CreateInstance(symbol.symbolCsType, new object[] { 0 }), symbol.symbolCsType);
                            else if (symbol.symbolCsType == typeof(string))
                                program.Heap.SetHeapVariable(symbolAddress, "", symbol.symbolCsType);
                        }
                    }
                }
            }

            int fieldInitializerErrorCount = RunFieldInitalizers(compiledModules);

            if (fieldInitializerErrorCount > 0)
            {
                foreach (CompilationModule module in compiledModules)
                {
                    module.programAsset.compileErrors.Add("Initializer error on an UdonSharpBehaviour, see output log for details.");
                }
            }

            foreach (CompilationModule module in compiledModules)
            {
                IUdonProgram program = module.programAsset.GetRealProgram();

                if (program != null)
                {
                    // Do not let users assign null to array fields, Unity does not allow this in its normal handling
                    foreach (SymbolDefinition symbol in module.moduleSymbols.GetAllUniqueChildSymbols())
                    {
                        uint symbolAddress = program.SymbolTable.GetAddressFromSymbol(symbol.symbolUniqueName);

                        if (symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public)) // Initialize null array fields to a 0-length array like Unity does
                        {
                            if (symbol.symbolCsType.IsArray)
                            {
                                if (program.Heap.GetHeapVariable(symbolAddress) == null)
                                {
                                    program.Heap.SetHeapVariable(symbolAddress, System.Activator.CreateInstance(symbol.symbolCsType, new object[] { 0 }), symbol.symbolCsType);
                                }
                            }
                            else if (symbol.symbolCsType == typeof(string))
                            {
                                if (program.Heap.GetHeapVariable(symbolAddress) == null)
                                {
                                    program.Heap.SetHeapVariable(symbolAddress, "", symbol.symbolCsType);
                                }
                            }
                        }

                        // Default to empty string on synced strings to prevent Udon sync from throwing errors
                        if (symbol.symbolCsType == typeof(string) &&
                            symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Private) &&
                            symbol.syncMode != UdonSyncMode.NotSynced)
                        {
                            if (program.Heap.GetHeapVariable(symbolAddress) == null)
                                program.Heap.SetHeapVariable(symbolAddress, "");
                        }
                    }
                }
            }

            return fieldInitializerErrorCount;
        }
        
        // Called from the generated assembly to assign values to the program
        static void SetHeapField<T>(IUdonProgram program, T value, string symbolName)
        {
            if (UdonSharpUtils.IsUserJaggedArray(typeof(T)))
            {
                Serializer<T> serializer = Serializer.CreatePooled<T>();

                SimpleValueStorage<object[]> arrayStorage = new SimpleValueStorage<object[]>();
                serializer.Write(arrayStorage, in value);
                
                program.Heap.SetHeapVariable<object[]>(program.SymbolTable.GetAddressFromSymbol(symbolName), arrayStorage.Value);
            }
            else
            {
                //program.Heap.SetHeapVariable<T>(program.SymbolTable.GetAddressFromSymbol(symbolName), value);
                throw new System.NotImplementedException(); // This should not get hit currently
            }
        }

        string GetFullTypeQualifiedName(System.Type type)
        {
            string namespaceStr = "";

            if (!string.IsNullOrEmpty(type.Namespace))
            {
                namespaceStr = type.Namespace + ".";
            }

            string nestedTypeStr = "";

            System.Type declaringType = type.DeclaringType;

            while (declaringType != null)
            {
                nestedTypeStr = $"{declaringType.Name}.{nestedTypeStr}";
                declaringType = declaringType.DeclaringType;
            }

            return namespaceStr + nestedTypeStr + type.Name;
        }

        private static List<MetadataReference> metadataReferences;

        private int RunFieldInitalizers(CompilationModule[] compiledModules)
        {
            CompilationModule[] modulesToInitialize = compiledModules.Where(e => e.fieldsWithInitializers.Count > 0).ToArray();
            
            // We don't need to run the costly compilation if the user hasn't defined any fields with initializers
            if (modulesToInitialize.Length == 0)
                return 0;

            int initializerErrorCount = 0;

            Microsoft.CodeAnalysis.SyntaxTree[] initializerTrees = new Microsoft.CodeAnalysis.SyntaxTree[modulesToInitialize.Length];
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
                method.Parameters.Add(new CodeParameterDeclarationExpression(typeof(MethodInfo), "heapSetMethod"));

                foreach (var fieldDeclarationSyntax in module.fieldsWithInitializers)
                {
                    var type = fieldDeclarationSyntax.Declaration.Type;
                    int count = 0;
                    bool isConst = fieldDeclarationSyntax.Modifiers.Any(t => t.ToString() == "const");
                    foreach (var variable in fieldDeclarationSyntax.Declaration.Variables)
                    {
                        FieldDefinition fieldDef = module.compiledClassDefinition?.fieldDefinitions?.Find(e => (e.fieldSymbol.declarationType == SymbolDeclTypeFlags.Private || e.fieldSymbol.declarationType == SymbolDeclTypeFlags.Public) &&
                                                                                                                e.fieldSymbol.symbolOriginalName == variable.Identifier.ToString());

                        string typeQualifiedName = type.ToString().Replace('+', '.');
                        if (fieldDef != null)
                            typeQualifiedName = GetFullTypeQualifiedName(fieldDef.fieldSymbol.symbolCsType);

                        if (variable.Initializer != null)
                        {
                            string name = variable.Identifier.ToString();

                            if (UdonSharpUtils.IsUserJaggedArray(fieldDef.fieldSymbol.userCsType))
                            {
                                if (fieldDef != null)
                                    typeQualifiedName = GetFullTypeQualifiedName(fieldDef.fieldSymbol.userCsType);

                                if (isConst)
                                    _class.Members.Add(new CodeSnippetTypeMember($"const {typeQualifiedName} {name} {variable.Initializer};"));
                                else
                                    method.Statements.Add(new CodeSnippetStatement($"{typeQualifiedName} {name} {variable.Initializer};"));

                                method.Statements.Add(new CodeSnippetStatement(
                                    "heapSetMethod.MakeGenericMethod(typeof(" + GetFullTypeQualifiedName(fieldDef.fieldSymbol.userCsType) + ")).Invoke(null, new object[] { program, " + name + ", \"" + variable.Identifier + "\"});"));
                            }
                            else
                            {
                                if (isConst)
                                    _class.Members.Add(new CodeSnippetTypeMember($"const {typeQualifiedName} {name} {variable.Initializer};"));
                                else
                                    method.Statements.Add(new CodeSnippetStatement($"{typeQualifiedName} {name} {variable.Initializer};"));

                                method.Statements.Add(new CodeSnippetStatement(
                                    $"program.Heap.SetHeapVariable(program.SymbolTable.GetAddressFromSymbol(\"{variable.Identifier}\"), {name});"));
                            }

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

                Microsoft.CodeAnalysis.SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sb.ToString());

                initializerTrees[moduleIdx] = syntaxTree;
            }

            if (metadataReferences == null)
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                metadataReferences = new List<MetadataReference>();

                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (!assemblies[i].IsDynamic && assemblies[i].Location.Length > 0)
                    {
                        PortableExecutableReference executableReference = null;

                        try
                        {
                            executableReference = MetadataReference.CreateFromFile(assemblies[i].Location);
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

#pragma warning disable CS1701 // Warning about System.Collections.Immutable versions potentially not matching
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"UdonSharpInitAssembly{initAssemblyCounter++}",
                syntaxTrees: initializerTrees,
                references: metadataReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
#pragma warning restore CS1701

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
                    Assembly assembly;

                    using (var loadScope = new UdonSharpUtils.UdonSharpAssemblyLoadStripScope())
                        assembly = Assembly.Load(memoryStream.ToArray());

                    for (int moduleIdx = 0; moduleIdx < modulesToInitialize.Length; ++moduleIdx)
                    {
                        CompilationModule module = modulesToInitialize[moduleIdx];
                        IUdonProgram program = module.programAsset.GetRealProgram();

                        System.Type cls = assembly.GetType($"FieldInitialzers.Initializer{moduleIdx}");
                        MethodInfo methodInfo = cls.GetMethod("DoInit", BindingFlags.Public | BindingFlags.Static);

                        try
                        {
                            methodInfo.Invoke(null, new object[] { program, typeof(UdonSharpCompiler).GetMethod("SetHeapField", BindingFlags.NonPublic | BindingFlags.Static) });
                        }
                        catch (System.Exception e)
                        {
                            UdonSharpUtils.LogBuildError($"Exception encountered in field initializer: {e}", AssetDatabase.GetAssetPath(module.programAsset.sourceCsScript), 0, 0);
                            initializerErrorCount++;
                            break;
                        }

                        foreach (var fieldDeclarationSyntax in module.fieldsWithInitializers)
                        {
                            foreach (var variable in fieldDeclarationSyntax.Declaration.Variables)
                            {
                                string varName = variable.Identifier.ToString();

                                object heapValue = program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(varName));

                                if (heapValue != null && UdonSharpUtils.IsUserDefinedType(heapValue.GetType()))
                                {
                                    string fieldError = $"Field: '{varName}' UdonSharp does not yet support field initializers on user-defined types";

                                    UdonSharpUtils.LogBuildError(fieldError, AssetDatabase.GetAssetPath(module.programAsset.sourceCsScript), 0, 0);

                                    module.programAsset.compileErrors.Add(fieldError);
                                }
                            }
                        }
                    }
                }
            }

            return initializerErrorCount;
        }

        class BindTaskResult
        {
            public UdonSharpProgramAsset programAsset;
            public ClassDefinition classDefinition;
            public MonoScript sourceScript;
            public List<CompileError> compileErrors = new List<CompileError>();
        }

        class ClassDefinitionBinder
        {
            UdonSharpProgramAsset programAsset;
            Microsoft.CodeAnalysis.SyntaxTree syntaxTree;

            public ClassDefinitionBinder(UdonSharpProgramAsset programAsset, Microsoft.CodeAnalysis.SyntaxTree syntaxTree)
            {
                this.programAsset = programAsset;
                this.syntaxTree = syntaxTree;
            }

            public BindTaskResult BuildClassDefinition()
            {
                ResolverContext resolver = new ResolverContext();
                SymbolTable classSymbols = new SymbolTable(resolver, null);

                classSymbols.OpenSymbolTable();

                LabelTable classLabels = new LabelTable();

                ClassVisitor classVisitor = new ClassVisitor(resolver, classSymbols, classLabels);

                BindTaskResult bindTaskResult = new BindTaskResult();
                bindTaskResult.programAsset = programAsset;
                bindTaskResult.sourceScript = programAsset.sourceCsScript;

                try
                {
                    classVisitor.Visit(syntaxTree.GetRoot());
                }
                catch (System.Exception e)
                {
                    SyntaxNode node = classVisitor.visitorContext.currentNode;

                    string errorString;
                    int charIndex;
                    int lineIndex;

                    if (node != null)
                    {
                        FileLinePositionSpan lineSpan = node.GetLocation().GetLineSpan();
                        
                        charIndex = lineSpan.StartLinePosition.Character;
                        lineIndex = lineSpan.StartLinePosition.Line;
                    }
                    else
                    {
                        charIndex = 0;
                        lineIndex = 0;
                    }

                    errorString = $"{e.GetType()}: {e.Message}";

#if UDONSHARP_DEBUG
                    Debug.LogException(e);
                    Debug.LogError(e.StackTrace);
#endif
                    
                    bindTaskResult.compileErrors.Add(new CompileError() { script = programAsset.sourceCsScript, errorStr = errorString, charIdx = charIndex, lineIdx = lineIndex });

                    classSymbols.CloseSymbolTable();

                    return bindTaskResult;
                }

                classSymbols.CloseSymbolTable();

                classVisitor.classDefinition.classScript = programAsset.sourceCsScript;

                bindTaskResult.classDefinition = classVisitor.classDefinition;

                return bindTaskResult;
            }
        }
    }
}