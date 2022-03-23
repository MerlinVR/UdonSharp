
using System;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharp.Serialization;
using UdonSharp.Updater;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Object = UnityEngine.Object;

namespace UdonSharpEditor
{
    /// <summary>
    /// Stored on the backing UdonBehaviour
    /// </summary>
    internal enum UdonSharpBehaviourVersion
    {
        V0,
        V0DataUpgradeNeeded,
        V1,
        NextVer,
        CurrentVersion = NextVer - 1,
    }
    
    /// <summary>
    /// Various utility functions for interacting with U# behaviours and proxies for editor scripting.
    /// </summary>
    public static class UdonSharpEditorUtility
    {
        /// <summary>
        /// Deletes an UdonSharp program asset and the serialized program asset associated with it
        /// </summary>
        [PublicAPI]
        public static void DeleteProgramAsset(UdonSharpProgramAsset programAsset)
        {
            if (programAsset == null)
                return;

            AbstractSerializedUdonProgramAsset serializedAsset = programAsset.GetSerializedUdonProgramAsset();

            if (serializedAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(serializedAsset);
                serializedAsset = AssetDatabase.LoadAssetAtPath<AbstractSerializedUdonProgramAsset>(assetPath);

                if (serializedAsset != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            string programAssetPath = AssetDatabase.GetAssetPath(programAsset);

            programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programAssetPath);

            if (programAsset != null)
                AssetDatabase.DeleteAsset(programAssetPath);
        }

        /// <summary>
        /// Converts a set of UdonSharpBehaviour components to their equivalent UdonBehaviour components
        /// </summary>
        [Obsolete("ConvertToUdonBehaviours is no longer supported, if you want to add a new U# component use UdonSharpEditorUtility.AddComponent", true)]
        public static UdonBehaviour[] ConvertToUdonBehaviours(UdonSharpBehaviour[] components, bool convertChildren = false)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts a set of UdonSharpBehaviour components to their equivalent UdonBehaviour components
        /// Registers an Undo operation for the conversion
        /// </summary>
        /// <returns></returns>
        [Obsolete("ConvertToUdonBehavioursWithUndo is no longer supported, if you want to add a new U# component use UdonSharpUndo.AddComponent", true)]
        public static UdonBehaviour[] ConvertToUdonBehavioursWithUndo(UdonSharpBehaviour[] components, bool convertChildren = false)
        {
            throw new NotImplementedException();
        }

        internal static Dictionary<MonoScript, UdonSharpProgramAsset> _programAssetLookup;
        internal static Dictionary<Type, UdonSharpProgramAsset> _programAssetTypeLookup;
        
        private static void InitTypeLookups()
        {
            if (_programAssetLookup != null) 
                return;
            
            _programAssetLookup = new Dictionary<MonoScript, UdonSharpProgramAsset>();
            _programAssetTypeLookup = new Dictionary<Type, UdonSharpProgramAsset>();

            UdonSharpProgramAsset[] udonSharpProgramAssets = UdonSharpProgramAsset.GetAllUdonSharpPrograms();

            foreach (UdonSharpProgramAsset programAsset in udonSharpProgramAssets)
            {
                if (programAsset && programAsset.sourceCsScript != null && !_programAssetLookup.ContainsKey(programAsset.sourceCsScript))
                {
                    _programAssetLookup.Add(programAsset.sourceCsScript, programAsset);
                    if (programAsset.GetClass() != null)
                        _programAssetTypeLookup.Add(programAsset.GetClass(), programAsset);
                }
            }
        }

        private static UdonSharpProgramAsset GetUdonSharpProgramAsset(MonoScript programScript)
        {
            InitTypeLookups();

            _programAssetLookup.TryGetValue(programScript, out var foundProgramAsset);

            return foundProgramAsset;
        }

        /// <summary>
        /// Gets the UdonSharpProgramAsset that represents the program for the given UdonSharpBehaviour
        /// </summary>
        /// <param name="udonSharpBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpProgramAsset GetUdonSharpProgramAsset(UdonSharpBehaviour udonSharpBehaviour)
        {
            return GetUdonSharpProgramAsset(MonoScript.FromMonoBehaviour(udonSharpBehaviour));
        }

        [PublicAPI]
        public static UdonSharpProgramAsset GetUdonSharpProgramAsset(Type type)
        {
            InitTypeLookups();

            _programAssetTypeLookup.TryGetValue(type, out UdonSharpProgramAsset foundProgramAsset);

            return foundProgramAsset;
        }

        [PublicAPI]
        public static UdonSharpProgramAsset GetUdonSharpProgramAsset(UdonBehaviour udonBehaviour)
        {
            if (!IsUdonSharpBehaviour(udonBehaviour))
                return null;

            return (UdonSharpProgramAsset)udonBehaviour.programSource;
        }

        internal const string BackingFieldName = "_udonSharpBackingUdonBehaviour";

        private static readonly FieldInfo _backingBehaviourField = typeof(UdonSharpBehaviour).GetField(BackingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Gets the backing UdonBehaviour for a proxy
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonBehaviour GetBackingUdonBehaviour(UdonSharpBehaviour behaviour)
        {
            return (UdonBehaviour)_backingBehaviourField.GetValue(behaviour);
        }

        internal static void SetBackingUdonBehaviour(UdonSharpBehaviour behaviour, UdonBehaviour backingBehaviour)
        {
            _backingBehaviourField.SetValue(behaviour, backingBehaviour);
        }

        private const string UDONSHARP_BEHAVIOUR_VERSION_KEY = "___UdonSharpBehaviourVersion___";
        private const string UDONSHARP_BEHAVIOUR_UPGRADE_MARKER = "___UdonSharpBehaviourPersistDataFromUpgrade___";
        private const string UDONSHARP_SCENE_BEHAVIOUR_UPGRADE_MARKER = "___UdonSharpBehaviourHasDoneSceneUpgrade___";

        private static bool ShouldPersistVariable(string variableSymbol)
        {
            return variableSymbol == UDONSHARP_BEHAVIOUR_VERSION_KEY ||
                   variableSymbol == UDONSHARP_BEHAVIOUR_UPGRADE_MARKER ||
                   variableSymbol == UDONSHARP_SCENE_BEHAVIOUR_UPGRADE_MARKER;
        }

        internal static UdonSharpBehaviourVersion GetBehaviourVersion(UdonBehaviour behaviour)
        {
            if (behaviour.publicVariables.TryGetVariableValue<int>(UDONSHARP_BEHAVIOUR_VERSION_KEY, out int val))
                return (UdonSharpBehaviourVersion)val;
            
            return UdonSharpBehaviourVersion.V0;
        }

        internal static void SetBehaviourVersion(UdonBehaviour behaviour, UdonSharpBehaviourVersion version)
        {
            UdonSharpBehaviourVersion lastVer = GetBehaviourVersion(behaviour);

            if (lastVer == version && lastVer != UdonSharpBehaviourVersion.V0)
                return;
            
            bool setVer = behaviour.publicVariables.TrySetVariableValue<int>(UDONSHARP_BEHAVIOUR_VERSION_KEY, (int)version);

            if (!setVer)
            {
                behaviour.publicVariables.RemoveVariable(UDONSHARP_BEHAVIOUR_VERSION_KEY);
                IUdonVariable newVar = new UdonVariable<int>(UDONSHARP_BEHAVIOUR_VERSION_KEY, (int)version);
                setVer = behaviour.publicVariables.TryAddVariable(newVar);
            }

            if (setVer)
            {
                UdonSharpUtils.SetDirty(behaviour);
                return;
            }
            
            UdonSharpUtils.LogError("Could not set version variable");
        }

        private static bool BehaviourRequiresBackwardsCompatibilityPersistence(UdonBehaviour behaviour)
        {
            if (behaviour.publicVariables.TryGetVariableValue<bool>(UDONSHARP_BEHAVIOUR_UPGRADE_MARKER, out bool needsBackwardsCompat) && PrefabUtility.IsPartOfPrefabAsset(behaviour))
                return needsBackwardsCompat;

            return false;
        }

        internal static void ClearBehaviourVariables(UdonBehaviour behaviour, bool clearPersistentVariables = false)
        {
            foreach (string publicVarSymbol in behaviour.publicVariables.VariableSymbols.ToArray()) // ToArray so we don't modify the collection while iterating it
            {
                if (!clearPersistentVariables && ShouldPersistVariable(publicVarSymbol))
                    continue;
                
                behaviour.publicVariables.RemoveVariable(publicVarSymbol);
            }
        }

        private static void SetBehaviourUpgraded(UdonBehaviour behaviour)
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(behaviour))
                return;

            if (!behaviour.publicVariables.TrySetVariableValue<bool>(UDONSHARP_BEHAVIOUR_UPGRADE_MARKER, true))
            {
                behaviour.publicVariables.RemoveVariable(UDONSHARP_BEHAVIOUR_UPGRADE_MARKER);
                
                IUdonVariable newVar = new UdonVariable<bool>(UDONSHARP_BEHAVIOUR_UPGRADE_MARKER, true);
                behaviour.publicVariables.TryAddVariable(newVar);
            }
            
            UdonSharpUtils.SetDirty(behaviour);
        }

