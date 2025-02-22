
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharp.Compiler.Symbols;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UdonSharpEditor
{
    using SyntaxTree = Microsoft.CodeAnalysis.SyntaxTree;
    
    [InitializeOnLoad]
    internal class UdonSharpUpgrader
    {
        static UdonSharpUpgrader()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (_needsProgramUpgradePass && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                UpgradeScripts();
            }
        }

        [MenuItem("VRChat SDK/Udon Sharp/Force Upgrade")]
        internal static void ForceUpgrade()
        {
            UdonSharpProgramAsset.GetAllUdonSharpPrograms().ForEach(QueueUpgrade);
            UdonSharpEditorCache.Instance.QueueUpgradePass();
            UdonSharpEditorManager._didSceneUpgrade = false;
        }

        private static bool _needsProgramUpgradePass;
        
        public static void QueueUpgrade(UdonSharpProgramAsset programAsset)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            
            if (programAsset == null ||
                programAsset.sourceCsScript == null)
                return;

            if (programAsset.ScriptVersion >= UdonSharpProgramVersion.CurrentVersion)
                return;

            _needsProgramUpgradePass = true;
        }

        /// <summary>
        /// Runs upgrade process on all U# scripts
        /// </summary>
        /// <returns>True if some scripts have been updated</returns>
        internal static bool UpgradeScripts()
        {
            bool upgraded = UpgradeScripts(UdonSharpProgramAsset.GetAllUdonSharpPrograms());

            _needsProgramUpgradePass = false;

            return upgraded;
        }

        private static int _assemblyCounter;

        private static bool UpgradeScripts(UdonSharpProgramAsset[] programAssets)
        {
            if (programAssets.Length == 0)
                return false;

            if (programAssets.All(e => e.ScriptVersion >= UdonSharpProgramVersion.CurrentVersion))
                return false;
            
            CompilationContext compilationContext = new CompilationContext(new UdonSharpCompileOptions());

            ModuleBinding[] bindings = compilationContext.LoadSyntaxTreesAndCreateModules(CompilationContext.GetAllFilteredSourcePaths(false), UdonSharpUtils.GetProjectDefines(false));
            
            CSharpCompilation compilation = CSharpCompilation.Create(
                $"UdonSharpRoslynUpgradeAssembly{_assemblyCounter++}",
                bindings.Select(e => e.tree),
                CompilationContext.GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            bool scriptUpgraded = false;
            bool versionUpgraded = false;
            
            foreach (var programAsset in programAssets)
            {
                string assetPath = AssetDatabase.GetAssetPath(programAsset.sourceCsScript);
                ModuleBinding binding = bindings.FirstOrDefault(e => e.filePath == assetPath);
                
                if (binding == null)
                    continue;

                if (programAsset.ScriptVersion < UdonSharpProgramVersion.V1SerializationUpdate)
                {
                    SyntaxTree bindingTree = binding.tree;
                    SemanticModel bindingModel = compilation.GetSemanticModel(bindingTree);

                    SerializationUpdateSyntaxRewriter rewriter = new SerializationUpdateSyntaxRewriter(bindingModel);

                    SyntaxNode newRoot = rewriter.Visit(bindingTree.GetRoot());

                    if (rewriter.Modified)
                    {
                        try
                        {
                            File.WriteAllText(binding.filePath, newRoot.ToFullString(), Encoding.UTF8);
                            scriptUpgraded = true;
                            
                            UdonSharpUtils.Log($"Upgraded field serialization attributes on U# script '{binding.filePath}'", programAsset.sourceCsScript);
                        }
                        catch (Exception e)
                        {
                            UdonSharpUtils.LogError($"Could not upgrade U# script, exception: {e}");
                        }
                    }
                    // We expect this to come through a second time after scripts have been updated and change the version on the asset. 
                    else
                    {
                        programAsset.ScriptVersion = UdonSharpProgramVersion.V1SerializationUpdate;
                        EditorUtility.SetDirty(programAsset);
                        versionUpgraded = true;
                    }
                }
            }

            if (scriptUpgraded)
            {
                AssetDatabase.Refresh();
                return true;
            }

            if (versionUpgraded)
            {
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions());
            }

            return false;
        }

        private class SerializationUpdateSyntaxRewriter : CSharpSyntaxRewriter
        {
            public bool Modified { get; private set; }
            
            private readonly SemanticModel model;
            
            public SerializationUpdateSyntaxRewriter(SemanticModel model)
            {
                this.model = model;
            }

            private static bool IsFieldSerializedWithoutOdin(IFieldSymbol fieldSymbol)
            {
                if (fieldSymbol.IsConst) return false;
                if (fieldSymbol.IsStatic) return false;
                if (fieldSymbol.IsReadOnly) return false;
                
                var fieldAttributes = fieldSymbol.GetAttributes();

                bool HasAttribute<T>()
                {
                    string fullTypeName = typeof(T).FullName;
                    
                    foreach (var fieldAttribute in fieldAttributes)
                    {
                        if (TypeSymbol.GetFullTypeName(fieldAttribute.AttributeClass) == fullTypeName)
                            return true;
                    }

                    return false;
                }
                
                if (HasAttribute<NonSerializedAttribute>() && !HasAttribute<OdinSerializeAttribute>()) return false;
                
                return (fieldSymbol.DeclaredAccessibility == Accessibility.Public || HasAttribute<SerializeField>()) && !HasAttribute<OdinSerializeAttribute>();
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                FieldDeclarationSyntax fieldDeclaration = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node);
                
                var typeInfo = model.GetTypeInfo(node.Declaration.Type);
                if (typeInfo.Type == null)
                {
                    UdonSharpUtils.LogWarning($"Could not find symbol for {node}");
                    return fieldDeclaration;
                }

                ITypeSymbol rootType = typeInfo.Type;

                while (rootType.TypeKind == TypeKind.Array)
                    rootType = ((IArrayTypeSymbol)rootType).ElementType;

                if (rootType.TypeKind == TypeKind.Error ||
                    rootType.TypeKind == TypeKind.Unknown)
                {
                    UdonSharpUtils.LogWarning($"Type {typeInfo.Type} for field '{fieldDeclaration.Declaration}' is invalid");
                    return fieldDeclaration;
                }
                
                IFieldSymbol firstFieldSymbol = (IFieldSymbol)model.GetDeclaredSymbol(node.Declaration.Variables.First());
                rootType = firstFieldSymbol.Type;

                // If the field is not serialized or is using Odin already, we don't need to do anything.
                if (!IsFieldSerializedWithoutOdin(firstFieldSymbol))
                    return fieldDeclaration;

                // Getting the type may fail if it's a user type that hasn't compiled on the C# side yet. For now we skip it, but we should do a simplified check for jagged arrays
                if (!TypeSymbol.TryGetSystemType(rootType, out Type systemType))
                    return fieldDeclaration;
                
                // If Unity can serialize the type, we're good
                if (UnitySerializationUtility.GuessIfUnityWillSerialize(systemType))
                    return fieldDeclaration;

                // Common type that gets picked up as serialized but shouldn't be
                // todo: Add actual checking for if a type is serializable, which isn't consistent. Unity/System library types in large part are serializable but don't have the System.Serializable tag, but types outside those assemblies need the tag to be serialized.
                if (systemType == typeof(VRCPlayerApi) || systemType == typeof(VRCPlayerApi[]))
                    return fieldDeclaration;

                Modified = true;
                
                NameSyntax odinSerializeName = IdentifierName("VRC");
                odinSerializeName = QualifiedName(odinSerializeName, IdentifierName("Udon"));
                odinSerializeName = QualifiedName(odinSerializeName, IdentifierName("Serialization"));
                odinSerializeName = QualifiedName(odinSerializeName, IdentifierName("OdinSerializer"));
                odinSerializeName = QualifiedName(odinSerializeName, IdentifierName("OdinSerialize"));

                // Somehow it seems like there's literally no decent way to maintain the indent on inserted code so we'll just inline the comment because Roslyn is dumb
                SyntaxTrivia commentTrivia = Comment(" /* UdonSharp auto-upgrade: serialization */ ");
                AttributeListSyntax newAttribList = AttributeList(SeparatedList(new [] { Attribute(odinSerializeName)})).WithTrailingTrivia(commentTrivia);

                SyntaxList<AttributeListSyntax> attributeList = fieldDeclaration.AttributeLists;
                
                if (attributeList.Count > 0)
                {
                    SyntaxTriviaList trailingTrivia = attributeList.Last().GetTrailingTrivia();
                    trailingTrivia = trailingTrivia.Insert(0, commentTrivia);
                    attributeList.Replace(attributeList[attributeList.Count - 1], attributeList[attributeList.Count -1].WithoutTrailingTrivia());

                    newAttribList = newAttribList.WithTrailingTrivia(trailingTrivia);
                }
                else
                {
                    newAttribList = newAttribList.WithLeadingTrivia(fieldDeclaration.GetLeadingTrivia());
                    fieldDeclaration = fieldDeclaration.WithoutLeadingTrivia();
                }
                
                attributeList = attributeList.Add(newAttribList);

                return fieldDeclaration.WithAttributeLists(attributeList);
            }
        }
    }
}
