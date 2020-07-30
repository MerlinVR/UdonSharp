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
        }

        private static void EditorSceneManager_sceneOpened(Scene scene, OpenSceneMode mode)
        {
            UpdatePublicVariables(GetAllUdonBehaviours(scene));
        }

        public static void RunPostBuildSceneFixup()
        {
            UpdatePublicVariables(GetAllUdonBehaviours());
            UdonEditorManager.Instance.RefreshQueuedProgramSources();
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
                        FieldDefinition fieldDefinition;
                        if (!fieldDefinitions.TryGetValue(variableSymbol, out fieldDefinition))
                        {
                            updatedBehaviourVariables++;
                            publicVariables.RemoveVariable(variableSymbol);
                            continue;
                        }

                        System.Type publicFieldType;
                        if (!publicVariables.TryGetVariableType(variableSymbol, out publicFieldType))
                            continue;

                        System.Type programSymbolType = fieldDefinition.fieldSymbol.symbolCsType;
                        if (!publicFieldType.IsAssignableFrom(programSymbolType))
                        {
                            updatedBehaviourVariables++;

                            if (publicFieldType.IsExplicitlyAssignableFrom(programSymbolType))
                            {
                                object symbolValue;
                                publicVariables.TryGetVariableValue(variableSymbol, out symbolValue);

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
