
using System.IO;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UdonSharpEditor
{
    public class UdonSharpAssemblyDefinition : ScriptableObject
    {
        public AssemblyDefinitionAsset sourceAssembly;
        
        [MenuItem("Assets/Create/U# Assembly Definition", false, 98)]
        private static void CreateAssemblyDefinition()
        {
            UdonSharpAssemblyDefinition newAssemblyDefinition = CreateInstance<UdonSharpAssemblyDefinition>();
            
            string folderPath = "Assets/";
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

                folderPath = assetPath;
                
                if (Selection.activeObject.GetType() != typeof(UnityEditor.DefaultAsset))
                {
                    folderPath = Path.GetDirectoryName(folderPath);
                }

                if (Selection.activeObject is AssemblyDefinitionAsset asmDef)
                {
                    newAssemblyDefinition.sourceAssembly = asmDef;
                    folderPath = Path.Combine(folderPath, $"{Path.GetFileNameWithoutExtension(assetPath)}.asset");
                }
                else
                {
                    folderPath = Path.Combine(folderPath, "AsmDef.asset");
                }
            }
            else if (Selection.assetGUIDs.Length > 0)
            {
                folderPath = Path.Combine(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]), "AsmDef.asset");
            }
            
            folderPath = folderPath.Replace('\\', '/');
            
            ProjectWindowUtil.CreateAsset(newAssemblyDefinition, folderPath);
        }
    }

    [CustomEditor(typeof(UdonSharpAssemblyDefinition))]
    internal class UdonSharpAssemblyDefinitionEditor : Editor
    {
        private SerializedProperty _assetProp;
        
        private void OnEnable()
        {
            _assetProp = serializedObject.FindProperty("sourceAssembly");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.ObjectField(_assetProp);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                CompilationContext.ResetAssemblyCaches();
            }
        }
    }
}
