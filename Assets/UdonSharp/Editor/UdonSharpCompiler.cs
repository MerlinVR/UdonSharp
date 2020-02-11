using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    public class UdonSharpCompiler
    {
        private MonoScript source;
        private CompilationModule module;

        private static int initAssemblyCounter = 0;

        public UdonSharpCompiler(MonoScript sourceScript)
        {
            source = sourceScript ?? throw new System.ArgumentException("No valid C# source file specified!");
            module = new CompilationModule(source);
        }

        public string Compile()
        {
            return module.Compile();
        }

        public void AssignHeapConstants(IUdonProgram program)
        {
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

                RunFieldInitalizers(program);
            }
        }

        private void RunFieldInitalizers(IUdonProgram program)
        {
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
                foreach (var variable in fieldDeclarationSyntax.Declaration.Variables)
                {
                    if (variable.Initializer != null)
                    {
                        var name = $"temp_{count}_{variable.Identifier}";
                        method.Statements.Add(new CodeSnippetStatement($"{type} {name} {variable.Initializer};"));
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

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
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