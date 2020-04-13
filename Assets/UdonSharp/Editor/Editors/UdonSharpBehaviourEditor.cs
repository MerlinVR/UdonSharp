
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace UdonSharp
{
    [CustomEditor(typeof(UdonSharpBehaviour), true)]
    [CanEditMultipleObjects]
    public class UdonSharpBehaviourEditor : Editor
    {
        private static UdonSharpProgramAsset GetUdonSharpProgram(MonoScript programScript)
        {
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            foreach (string dataGuid in udonSharpDataAssets)
            {
                UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid));

                if (programAsset && programAsset.sourceCsScript == programScript)
                {
                    return programAsset;
                }
            }

            return null;
        }

        [MenuItem("Assets/Create/U# Script", false, 5)]
        private static void CreateUSharpScript()
        {
            string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (Selection.activeObject.GetType() != typeof(UnityEditor.DefaultAsset))
            {
                folderPath = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            }
            
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
                Undo.RegisterCompleteObjectUndo(targets.Select(e => (e as MonoBehaviour)).Distinct().ToArray(), "Convert to UdonBehaviour");

                foreach (Object targetObject in targets.Distinct())
                {
                    MonoScript behaviourScript = MonoScript.FromMonoBehaviour(targetObject as MonoBehaviour);
                    UdonSharpProgramAsset programAsset = GetUdonSharpProgram(behaviourScript);

                    if (programAsset == null)
                    {
                        string scriptPath = AssetDatabase.GetAssetPath(behaviourScript);
                        string scriptDirectory = Path.GetDirectoryName(scriptPath);
                        string scriptFileName = Path.GetFileNameWithoutExtension(scriptPath);

                        string assetPath = Path.Combine(scriptDirectory, $"{scriptFileName}.asset").Replace('\\', '/');

                        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null)
                        {
                            if (!EditorUtility.DisplayDialog("Existing file found", $"Asset file {assetPath} already exists, do you want to overwrite it?", "Ok", "Cancel"))
                                continue;
                        }

                        programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                        programAsset.sourceCsScript = behaviourScript;
                        programAsset.CompileCsProgram();

                        AssetDatabase.CreateAsset(programAsset, assetPath);
                        AssetDatabase.SaveAssets();

                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }

                    GameObject targetGameObject = (targetObject as MonoBehaviour).gameObject;

                    Undo.DestroyObjectImmediate(targetObject);

                    UdonBehaviour udonBehaviour = targetGameObject.AddComponent<UdonBehaviour>();

                    udonBehaviour.programSource = programAsset;

                    Undo.RegisterCreatedObjectUndo(udonBehaviour, "Convert to UdonBehaviour");
                }

                return;
            }

            EditorGUILayout.Space();

            base.OnInspectorGUI();
        }
    }
}
