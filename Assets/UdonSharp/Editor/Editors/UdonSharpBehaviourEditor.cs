
using System;
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace UdonSharp
{
    [CustomEditor(typeof(UdonSharpBehaviour), true)]
    [CanEditMultipleObjects]
    public class UdonSharpBehaviourEditor : Editor
    {
        [MenuItem("Assets/Create/U# Script", false, 5)]
        private static void CreateUSharpScript()
        {
            string folderPath = "Assets/";
            if (Selection.activeObject != null)
            {
                folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (Selection.activeObject.GetType() != typeof(UnityEditor.DefaultAsset))
                {
                    folderPath = Path.GetDirectoryName(folderPath);
                }
            }
            else if (Selection.assetGUIDs.Length > 0)
            {
                folderPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
            }

            folderPath = folderPath.Replace('\\', '/');
            
            string chosenFilePath = EditorUtility.SaveFilePanelInProject("Save UdonSharp File", "", "cs", "Save UdonSharp file", folderPath);

            if (chosenFilePath.Length > 0)
            {
                string chosenFileName = Path.GetFileNameWithoutExtension(chosenFilePath).Replace(" ", "").Replace("#", "Sharp");
                string assetFilePath = Path.Combine(Path.GetDirectoryName(chosenFilePath), $"{chosenFileName}.asset");

                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetFilePath) != null)
                {
                    if (!EditorUtility.DisplayDialog("File already exists", $"Corresponding asset file '{assetFilePath}' already found for new UdonSharp script. Overwrite?", "Ok", "Cancel"))
                        return;
                }

                string fileContents = UdonSharpSettings.GetProgramTemplateString(chosenFileName);

                File.WriteAllText(chosenFilePath, fileContents);

                AssetDatabase.ImportAsset(chosenFilePath, ImportAssetOptions.ForceSynchronousImport);
                MonoScript newScript = AssetDatabase.LoadAssetAtPath<MonoScript>(chosenFilePath);

                UdonSharpProgramAsset newProgramAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                newProgramAsset.sourceCsScript = newScript;

                AssetDatabase.CreateAsset(newProgramAsset, assetFilePath);

                AssetDatabase.Refresh();
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Udon Sharp Behaviours need to be converted to Udon Behaviours to work in game. Click the convert button below to automatically convert the script.", MessageType.Warning);

            if (GUILayout.Button("Convert to UdonBehaviour", GUILayout.Height(25)))
            {
                UdonSharpEditorUtility.ConvertToUdonBehavioursInternal(Array.ConvertAll(targets, e => e as UdonSharpBehaviour), true, true);

                return;
            }

            EditorGUILayout.Space();

            base.OnInspectorGUI();
        }
    }
}
