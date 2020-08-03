using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace UdonSharpEditor
{
    [InitializeOnLoad]
    public class UdonSharpEditorManager
    {
        static UdonSharpEditorManager()
        {
            EditorSceneManager.sceneOpened += EditorSceneManager_sceneOpened;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += PlayModeErrorCheck;
        }

        private static void EditorSceneManager_sceneOpened(Scene scene, OpenSceneMode mode)
        {
            List<UdonBehaviour> allBehaviours = GetAllUdonBehaviours();
            UpdatePublicVariables(allBehaviours);
            UpdateSerializedProgramAssets(allBehaviours);
        }

        internal static void RunPostBuildSceneFixup()
        {
            List<UdonBehaviour> allBehaviours = GetAllUdonBehaviours();
            UpdateSerializedProgramAssets(allBehaviours);
            UpdatePublicVariables(allBehaviours);
            UdonEditorManager.Instance.RefreshQueuedProgramSources();
        }

        static void PlayModeErrorCheck(PlayModeStateChange state)
        {
            // Prevent people from entering play mode when there are compile errors, like normal Unity C#
            // READ ME
            // --------
            // If you think you know better and are about to edit this out, be aware that you gain nothing by doing so. 
            // If a script hits a compile error, it will not update until the compile errors are resolved.
            // You will just be left wondering "why aren't my scripts changing when I edit them?" since the old copy of the script will be used until the compile errors are resolved.
            // --------
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                {
                    EditorApplication.isPlaying = false;

                    UdonSharpUtils.ShowEditorNotification("All U# compile errors have to be fixed before you can enter playmode!");
                }
            }
        }

        static bool _requiresCompile = false;
        internal static void QueueScriptCompile()
        {
            _requiresCompile = true;
        }

        private static void OnEditorUpdate()
        {
            if (_requiresCompile)
            {
                UdonSharpProgramAsset.CompileAllCsPrograms();
                _requiresCompile = false;
            }
        }

        static List<UdonBehaviour> GetAllUdonBehaviours()
        {
            int sceneCount = EditorSceneManager.loadedSceneCount;

            int maxGameObjectCount = 0;

            for (int i = 0; i < sceneCount; ++i) maxGameObjectCount = Mathf.Max(maxGameObjectCount, EditorSceneManager.GetSceneAt(i).rootCount);

            List<GameObject> rootObjects = new List<GameObject>(maxGameObjectCount);
            List<UdonBehaviour> behaviourList = new List<UdonBehaviour>();

            for (int i = 0; i < sceneCount; ++i)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);
                int rootCount = scene.rootCount;

                scene.GetRootGameObjects(rootObjects);

                for (int j = 0; j < rootCount; ++j)
                {
                    behaviourList.AddRange(rootObjects[j].GetComponentsInChildren<UdonBehaviour>());
                }
            }

            return behaviourList;
        }

        static List<UdonBehaviour> GetAllUdonBehaviours(Scene scene)
        {
            int rootCount = scene.rootCount;
            GameObject[] rootObjects = scene.GetRootGameObjects();

            List<UdonBehaviour> behaviourList = new List<UdonBehaviour>();

            for (int j = 0; j < rootCount; ++j)
            {
                behaviourList.AddRange(rootObjects[j].GetComponentsInChildren<UdonBehaviour>());
            }

            return behaviourList;
        }

        static FieldInfo _serializedAssetField;
        static void UpdateSerializedProgramAssets(List<UdonBehaviour> udonBehaviours)
        {
            if (_serializedAssetField == null)
                _serializedAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
                UdonSharpProgramAsset programAsset = behaviour.programSource as UdonSharpProgramAsset;
                if (programAsset == null)
                    continue;
                
                if (_serializedAssetField.GetValue(behaviour) == null)
                {
                    SerializedObject serializedBehaviour = new SerializedObject(behaviour);
                    SerializedProperty serializedProgramProperty = serializedBehaviour.FindProperty("serializedProgramAsset");
                    serializedProgramProperty.objectReferenceValue = programAsset.SerializedProgramAsset;
                    serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// Updates the public variable types on behavours.
        /// If public variable type does not match from a prior version of the script on the behaviour, 
        ///   this will attempt to convert the type using System.Convert, then if that fails, by using an explicit/implicit cast if found.
        /// If no conversion works, this will set the public variable to the default value for the type.
        /// </summary>
        /// <param name="udonBehaviours"></param>
        static void UpdatePublicVariables(List<UdonBehaviour> udonBehaviours)
        {
            int updatedBehaviourVariables = 0;

            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
                if (behaviour.programSource == null || !(behaviour.programSource is UdonSharpProgramAsset programAsset))
                    continue;

                IUdonVariableTable publicVariables = behaviour.publicVariables;

                Dictionary<string, FieldDefinition> fieldDefinitions = programAsset.fieldDefinitions;

                IReadOnlyCollection<string> behaviourVariables = publicVariables.VariableSymbols.ToArray();

                foreach (string variableSymbol in behaviourVariables)
                {
                    try
                    {
                        // Remove variables that have been removed from the program asset
                        if (!fieldDefinitions.TryGetValue(variableSymbol, out FieldDefinition fieldDefinition))
                        {
                            updatedBehaviourVariables++;
                            publicVariables.RemoveVariable(variableSymbol);
                            continue;
                        }
                        
                        if (!publicVariables.TryGetVariableType(variableSymbol, out System.Type publicFieldType))
                            continue;

                        System.Type programSymbolType = fieldDefinition.fieldSymbol.symbolCsType;
                        if (!publicFieldType.IsAssignableFrom(programSymbolType))
                        {
                            updatedBehaviourVariables++;

                            if (publicFieldType.IsExplicitlyAssignableFrom(programSymbolType))
                            {
                                publicVariables.TryGetVariableValue(variableSymbol, out object symbolValue);

                                object convertedValue;
                                try
                                {
                                    convertedValue = Convert.ChangeType(symbolValue, programSymbolType);
                                }
                                catch (InvalidCastException)
                                {
                                    MethodInfo castMethod = publicFieldType.GetCastMethod(programSymbolType);

                                    if (castMethod != null)
                                        convertedValue = castMethod.Invoke(null, new object[] { symbolValue });
                                    else
                                        convertedValue = programAsset.GetPublicVariableDefaultValue(variableSymbol);
                                }

                                publicVariables.RemoveVariable(variableSymbol);
                                IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, convertedValue });
                                publicVariables.TryAddVariable(newVariable);
                            }
                            else
                            {
                                publicVariables.RemoveVariable(variableSymbol);
                                object defaultValue = programAsset.GetPublicVariableDefaultValue(variableSymbol);
                                IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, defaultValue });
                                publicVariables.TryAddVariable(newVariable);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to update public variable {variableSymbol} on behaviour {behaviour}, exception {e}\n\nPlease report this error to Merlin!");
                    }
                }
            }

            if (updatedBehaviourVariables > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
            }
        }
    }
}