        internal static void SetSceneBehaviourUpgraded(UdonBehaviour behaviour)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(behaviour) && !PrefabUtility.IsPartOfPrefabAsset(behaviour))
                return;
            
            if (!behaviour.publicVariables.TrySetVariableValue<bool>(UDONSHARP_SCENE_BEHAVIOUR_UPGRADE_MARKER, true))
            {
                behaviour.publicVariables.RemoveVariable(UDONSHARP_SCENE_BEHAVIOUR_UPGRADE_MARKER);
                
                IUdonVariable newVar = new UdonVariable<bool>(UDONSHARP_SCENE_BEHAVIOUR_UPGRADE_MARKER, true);
                behaviour.publicVariables.TryAddVariable(newVar);
            }
            
            UdonSharpUtils.SetDirty(behaviour);
        }

        private static bool HasSceneBehaviourUpgradeFlag(UdonBehaviour behaviour)
        {
            return behaviour.publicVariables.TryGetVariableValue<bool>(UDONSHARP_SCENE_BEHAVIOUR_UPGRADE_MARKER, out bool sceneBehaviourUpgraded) && sceneBehaviourUpgraded;
        }
        
        /// <summary>
        /// Runs a two pass upgrade of a set of prefabs, assumes all dependencies of the prefabs are included, otherwise the process could fail to maintain references.
        /// First creates a new UdonSharpBehaviour proxy script and hooks it to a given UdonBehaviour. Then in a second pass goes over all behaviours and serializes their data into the C# proxy and wipes their old data out.
        /// </summary>
        /// <param name="prefabRootEnumerable"></param>
        internal static void UpgradePrefabs(IEnumerable<GameObject> prefabRootEnumerable)
        {
            if (UdonSharpProgramAsset.IsAnyProgramAssetSourceDirty() ||
                UdonSharpProgramAsset.IsAnyProgramAssetOutOfDate())
            {
                UdonSharpCompilerV1.CompileSync();
            }

            GameObject[] prefabRoots = prefabRootEnumerable.ToArray();

            bool NeedsNewProxy(UdonBehaviour udonBehaviour)
            {
                if (!IsUdonSharpBehaviour(udonBehaviour))
                    return false;
                
                // The behaviour originates from a parent prefab so we don't want to modify this copy of the prefab
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(udonBehaviour) != udonBehaviour)
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                        UdonSharpUtils.LogWarning($"Nested prefab with UdonSharpBehaviours detected during prefab upgrade: {udonBehaviour.gameObject}, nested prefabs are not eligible for automatic upgrade.");
                    
                    return false;
                }

                if (GetProxyBehaviour(udonBehaviour))
                    return false;
                
                return true;
            }

            bool NeedsSerializationUpgrade(UdonBehaviour udonBehaviour)
            {
                if (!IsUdonSharpBehaviour(udonBehaviour))
                    return false;
                
                if (NeedsNewProxy(udonBehaviour))
                    return true;

                if (GetBehaviourVersion(udonBehaviour) == UdonSharpBehaviourVersion.V0DataUpgradeNeeded)
                    return true;
                
                return false;
            }

            HashSet<GameObject> phase1FixupPrefabRoots = new HashSet<GameObject>();

            // Phase 1 Pruning - Add missing proxy behaviours
            foreach (GameObject prefabRoot in prefabRoots)
            {
                if (!prefabRoot.GetComponentsInChildren<UdonBehaviour>(true).Any(NeedsNewProxy)) 
                    continue;
                
                string prefabPath = AssetDatabase.GetAssetPath(prefabRoot);

                if (!prefabPath.IsNullOrWhitespace())
                    phase1FixupPrefabRoots.Add(prefabRoot);
            }

            HashSet<GameObject> phase2FixupPrefabRoots = new HashSet<GameObject>(phase1FixupPrefabRoots);

            // Phase 2 Pruning - Check for behaviours that require their data ownership to be transferred Udon -> C#
            foreach (GameObject prefabRoot in prefabRoots)
            {
                foreach (UdonBehaviour udonBehaviour in prefabRoot.GetComponentsInChildren<UdonBehaviour>(true))
                {
                    if (NeedsSerializationUpgrade(udonBehaviour))
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(prefabRoot);

                        if (!prefabPath.IsNullOrWhitespace())
                            phase2FixupPrefabRoots.Add(prefabRoot);

                        break;
                    }
                }
            }
            
            // Now we have a set of prefabs that we can actually load and run the two upgrade phases on.
            // Todo: look at merging the two passes since we don't actually need to load prefabs into scenes apparently

            // Early out and avoid the edit scope
            if (phase1FixupPrefabRoots.Count == 0 && phase2FixupPrefabRoots.Count == 0)
                return;
            
            if (phase2FixupPrefabRoots.Count > 0)
                UdonSharpUtils.Log($"Running upgrade process on {phase2FixupPrefabRoots.Count} prefabs: {string.Join(", ", phase2FixupPrefabRoots.Select(e => e.name))}");
            
            using (new UdonSharpEditorManager.AssetEditScope())
            {
                foreach (GameObject prefabRoot in phase1FixupPrefabRoots)
                {
                    try
                    {
                        foreach (UdonBehaviour udonBehaviour in prefabRoot.GetComponentsInChildren<UdonBehaviour>(true))
                        {
                            if (!NeedsNewProxy(udonBehaviour))
                                continue;
                            
                            UdonSharpBehaviour newProxy = (UdonSharpBehaviour)udonBehaviour.gameObject.AddComponent(GetUdonSharpBehaviourType(udonBehaviour));
                            newProxy.enabled = udonBehaviour.enabled;

                            SetBackingUdonBehaviour(newProxy, udonBehaviour);

                            MoveComponentRelativeToComponent(newProxy, udonBehaviour, true);

                            SetBehaviourVersion(udonBehaviour, UdonSharpBehaviourVersion.V0DataUpgradeNeeded);
                        }

                        // Phase2 is a superset of phase 1 upgrades, and AssetEditScope prevents flushing to disk anyways so just don't save here.
                        // PrefabUtility.SavePrefabAsset(prefabRoot);
                        
                        // UdonSharpUtils.Log($"Ran prefab upgrade phase 1 on {prefabRoot}");
                    }
                    catch (Exception e)
                    {
                        UdonSharpUtils.LogError($"Encountered exception while upgrading prefab {prefabRoot}, report exception to Merlin: {e}");
                    }
                }

                foreach (GameObject prefabRoot in phase2FixupPrefabRoots)
                {
                    try
                    {
                        foreach (UdonBehaviour udonBehaviour in prefabRoot.GetComponentsInChildren<UdonBehaviour>(true))
                        {
                            if (!NeedsSerializationUpgrade(udonBehaviour))
                                continue;
                            
                            CopyUdonToProxy(GetProxyBehaviour(udonBehaviour), ProxySerializationPolicy.RootOnly);

                            // We can't remove this data for backwards compatibility :'(
                            // If we nuke the data, the unity object array on the underlying storage may change.
                            // Which means that if people have copies of this prefab in the scene with no object reference changes, their data will also get nuked which we do not want.
                            // Public variable data on the prefabs will never be touched again by U# after upgrading
                            // We will probably provide an optional upgrade process that strips this extra data, and takes into account all scenes in the project
                            
                            // foreach (string publicVarSymbol in udonBehaviour.publicVariables.VariableSymbols.ToArray())
                            //     udonBehaviour.publicVariables.RemoveVariable(publicVarSymbol);
                            
                            SetBehaviourVersion(udonBehaviour, UdonSharpBehaviourVersion.V1);
                            SetBehaviourUpgraded(udonBehaviour);
                        }

                        PrefabUtility.SavePrefabAsset(prefabRoot);
                        
                        // UdonSharpUtils.Log($"Ran prefab upgrade phase 2 on {prefabRoot}");
                    }
                    catch (Exception e)
                    {
                        UdonSharpUtils.LogError($"Encountered exception while upgrading prefab {prefabRoot}, report exception to Merlin: {e}");
                    }
                }
                
                UdonSharpUtils.Log("Prefab upgrade pass finished");
            }
        }
        
        internal static void UpgradeSceneBehaviours(IEnumerable<UdonBehaviour> behaviours)
        {
            // Create proxies if they do not exist
            foreach (UdonBehaviour udonBehaviour in behaviours)
            {
                if (!IsUdonSharpBehaviour(udonBehaviour))
                    continue;
                
                if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour) &&
                    PrefabUtility.IsAddedComponentOverride(udonBehaviour))
                    continue;

                if (GetProxyBehaviour(udonBehaviour) == null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour) &&
                        PrefabUtility.GetCorrespondingObjectFromSource(udonBehaviour) != udonBehaviour)
                    {
                        UdonSharpUtils.LogError($"Cannot upgrade scene behaviour '{udonBehaviour}' since its prefab must be upgraded.", udonBehaviour);
                        continue;
                    }
                    
                    Type udonSharpBehaviourType = GetUdonSharpBehaviourType(udonBehaviour);

                    if (!udonSharpBehaviourType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                    {
                        UdonSharpUtils.LogError($"Class script referenced by program asset '{udonBehaviour.programSource}' is not an UdonSharpBehaviour", udonBehaviour.programSource);
                        continue;
                    }
                    
                    UdonSharpBehaviour newProxy = (UdonSharpBehaviour)udonBehaviour.gameObject.AddComponent(udonSharpBehaviourType);
                    newProxy.enabled = udonBehaviour.enabled;

                    SetBackingUdonBehaviour(newProxy, udonBehaviour);

                    if (!PrefabUtility.IsAddedComponentOverride(udonBehaviour))
                    {
                        MoveComponentRelativeToComponent(newProxy, udonBehaviour, true);
                    }
                    else
                    {
                        UdonSharpUtils.LogWarning("Cannot reorder internal UdonBehaviour during upgrade because it is on a prefab instance.");
                    }

                    UdonSharpUtils.SetDirty(newProxy);
                }
                
                if (GetBehaviourVersion(udonBehaviour) == UdonSharpBehaviourVersion.V0)
                    SetBehaviourVersion(udonBehaviour, UdonSharpBehaviourVersion.V0DataUpgradeNeeded);
            }

            // Copy data over from UdonBehaviour to UdonSharpBehaviour
            foreach (UdonBehaviour udonBehaviour in behaviours)
            {
                if (!IsUdonSharpBehaviour(udonBehaviour))
                    continue;
                
                bool needsPrefabInstanceUpgrade = false;

                // Checks if the version is below V1 or if it needs the prefab instance upgrade
                UdonSharpBehaviourVersion behaviourVersion = GetBehaviourVersion(udonBehaviour);
                if (behaviourVersion >= UdonSharpBehaviourVersion.V1)
                {
                    // Check if the prefab instance has a prefab that was upgraded causing the string data to be copied, but has a delta'd UnityEngine.Object storage array
                    if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour) &&
                        !HasSceneBehaviourUpgradeFlag(udonBehaviour))
                    {
                        UdonBehaviour prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(udonBehaviour);
                    
                        if (prefabSource && BehaviourRequiresBackwardsCompatibilityPersistence(prefabSource))
                        {
                            PropertyModification[] modifications =
                                PrefabUtility.GetPropertyModifications(udonBehaviour);
                    
                            if (modifications != null &&
                                modifications.Any(e => e.propertyPath.StartsWith("publicVariablesUnityEngineObjects", StringComparison.Ordinal)))
                            {
                                needsPrefabInstanceUpgrade = true;
                            }
                        }
                    }

                    if (!needsPrefabInstanceUpgrade)
                        continue;
                }
                
                UdonSharpBehaviour proxy = GetProxyBehaviour(udonBehaviour);

                if (proxy == null)
                {
                    UdonSharpUtils.LogWarning($"UdonSharpBehaviour '{udonBehaviour}' could not be upgraded since it is missing a proxy", udonBehaviour);
                    continue;
                }
                
                CopyUdonToProxy(proxy, ProxySerializationPolicy.RootOnly);

                // Nuke out old data now because we want only the C# side to own the data from this point on
                
                ClearBehaviourVariables(udonBehaviour, true);
                            
                SetBehaviourVersion(udonBehaviour, UdonSharpBehaviourVersion.V1);
                SetSceneBehaviourUpgraded(udonBehaviour);

                if (needsPrefabInstanceUpgrade)
                    UdonSharpUtils.Log($"Scene behaviour '{udonBehaviour.name}' needed UnityEngine.Object upgrade pass", udonBehaviour);

                UdonSharpUtils.SetDirty(proxy);
                
                UdonSharpUtils.Log($"Upgraded scene behaviour '{udonBehaviour.name}'", udonBehaviour);
            }
        }

        internal static bool BehaviourNeedsSetup(UdonSharpBehaviour behaviour)
        {
            return GetBackingUdonBehaviour(behaviour) == null ||
                   behaviour.enabled != GetBackingUdonBehaviour(behaviour).enabled;
        }
        
        private static readonly MethodInfo _moveComponentRelativeToComponent = typeof(UnityEditorInternal.ComponentUtility).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).First(e => e.Name == "MoveComponentRelativeToComponent" && e.GetParameters().Length == 3);

        internal static void MoveComponentRelativeToComponent(Component component, Component targetComponent, bool aboveTarget)
        {
            _moveComponentRelativeToComponent.Invoke(null, new object[] { component, targetComponent, aboveTarget });
        }
        
        private static readonly FieldInfo _serializedProgramAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static void RunBehaviourSetup(UdonSharpBehaviour behaviour, bool withUndo)
        {
            UdonBehaviour backingBehaviour = GetBackingUdonBehaviour(behaviour);

            // Handle components pasted across different behaviours
            if (backingBehaviour && backingBehaviour.gameObject != behaviour.gameObject)
                backingBehaviour = null;

            // Handle pasting components on the same behaviour, assumes pasted components are always the last in the list.
            if (backingBehaviour)
            {
                int refCount = 0;
                UdonSharpBehaviour[] behaviours = backingBehaviour.GetComponents<UdonSharpBehaviour>();
                foreach (UdonSharpBehaviour udonSharpBehaviour in behaviours)
                {
                    if (GetBackingUdonBehaviour(udonSharpBehaviour) == backingBehaviour)
                        refCount++;
                }

                if (refCount > 1 && behaviour == behaviours.Last())
                {
                    backingBehaviour = null;
                }
            }

            bool isPartOfPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(behaviour) && 
                                          PrefabUtility.GetCorrespondingObjectFromSource(behaviour) != behaviour;

            if (backingBehaviour == null)
            {
                if (isPartOfPrefabInstance)
                {
                    UdonSharpUtils.LogWarning("Cannot setup behaviour on prefab instance, original prefab asset needs setup");
                    return;
                }
                
                SetIgnoreEvents(true);
                
                try
                {
                    backingBehaviour = withUndo ? Undo.AddComponent<UdonBehaviour>(behaviour.gameObject) : behaviour.gameObject.AddComponent<UdonBehaviour>();
                    
                #pragma warning disable CS0618 // Type or member is obsolete
                    backingBehaviour.SynchronizePosition = false;
                    backingBehaviour.AllowCollisionOwnershipTransfer = false;
                #pragma warning restore CS0618 // Type or member is obsolete

                    MoveComponentRelativeToComponent(backingBehaviour, behaviour, false);
                    
                    SetBackingUdonBehaviour(behaviour, backingBehaviour);
                    
                    SetBehaviourVersion(backingBehaviour, UdonSharpBehaviourVersion.CurrentVersion);
                    SetSceneBehaviourUpgraded(backingBehaviour);
                    
                    // UdonSharpUtils.Log($"Created behaviour {backingBehaviour}", behaviour);
                }
                finally
                {
                    SetIgnoreEvents(false);
                }
                
                _proxyBehaviourLookup.Add(backingBehaviour, behaviour);
                
                UdonSharpUtils.SetDirty(behaviour);
                UdonSharpUtils.SetDirty(backingBehaviour);
            }
            
            // Handle U# behaviours that have been added to a prefab via Added Component > Apply To Prefab, but have not had their backing behaviour added
            // if (isPartOfPrefabInstance && 
            //     backingBehaviour != null && 
            //     !PrefabUtility.IsPartOfPrefabInstance(backingBehaviour))
            // {
            //     PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(behaviour);
            //
            //     if (modifications != null)
            //     {
            //         
            //     }
            // }

            UdonSharpProgramAsset programAsset = GetUdonSharpProgramAsset(behaviour);

            if (backingBehaviour.programSource == null)
            {
                backingBehaviour.programSource = programAsset;
                if (backingBehaviour.programSource == null)
                    UdonSharpUtils.LogError($"Unable to find valid U# program asset associated with script '{behaviour}'", behaviour);
                
                UdonSharpUtils.SetDirty(backingBehaviour);
            }

            if (_serializedProgramAssetField.GetValue(backingBehaviour) == null)
            {
                SerializedObject componentAsset = new SerializedObject(backingBehaviour);
                SerializedProperty serializedProgramAssetProperty = componentAsset.FindProperty("serializedProgramAsset");

                serializedProgramAssetProperty.objectReferenceValue = programAsset.SerializedProgramAsset;

                if (withUndo)
                    componentAsset.ApplyModifiedProperties();
                else
                    componentAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            if (backingBehaviour.enabled != behaviour.enabled)
            {
                if (withUndo)
                    Undo.RecordObject(backingBehaviour, "Enabled change");
                    
                backingBehaviour.enabled = behaviour.enabled;

                if (!withUndo)
                {
                    UdonSharpUtils.SetDirty(backingBehaviour);
                }
            }

        #if UDONSHARP_DEBUG
            backingBehaviour.hideFlags &= ~HideFlags.HideInInspector;
        #else
            backingBehaviour.hideFlags |= HideFlags.HideInInspector;
        #endif
            
            ((UdonSharpProgramAsset)backingBehaviour.programSource)?.UpdateProgram();
        }

        internal static void RunBehaviourSetup(UdonSharpBehaviour behaviour)
        {
            RunBehaviourSetup(behaviour, false);
        }

        internal static void RunBehaviourSetupWithUndo(UdonSharpBehaviour behaviour)
        {
            RunBehaviourSetup(behaviour, true);
        }

        /// <summary>
        /// Returns true if the given behaviour is a proxy behaviour that's linked to an UdonBehaviour.
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool IsProxyBehaviour(UdonSharpBehaviour behaviour)
        {
            if (behaviour == null)
                return false;
            
            return GetBackingUdonBehaviour(behaviour) != null;
        }

        private static Dictionary<UdonBehaviour, UdonSharpBehaviour> _proxyBehaviourLookup = new Dictionary<UdonBehaviour, UdonSharpBehaviour>();

        /// <summary>
        /// Finds an existing proxy behaviour, if none exists returns null
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [Obsolete("FindProxyBehaviour is deprecated, use GetProxyBehaviour instead.")]
        public static UdonSharpBehaviour FindProxyBehaviour(UdonBehaviour udonBehaviour)
        {
            return FindProxyBehaviour_Internal(udonBehaviour);
        }

        /// <summary>
        /// Finds an existing proxy behaviour, if none exists returns null
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        private static UdonSharpBehaviour FindProxyBehaviour_Internal(UdonBehaviour udonBehaviour)
        {
            if (_proxyBehaviourLookup.TryGetValue(udonBehaviour, out UdonSharpBehaviour proxyBehaviour))
            {
                if (proxyBehaviour != null)
                    return proxyBehaviour;

                _proxyBehaviourLookup.Remove(udonBehaviour);
            }

            UdonSharpBehaviour[] behaviours = udonBehaviour.GetComponents<UdonSharpBehaviour>();
            
            foreach (UdonSharpBehaviour udonSharpBehaviour in behaviours)
            {
                IUdonBehaviour backingBehaviour = GetBackingUdonBehaviour(udonSharpBehaviour);
                if (backingBehaviour != null && ReferenceEquals(backingBehaviour, udonBehaviour))
                {
                    _proxyBehaviourLookup.Add(udonBehaviour, udonSharpBehaviour);

                    return udonSharpBehaviour;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the C# version of an UdonSharpBehaviour that proxies an UdonBehaviour with the program asset for the matching UdonSharpBehaviour type
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpBehaviour GetProxyBehaviour(UdonBehaviour udonBehaviour)
        {
            return GetProxyBehaviour_Internal(udonBehaviour);
        }

        /// <summary>
        /// Returns if the given UdonBehaviour is an UdonSharpBehaviour
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool IsUdonSharpBehaviour(UdonBehaviour udonBehaviour)
        {
            return udonBehaviour.programSource != null && 
                   udonBehaviour.programSource is UdonSharpProgramAsset programAsset && 
                   programAsset.sourceCsScript != null;
        }

        /// <summary>
        /// Gets the UdonSharpBehaviour type from the given behaviour.
        /// If the behaviour is not an UdonSharpBehaviour, returns null.
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        public static Type GetUdonSharpBehaviourType(UdonBehaviour udonBehaviour)
        {
            if (!IsUdonSharpBehaviour(udonBehaviour))
                return null;

            return ((UdonSharpProgramAsset)udonBehaviour.programSource).GetClass();
        }

        private static readonly FieldInfo _skipEventsField = typeof(UdonSharpBehaviour).GetField("_skipEvents", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Used to disable sending events to UdonSharpBehaviours for OnEnable, OnDisable, and OnDestroy since they are not always in a valid state to be recognized as proxies during these events.
        /// </summary>
        /// <param name="ignore"></param>
        internal static void SetIgnoreEvents(bool ignore)
        {
            _skipEventsField.SetValue(null, ignore);
        }

        /// <summary>
        /// Gets the C# version of an UdonSharpBehaviour that proxies an UdonBehaviour with the program asset for the matching UdonSharpBehaviour type
        /// </summary>
        /// <param name="udonBehaviour"></param>
        /// <returns></returns>
        [PublicAPI]
        private static UdonSharpBehaviour GetProxyBehaviour_Internal(UdonBehaviour udonBehaviour)
        {
            if (udonBehaviour == null)
                throw new ArgumentNullException(nameof(udonBehaviour));

            UdonSharpBehaviour proxyBehaviour = FindProxyBehaviour_Internal(udonBehaviour);
            
            return proxyBehaviour;
        }
        
        // private static readonly FieldInfo _publicVariablesUnityEngineObjectsField = typeof(UdonBehaviour).GetField("publicVariablesUnityEngineObjects", BindingFlags.NonPublic | BindingFlags.Instance);
        //
        // private static IEnumerable<Object> GetUdonBehaviourObjectReferences(UdonBehaviour behaviour)
        // {
        //     return (List<Object>)_publicVariablesUnityEngineObjectsField.GetValue(behaviour);
        // }
        //
        // internal static ImmutableHashSet<GameObject> CollectReferencedPrefabs(GameObject[] roots)
        // {
        //     HashSet<GameObject> allReferencedPrefabs = new HashSet<GameObject>();
        //
        //     foreach (var root in roots)
        //     {
        //         foreach (var udonBehaviour in root.GetComponentsInChildren<UdonBehaviour>(true))
        //         {
        //             if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
        //             {
        //                 allReferencedPrefabs.Add(PrefabUtility.GetNearestPrefabInstanceRoot(udonBehaviour));
        //             }
        //         }
        //     }
        //
        //     HashSet<GameObject> visitedRoots = new HashSet<GameObject>();
        //     HashSet<GameObject> currentRoots = new HashSet<GameObject>(roots);
        //
        //     while (currentRoots.Count > 0)
        //     {
        //         foreach (var root in currentRoots)
        //         {
        //             if (visitedRoots.Contains(root))
        //                 continue;
        //
        //             foreach (var udonBehaviour in root.GetComponentsInChildren<UdonBehaviour>(true))
        //             {
        //                 var objects = GetUdonBehaviourObjectReferences(udonBehaviour);
        //
        //                 foreach (var obj in objects)
        //                 {
        //                     if ((obj is GameObject || obj is Component) &&
        //                         PrefabUtility.IsPartOfPrefabAsset(obj))
        //                     {
        //                         
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //
        //     return allReferencedPrefabs.ToImmutableHashSet();
        // }

        internal static ImmutableHashSet<Object> CollectUdonSharpBehaviourRootDependencies(UdonSharpBehaviour behaviour)
        {
            if (!IsProxyBehaviour(behaviour))
                return ImmutableHashSet<Object>.Empty;
            
            UsbSerializationContext.Dependencies.Clear();
            
            CopyProxyToUdon(behaviour, ProxySerializationPolicy.CollectRootDependencies);

            return UsbSerializationContext.Dependencies.ToImmutableHashSet();
        }

        /// <summary>
        /// Copies the state of the proxy to its backing UdonBehaviour
        /// </summary>
        /// <param name="proxy"></param>
        [PublicAPI]
        public static void CopyProxyToUdon(UdonSharpBehaviour proxy)
        {
            CopyProxyToUdon(proxy, ProxySerializationPolicy.Default);
        }

        /// <summary>
        /// Copies the state of the UdonBehaviour to its proxy object
        /// </summary>
        /// <param name="proxy"></param>
        [PublicAPI]
        public static void CopyUdonToProxy(UdonSharpBehaviour proxy)
        {
            CopyUdonToProxy(proxy, ProxySerializationPolicy.Default);
        }

        /// <summary>
        /// Copies the state of the proxy to its backing UdonBehaviour
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="serializationPolicy"></param>
        [PublicAPI]
        public static void CopyProxyToUdon(UdonSharpBehaviour proxy, ProxySerializationPolicy serializationPolicy)
        {
            if (serializationPolicy.MaxSerializationDepth == 0)
                return;

            UdonSharpProgramAsset programAsset = GetUdonSharpProgramAsset(proxy);
            
            if (programAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion)
                throw new InvalidOperationException($"Cannot run serialization on U# behaviour '{proxy}' with outdated script version, wait until program assets have compiled.");

            if (programAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion)
                throw new InvalidOperationException($"Cannot run serialization on U# behaviour '{proxy}' with outdated behaviour version, wait until program assets have compiled.");

            Profiler.BeginSample("CopyProxyToUdon");

            try
            {
                lock (UsbSerializationContext.UsbLock)
                {
                    var udonBehaviourStorage = new SimpleValueStorage<UdonBehaviour>(GetBackingUdonBehaviour(proxy));

                    ProxySerializationPolicy lastPolicy = UsbSerializationContext.CurrentPolicy;
                    UsbSerializationContext.CurrentPolicy = serializationPolicy;

                    Serializer.CreatePooled(proxy.GetType()).WriteWeak(udonBehaviourStorage, proxy);

                    UsbSerializationContext.CurrentPolicy = lastPolicy;
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        /// <summary>
        /// Copies the state of the UdonBehaviour to its proxy object
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="serializationPolicy"></param>
        [PublicAPI]
        public static void CopyUdonToProxy(UdonSharpBehaviour proxy, ProxySerializationPolicy serializationPolicy)
        {
            if (serializationPolicy.MaxSerializationDepth == 0)
                return;

            UdonSharpProgramAsset programAsset = GetUdonSharpProgramAsset(proxy);
            
            if (programAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion)
                throw new InvalidOperationException($"Cannot run serialization on U# behaviour '{proxy}' with outdated script version, wait until program assets have compiled.");

            if (programAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion)
                throw new InvalidOperationException($"Cannot run serialization on U# behaviour '{proxy}' with outdated behaviour version, wait until program assets have compiled.");
            
            Profiler.BeginSample("CopyUdonToProxy");

            try
            {
                lock (UsbSerializationContext.UsbLock)
                {
                    var udonBehaviourStorage = new SimpleValueStorage<UdonBehaviour>(GetBackingUdonBehaviour(proxy));

                    ProxySerializationPolicy lastPolicy = UsbSerializationContext.CurrentPolicy;
                    UsbSerializationContext.CurrentPolicy = serializationPolicy;

                    object proxyObj = proxy;
                    Serializer.CreatePooled(proxy.GetType()).ReadWeak(ref proxyObj, udonBehaviourStorage);

                    UsbSerializationContext.CurrentPolicy = lastPolicy;
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        [PublicAPI]
        public static UdonBehaviour CreateBehaviourForProxy(UdonSharpBehaviour udonSharpBehaviour)
        {
            UdonBehaviour backingBehaviour = GetBackingUdonBehaviour(udonSharpBehaviour);

            CopyProxyToUdon(udonSharpBehaviour);

            return backingBehaviour;
        }

        /// <summary>
        /// Destroys an UdonSharpBehaviour proxy and its underlying UdonBehaviour
        /// </summary>
        /// <param name="behaviour"></param>
        [PublicAPI]
        public static void DestroyImmediate(UdonSharpBehaviour behaviour)
        {
            UdonBehaviour backingBehaviour = GetBackingUdonBehaviour(behaviour);
            
            Object.DestroyImmediate(behaviour);

            if (backingBehaviour)
            {
                _proxyBehaviourLookup.Remove(backingBehaviour);

                SetIgnoreEvents(true);

                try
                {
                    Object.DestroyImmediate(backingBehaviour);
                }
                finally
                {
                    SetIgnoreEvents(false);
                }
            }
        }

        internal static void DeletePrefabBuildAssets()
        {
        #if UDONSHARP_DEBUG
            return;
        #endif
            
            string prefabBuildPath = UdonSharpLocator.IntermediatePrefabPath;

            if (!Directory.Exists(prefabBuildPath))
                return;
            
            AssetDatabase.DeleteAsset(prefabBuildPath);
        }
    }
}
