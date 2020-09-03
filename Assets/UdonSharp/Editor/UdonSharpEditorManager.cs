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
    internal class UdonSharpEditorManager
    {
        static UdonSharpEditorManager()
        {
            EditorSceneManager.sceneOpened += EditorSceneManager_sceneOpened;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnChangePlayMode;
            AssemblyReloadEvents.afterAssemblyReload += RunPostAssemblyBuildRefresh;
        }

        private static void EditorSceneManager_sceneOpened(Scene scene, OpenSceneMode mode)
        {
            List<UdonBehaviour> udonBehaviours = GetAllUdonBehaviours();

            RunAllUpdates(udonBehaviours);
        }

        internal static void RunPostBuildSceneFixup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RunAllUpdates();

            UdonEditorManager.Instance.RefreshQueuedProgramSources();
        }

        static void RunPostAssemblyBuildRefresh()
        {
            UdonSharpProgramAsset.CompileAllCsPrograms();
        }

        static void OnChangePlayMode(PlayModeStateChange state)
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
                else if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    CreateProxyBehaviours(GetAllUdonBehaviours());
                }
            }

            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
            {
                UdonSharpEditorCache.ResetInstance();
                if (UdonSharpEditorCache.Instance.LastBuildType == UdonSharpEditorCache.DebugInfoType.Client)
                {
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                }

                RunAllUpdates();
            }
        }

        static void RunAllUpdates(List<UdonBehaviour> allBehaviours = null)
        {
            if (allBehaviours == null)
                allBehaviours = GetAllUdonBehaviours();

            UpdateSerializedProgramAssets(allBehaviours);
            UpdatePublicVariables(allBehaviours);
#if UDON_BETA_SDK
            UpdateSyncModes(allBehaviours);
#endif
            CreateProxyBehaviours(allBehaviours);
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
                    behaviourList.AddRange(rootObjects[j].GetComponentsInChildren<UdonBehaviour>(true));
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

#if UDON_BETA_SDK
        static void UpdateSyncModes(List<UdonBehaviour> udonBehaviours)
        {
            int modificationCount = 0;

            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
                if (behaviour.programSource == null || !(behaviour.programSource is UdonSharpProgramAsset programAsset))
                    continue;

                if (behaviour.Reliable == true &&
                    programAsset.behaviourSyncMode == BehaviourSyncMode.Continuous)
                {
                    behaviour.Reliable = false;
                    modificationCount++;
                }
                else if (behaviour.Reliable == false &&
                         programAsset.behaviourSyncMode == BehaviourSyncMode.Manual)
                {
                    behaviour.Reliable = true;
                    modificationCount++;
                }
            }

            if (modificationCount > 0)
                EditorSceneManager.MarkAllScenesDirty();
        }
