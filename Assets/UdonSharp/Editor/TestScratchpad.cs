using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public class TestScratchpad : EditorWindow
{
    [MenuItem("Udon Sharp/Scratchpad")]
    static void Init()
    {
        GetWindow<TestScratchpad>("Scratchpad");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Hello there");

        if (GUILayout.Button("Run Test"))
        {
            //TestSyntaxTree();
            TestSemanticModel();
        }
    }

    static int assemblyCounter = 0;

    static List<MetadataReference> metadataReferences;

    void TestSyntaxTree()
    {
        var allScripts = UdonSharpSettings.FilterBlacklistedPaths(Directory.GetFiles("Assets/", "*.cs", SearchOption.AllDirectories));

        HashSet<string> assemblySourcePaths = new HashSet<string>();

        foreach (Assembly asm in CompilationPipeline.GetAssemblies(AssembliesType.Editor).Union(CompilationPipeline.GetAssemblies(AssembliesType.Player)))
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

        allScripts = filteredPaths;

        System.Diagnostics.Stopwatch parseTimer = new System.Diagnostics.Stopwatch();
        parseTimer.Start();

        string[] defines = UdonSharpUtils.GetProjectDefines(true);

        ConcurrentBag<Microsoft.CodeAnalysis.SyntaxTree> syntaxTrees = new ConcurrentBag<SyntaxTree>();

        Parallel.ForEach(allScripts, (currentProgram) =>
        {
            string programSource = UdonSharpUtils.ReadFileTextSync(currentProgram);

#pragma warning disable CS1701 // Warning about System.Collections.Immutable versions potentially not matching
            Microsoft.CodeAnalysis.SyntaxTree programSyntaxTree = CSharpSyntaxTree.ParseText(programSource, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithPreprocessorSymbols(defines).WithLanguageVersion(LanguageVersion.CSharp7_2));
#pragma warning restore CS1701

            syntaxTrees.Add(programSyntaxTree);
        });

        //foreach (string script in allScripts.OrderBy(e => e))
        //    Debug.Log(script);

        Debug.Log($"Parsed all .cs files in {parseTimer.Elapsed.TotalSeconds * 1000f}ms");

        parseTimer.Restart();

#pragma warning disable CS1701 // Warning about System.Collections.Immutable versions potentially not matching
        CSharpCompilation compilation = CSharpCompilation.Create(
            $"UdonSharpCompileAssembly{assemblyCounter++}",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
#pragma warning restore CS1701

        //compilation.GetSemanticModel

        //Debug.Log($"Ran compilation in {parseTimer.Elapsed.TotalSeconds * 1000f}ms");

        //parseTimer.Restart();
        var diagnostics = compilation.GetDiagnostics();

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                Debug.LogError($"{diagnostic},\n {diagnostic.Location.SourceTree}");
        }

        //Debug.Log($"Queried diagnostics in {parseTimer.Elapsed.TotalSeconds * 1000f}ms");

        //parseTimer.Restart();

        ConcurrentBag<List<SymbolInfo>> symbolInfos = new ConcurrentBag<List<SymbolInfo>>();

        Parallel.ForEach(syntaxTrees, (tree) =>
        {
            SemanticModel model = compilation.GetSemanticModel(tree);

            List<SymbolInfo> modelSymbols = new List<SymbolInfo>();

            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                modelSymbols.Add(model.GetSymbolInfo(node));
            }

            symbolInfos.Add(modelSymbols);
        });

        //foreach (List<SymbolInfo> symbolInfoList in symbolInfos)
        //{
        //    foreach (SymbolInfo symbolInfo in symbolInfoList)
        //    {
        //        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        //            Debug.Log($"{symbolInfo.Symbol}: {methodSymbol.MethodKind}, loc: {methodSymbol.Locations.FirstOrDefault()}");
        //    }
        //}

        Debug.Log($"Queried semantic models in {parseTimer.Elapsed.TotalSeconds * 1000f}ms");
    }

    void TestSemanticModel()
    {
        string testClass = @"
using System;
using UnityEngine;
using UnityEditor;

//public enum MyEnum
//{
//    A = 1,
//    B = A + 2,
//    C = B + 5,
//}

//public static class StaticClass
//{
//    public const string MY_CONST = ""my constant value!"";
//}

public partial class BaseTest<T> : MonoBehaviour
{
    //private Vector2 baseVector = new Vector2(1, 2);

    //protected void MyInheritedMethod()
    //{
    //    Debug.Log(""hi"");
    //}

    public void MyGeneric<V>(T val1, V val2)
    {
        
    }

    int myInt;
}

//public class TestingClass : BaseTest
//{
//    //Vector3 Vector3 = new Vector3(3f, 2f, 1f);

//    //[MenuItem(""Hello this is a test"" + ""aaaa"" + StaticClass.MY_CONST)]
//    void DoThing()
//    {
//        //Vector3 i = default;
//        //i.x = (float)4.0;
//        //Debug.Log(Vector3.magnitude);

//        //Debug.Log((int)MyEnum.C);
//        if (this)
//            Debug.Log(""test"");

//        //UnityEngine.Debug.Log(""Hello!"" + "" test"");
//        //Debug.Log(this);

//        byte testByte = 0;
//        testByte += 1;
//    }
//}
";

        string testClass2 = @"
using UnityEngine;
using System;

public partial class BaseTest<T>
{
    //public float myTestFloat;
    //public float[][][] myJaggedArray;

    public float MyFloatProp { get; set; }

    public void LogThing()
    {
        Debug.Log(MyFloatProp + 6f);
        //BaseTest<T> self = this;

        //self.MyFloatProp = 5f;
    }
}
";

        SyntaxTree programSyntaxTree = CSharpSyntaxTree.ParseText(testClass, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithLanguageVersion(LanguageVersion.CSharp7_2));
        SyntaxTree programSyntaxTree2 = CSharpSyntaxTree.ParseText(testClass2, CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithLanguageVersion(LanguageVersion.CSharp7_2));

        CSharpCompilation compilation = CSharpCompilation.Create(
            $"UdonSharpCompileAssembly{assemblyCounter++}",
            syntaxTrees: new[] { programSyntaxTree, programSyntaxTree2 },
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics();

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                Debug.LogError($"{diagnostic},\n {diagnostic.Location.SourceTree}");
        }

        SemanticModel model = compilation.GetSemanticModel(programSyntaxTree);
        SemanticModel model2 = compilation.GetSemanticModel(programSyntaxTree2);

        INamedTypeSymbol modelClass0 = model.GetDeclaredSymbol(programSyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault());
        INamedTypeSymbol modelClass1 = model2.GetDeclaredSymbol(programSyntaxTree2.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault());

        Debug.Log(modelClass0);
        Debug.Log(modelClass1);

        Debug.Log(modelClass0 == modelClass1);
        Debug.Log(modelClass0.Equals(modelClass1));

        INamedTypeSymbol constructedType = modelClass0.Construct(modelClass0);

        Debug.Log($"{constructedType} {constructedType.GetType()}");

        IMethodSymbol genericMethod = constructedType.GetMembers().OfType<IMethodSymbol>().First();

        Debug.Log(genericMethod);

        IMethodSymbol constructedMethod = genericMethod.Construct(constructedType);

        Debug.Log($"{constructedMethod}, {constructedMethod.GetType()}");

        //IPropertySymbol genericProperty = constructedType.GetMembers().OfType<IPropertySymbol>().First();

        Debug.Log(string.Join(", ", modelClass0.TypeArguments.Select(e => e.GetType())));
        Debug.Log(string.Join(", ", constructedType.TypeArguments.Select(e => e.GetType())));

        Debug.Log(modelClass0.TypeArguments.First().TypeKind);
        Debug.Log(constructedType.TypeArguments.First().TypeKind);

        //Debug.Log(((IArrayTypeSymbol)modelClass0.GetMembers().OfType<IFieldSymbol>().First(e => e.Name == "myJaggedArray").Type).ElementType.GetType());

        //return;

        var variableDeclNodes = programSyntaxTree2.GetRoot().DescendantNodes();

        foreach (SyntaxNode syntaxNode in variableDeclNodes)
        {
            //if (syntaxNode is ClassDeclarationSyntax classDecl)
            //{
            //    ITypeSymbol classSymbol = model.GetDeclaredSymbol(classDecl) as ITypeSymbol;

            //    Debug.Log($"Class declaration: {classSymbol}");

            //    int indent = 1;

            //    while (classSymbol != null)
            //    {
            //        foreach (var classMember in classSymbol.GetMembers())
            //        {
            //            Debug.Log($"{new string('\t', indent)}{classMember}, T:{classMember.GetType()}, loc: {classMember.Locations.First().SourceTree?.GetRoot()?.ToString() ?? "null"}");
            //        }

            //        classSymbol = classSymbol.BaseType;
            //        indent++;
            //    }
            //}

            SymbolInfo symbolInfo = model2.GetSymbolInfo(syntaxNode);
            TypeInfo typeInfo = model2.GetTypeInfo(syntaxNode);

            //if (typeInfo.Type != null)
            //{
            //    Debug.Log($"TypeInfo: {syntaxNode.Kind()}, {typeInfo.Type}, typeof: {typeInfo.Type.GetType()}\n{syntaxNode}");
            //}

            IOperation operation = model2.GetOperation(syntaxNode);

            if (operation != null)
            {
                Debug.Log($"Operation: {operation}, kind: {operation.Kind}");

                IEnumerable<IOperation> childrenOps = operation.Children;

                int indentLevel = 1;

                foreach (IOperation child in childrenOps)
                {
                    if (child is IConversionOperation conversion)
                    {
                        Debug.Log($"{new string('\t', indentLevel)}Conversion op: {conversion.OperatorMethod}");
                    }
                    else
                        Debug.Log($"{new string('\t', indentLevel)}op: {child}, kind {child.Kind}");
                }

                if (operation is IPropertyReferenceOperation propertyReferenceOperation)
                {
                    Debug.Log($"\t\tProperty reference {propertyReferenceOperation.Property}");
                }
            }

            if (symbolInfo.Symbol != null)
            {
                Debug.Log($"{syntaxNode.Kind()}, {symbolInfo.Symbol}, typeof: {symbolInfo.Symbol.GetType()}\n{syntaxNode}");


                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    //Debug.Log($"    Field symbol: const: {fieldSymbol.IsConst}" + (fieldSymbol.IsConst ? $", constant value: {fieldSymbol.ConstantValue}" : ""));
                }
                //else if (symbolInfo.Symbol is INamespaceSymbol namespaceSymbol)
                //{
                //    foreach (INamedTypeSymbol typeSymbol in namespaceSymbol.GetTypeMembers())
                //    {
                //        Debug.Log("\t" + typeSymbol.Name);
                //    }
                //}
                else if (symbolInfo.Symbol is IParameterSymbol parameterSymbol)
                {
                    Debug.Log($"\tParameter Symbol: {parameterSymbol.IsThis}, {parameterSymbol.Type.Name}, {model2.GetTypeInfo(syntaxNode).ConvertedType}");
                }
                //else if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
                //{
                //    var typeMembers = typeSymbol.GetMembers();

                //    foreach (var typeMember in typeMembers)
                //    {
                //        Debug.Log($"{typeMember}: {typeMember.GetType()}");
                //    }
                //}
            }
            else
                Debug.Log($"{syntaxNode.Kind()}, no info\n{syntaxNode}");
        }

        //foreach (Assembly asm in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
        //{
        //    Debug.Log(asm.name);

        //    //foreach (string sourceFile in asm.sourceFiles)
        //    //    Debug.Log("\t" + sourceFile);
        //}

        
    }

    List<MetadataReference> GetMetadataReferences()
    {
        if (metadataReferences == null)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            metadataReferences = new List<MetadataReference>();

            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!assemblies[i].IsDynamic && assemblies[i].Location.Length > 0)
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