#endif

        static bool UdonSharpBehaviourTypeMatches(object symbolValue, System.Type expectedType, string behaviourName, string variableName)
        {
            if (symbolValue == null)
                return true;

            // A reference to an actual UdonSharpBehaviour has been put in the UdonBehaviour, UdonSharpBehaviours are not serializable into VRC so this will cause issues
            if (symbolValue is UdonSharpBehaviour)
            {
                Debug.LogWarning($"Clearing reference to an UdonSharpBehaviour's proxy '{symbolValue}' from variable '{variableName}' on behaviour '{behaviourName}' You must only reference backer UdonBehaviours, not their proxies.");
                return false;
            }

            if (!(expectedType == typeof(UdonBehaviour) ||
                  expectedType == typeof(UdonSharpBehaviour) ||
                  expectedType.IsSubclassOf(typeof(UdonSharpBehaviour))))
                return true;

            if (symbolValue.GetType() != typeof(UdonBehaviour))
                return false;
            
            UdonBehaviour otherBehaviour = (UdonBehaviour)symbolValue;

            AbstractUdonProgramSource behaviourProgramAsset = otherBehaviour.programSource;
            
            if (behaviourProgramAsset == null)
                return true;
            
            if (behaviourProgramAsset is UdonSharpProgramAsset behaviourUSharpAsset && 
                expectedType != typeof(UdonBehaviour)) // Leave references to UdonBehaviours intact to prevent breaks on old behaviours, this may be removed in 1.0 to enforce the correct division in types in C# land
            {
                System.Type symbolUSharpType = behaviourUSharpAsset.sourceCsScript?.GetClass();

                if (symbolUSharpType != null &&
                    symbolUSharpType != expectedType &&
                    !symbolUSharpType.IsSubclassOf(expectedType))
                {
                    return false;
                }
            }
            else if (expectedType != typeof(UdonSharpBehaviour) &&
                     expectedType != typeof(UdonBehaviour))
            {
                // Don't allow graph assets and such to exist in references to specific U# types
                return false;
            }

            if (expectedType == typeof(UdonSharpBehaviour) && !(behaviourProgramAsset is UdonSharpProgramAsset))
            {
                // Don't allow graph asset references in non specific U# types either
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles arrays and jagged arrays, validates jagged arrays have the valid array types and verifies that UdonSharpBehaviour references in arrays/jagged arrays are valid
        /// </summary>
        /// <param name="rootArray"></param>
        /// <param name="rootArrayType"></param>
        /// <param name="jaggedArrayDimensionCount"></param>
        /// <param name="currentDepth"></param>
        /// <returns></returns>
        static bool VerifyArrayValidity(object rootArray, System.Type rootArrayType, int arrayDimensionCount, int currentDepth, string behaviourName, string variableName)
        {
            if (rootArray == null)
                return true;

            if (arrayDimensionCount == currentDepth)
            {
                System.Type elementType = rootArrayType.GetElementType();
                
                if (rootArrayType == typeof(UdonBehaviour[]) ||
                    rootArrayType == typeof(UdonSharpBehaviour[]) ||
                    elementType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                {
                    if (rootArray.GetType() != typeof(Component[]) &&
                        rootArray.GetType() != typeof(UdonBehaviour[]))
                        return false;

                    Array array = (Array)rootArray;
                    for (int i = 0; i < array.Length; ++i)
                    {
                        object arrayVal = array.GetValue(i);
                        if (arrayVal != null && !(arrayVal is UdonBehaviour))
                        {
                            array.SetValue(null, i);
                            continue;
                        }

                        UdonBehaviour behaviour = (UdonBehaviour)arrayVal;

                        if (!UdonSharpBehaviourTypeMatches(behaviour, elementType, behaviourName, variableName))
                            array.SetValue(null, i);
                    }
                }
                else if (rootArray.GetType() != rootArrayType)
                    return false;
            }
            else
            {
                Array array = rootArray as Array;
                if (array == null)
                    return false;

                foreach (object element in array)
                {
                    if (!VerifyArrayValidity(element, rootArrayType, arrayDimensionCount, currentDepth + 1, behaviourName, variableName))
                        return false;
                }
            }

            return true;
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

                        // Field was exported at one point, but is no longer. So we need to remove it from the behaviour
                        if (!fieldDefinition.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public))
                        {
                            updatedBehaviourVariables++;
                            publicVariables.RemoveVariable(variableSymbol);
                            continue;
                        }
                        
                        if (!publicVariables.TryGetVariableType(variableSymbol, out System.Type publicFieldType))
                            continue;

                        publicVariables.TryGetVariableValue(variableSymbol, out object symbolValue);

                        System.Type programSymbolType = fieldDefinition.fieldSymbol.symbolCsType;
                        if (!publicFieldType.IsAssignableFrom(programSymbolType))
                        {
                            updatedBehaviourVariables++;

                            if (publicFieldType.IsExplicitlyAssignableFrom(programSymbolType))
                            {
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

                        string behaviourName = behaviour.ToString();

                        // Clean up UdonSharpBehaviour types that are no longer compatible
                        System.Type userType = fieldDefinition.fieldSymbol.userCsType;
                        if (!UdonSharpBehaviourTypeMatches(symbolValue, userType, behaviourName, variableSymbol))
                        {
                            updatedBehaviourVariables++;
                            publicVariables.RemoveVariable(variableSymbol);
                            continue;
                        }

                        if (userType.IsArray)
                        {
                            int arrayDepth = 0;
                            System.Type currentType = userType;

                            while (currentType.IsArray)
                            {
                                arrayDepth++;
                                currentType = currentType.GetElementType();
                            }

                            if (!VerifyArrayValidity(symbolValue, currentType.MakeArrayType(), arrayDepth, 1, behaviourName, variableSymbol))
                            {
                                publicVariables.RemoveVariable(variableSymbol);
                                object defaultValue = programAsset.GetPublicVariableDefaultValue(variableSymbol);
                                IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, defaultValue });
                                publicVariables.TryAddVariable(newVariable);
                                updatedBehaviourVariables++;
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

        /// <summary>
        /// Creates proxy behaviours for all behaviours in the scene
        /// </summary>
        /// <param name="allBehaviours"></param>
        static void CreateProxyBehaviours(List<UdonBehaviour> allBehaviours)
        {
            foreach (UdonBehaviour udonBehaviour in allBehaviours)
            {
                if (udonBehaviour.programSource != null && udonBehaviour.programSource is UdonSharpProgramAsset)
                    UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour, ProxySerializationPolicy.NoSerialization);
            }
        }
    }
}
