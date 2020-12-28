using HarmonyLib;
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
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnChangePlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReloadCleanup;
            AssemblyReloadEvents.afterAssemblyReload += RunPostAssemblyBuildRefresh;
        }

        static bool _skipSceneOpen = false;

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!_skipSceneOpen)
            {
                List<UdonBehaviour> udonBehaviours = GetAllUdonBehaviours();

                RunAllUpdates(udonBehaviours);
            }
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
            InjectUnityEventInterceptors();
        }

        const string HARMONY_ID = "UdonSharp.Editor.EventPatch";

        private static void BeforeAssemblyReloadCleanup()
        {
            Harmony harmony = new Harmony(HARMONY_ID);
            harmony.UnpatchAll(HARMONY_ID);
        }

        static void InjectUnityEventInterceptors()
        {
            List<System.Type> udonSharpBehaviourTypes = new List<Type>();

            foreach (Assembly assembly in UdonSharpUtils.GetLoadedEditorAssemblies())
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (type != typeof(UdonSharpBehaviour) && type.IsSubclassOf(typeof(UdonSharpBehaviour)))
                        udonSharpBehaviourTypes.Add(type);
                }
            }
            
            Harmony harmony = new Harmony(HARMONY_ID);
            harmony.UnpatchAll(HARMONY_ID);

            MethodInfo injectedEvent = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.EventInterceptor), BindingFlags.Static | BindingFlags.Public);
            HarmonyMethod injectedMethod = new HarmonyMethod(injectedEvent);

            void InjectEvent(System.Type behaviourType, string eventName)
            {
                const BindingFlags eventBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                MethodInfo eventInfo = behaviourType.GetMethods(eventBindingFlags).FirstOrDefault(e => e.Name == eventName && e.ReturnType == typeof(void));

                try
                {
                    if (eventInfo != null) harmony.Patch(eventInfo, injectedMethod);
                }
                catch (System.Exception)
                {
                    Debug.LogWarning($"Failed to patch event {eventInfo} on {behaviourType}");
                }
            }

            foreach (System.Type udonSharpBehaviourType in udonSharpBehaviourTypes)
            {
                // Trigger events
                InjectEvent(udonSharpBehaviourType, "OnTriggerEnter");
                InjectEvent(udonSharpBehaviourType, "OnTriggerExit");
                InjectEvent(udonSharpBehaviourType, "OnTriggerStay");
                InjectEvent(udonSharpBehaviourType, "OnTriggerEnter2D");
                InjectEvent(udonSharpBehaviourType, "OnTriggerExit2D");
                InjectEvent(udonSharpBehaviourType, "OnTriggerStay2D");

                // Collision events
                InjectEvent(udonSharpBehaviourType, "OnCollisionEnter");
                InjectEvent(udonSharpBehaviourType, "OnCollisionExit");
                InjectEvent(udonSharpBehaviourType, "OnCollisionStay");
                InjectEvent(udonSharpBehaviourType, "OnCollisionEnter2D");
                InjectEvent(udonSharpBehaviourType, "OnCollisionExit2D");
                InjectEvent(udonSharpBehaviourType, "OnCollisionStay2D");

                // Controller
                InjectEvent(udonSharpBehaviourType, "OnControllerColliderHit");

                // Animator events
                InjectEvent(udonSharpBehaviourType, "OnAnimatorIK");
                InjectEvent(udonSharpBehaviourType, "OnAnimatorMove");

                // Mouse events
                InjectEvent(udonSharpBehaviourType, "OnMouseDown");
                InjectEvent(udonSharpBehaviourType, "OnMouseDrag");
                InjectEvent(udonSharpBehaviourType, "OnMouseEnter");
                InjectEvent(udonSharpBehaviourType, "OnMouseExit");
                InjectEvent(udonSharpBehaviourType, "OnMouseOver");
                InjectEvent(udonSharpBehaviourType, "OnMouseUp");
                InjectEvent(udonSharpBehaviourType, "OnMouseUpAsButton");

                // Particle events
                InjectEvent(udonSharpBehaviourType, "OnParticleCollision");
                InjectEvent(udonSharpBehaviourType, "OnParticleSystemStopped");
                InjectEvent(udonSharpBehaviourType, "OnParticleTrigger");
                InjectEvent(udonSharpBehaviourType, "OnParticleUpdateJobScheduled");

                // Rendering events
                InjectEvent(udonSharpBehaviourType, "OnPostRender");
                InjectEvent(udonSharpBehaviourType, "OnPreCull");
                InjectEvent(udonSharpBehaviourType, "OnPreRender");
                InjectEvent(udonSharpBehaviourType, "OnRenderImage");
                InjectEvent(udonSharpBehaviourType, "OnRenderObject");
                InjectEvent(udonSharpBehaviourType, "OnWillRenderObject");

                // Joint events
                InjectEvent(udonSharpBehaviourType, "OnJointBreak");
                InjectEvent(udonSharpBehaviourType, "OnJointBreak2D");

                // Audio
                InjectEvent(udonSharpBehaviourType, "OnAudioFilterRead");
                
                // Transforms
                InjectEvent(udonSharpBehaviourType, "OnTransformChildrenChanged");
                InjectEvent(udonSharpBehaviourType, "OnTransformParentChanged");

                // Object state, OnDisable and OnDestroy will get called regardless of the enabled state of the component, include OnEnable for consistency
                InjectEvent(udonSharpBehaviourType, "OnEnable");
                InjectEvent(udonSharpBehaviourType, "OnDisable");
                InjectEvent(udonSharpBehaviourType, "OnDestroy");
            }

            // Add method for checking if events need to be skipped
            InjectedMethods.shouldSkipEventsMethod = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), typeof(UdonSharpBehaviour).GetMethod("ShouldSkipEvents", BindingFlags.Static | BindingFlags.NonPublic));

            // Patch GUI object field drawer
            MethodInfo doObjectFieldMethod = typeof(EditorGUI).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault(e => e.Name == "DoObjectField" && e.GetParameters().Length == 9);

            HarmonyMethod objectFieldProxy = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.DoObjectFieldProxy)));
            harmony.Patch(doObjectFieldMethod, objectFieldProxy);

            System.Type validatorDelegateType = typeof(EditorGUI).GetNestedType("ObjectFieldValidator", BindingFlags.Static | BindingFlags.NonPublic);
            InjectedMethods.validationDelegate = Delegate.CreateDelegate(validatorDelegateType, typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.ValidateObjectReference)));

            InjectedMethods.objectValidatorMethod = typeof(EditorGUI).GetMethod("ValidateObjectReferenceValue", BindingFlags.NonPublic | BindingFlags.Static);

            MethodInfo crossSceneRefCheckMethod = typeof(EditorGUI).GetMethod("CheckForCrossSceneReferencing", BindingFlags.NonPublic | BindingFlags.Static);
            InjectedMethods.crossSceneRefCheckMethod = (Func<UnityEngine.Object, UnityEngine.Object, bool>)Delegate.CreateDelegate(typeof(Func<UnityEngine.Object, UnityEngine.Object, bool>), crossSceneRefCheckMethod);

            // Patch post BuildAssetBundles fixup function
            MethodInfo buildAssetbundlesMethod = typeof(BuildPipeline).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).First(e => e.Name == "BuildAssetBundles" && e.GetParameters().Length == 5);

            MethodInfo postBuildMethod = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.PostBuildAssetBundles), BindingFlags.Public | BindingFlags.Static);
            HarmonyMethod postBuildHarmonyMethod = new HarmonyMethod(postBuildMethod);

            MethodInfo preBuildMethod = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.PreBuildAssetBundles), BindingFlags.Public | BindingFlags.Static);
            HarmonyMethod preBuildHarmonyMethod = new HarmonyMethod(preBuildMethod);

            harmony.Patch(buildAssetbundlesMethod, preBuildHarmonyMethod, postBuildHarmonyMethod);

#if ODIN_INSPECTOR_3
            try
            {
                Assembly odinEditorAssembly = UdonSharpUtils.GetLoadedEditorAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == "Sirenix.OdinInspector.Editor");

                System.Type editorUtilityType = odinEditorAssembly.GetType("Sirenix.OdinInspector.Editor.CustomEditorUtility");

                MethodInfo resetCustomEditorsMethod = editorUtilityType.GetMethod("ResetCustomEditors");

                MethodInfo odinInspectorOverrideMethod = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.OdinInspectorOverride), BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod odinInspectorOverrideHarmonyMethod = new HarmonyMethod(odinInspectorOverrideMethod);

                harmony.Patch(resetCustomEditorsMethod, null, odinInspectorOverrideHarmonyMethod);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to patch Odin inspector fix for U#\nException: {e}");
            }
#endif
        }

        static class InjectedMethods
        {
            public static Delegate validationDelegate;
            public static MethodInfo objectValidatorMethod;
            public static Func<UnityEngine.Object, UnityEngine.Object, bool> crossSceneRefCheckMethod;
            public static Func<bool> shouldSkipEventsMethod;

            public static bool EventInterceptor(UdonSharpBehaviour __instance)
            {
                if (UdonSharpEditorUtility.IsProxyBehaviour(__instance) || shouldSkipEventsMethod())
                    return false;

                return true;
            }

            public static UnityEngine.Object ValidateObjectReference(UnityEngine.Object[] references, System.Type objType, SerializedProperty property, Enum options = null)
            {
                if (references.Length == 0)
                    return null;

                if (property != null)
                {
                    if (references[0] != null)
                    {
                        if (EditorSceneManager.preventCrossSceneReferences && crossSceneRefCheckMethod(references[0], property.serializedObject.targetObject))
                            return null;

                        if (references[0] is GameObject gameObject)
                        {
                            references = gameObject.GetComponents<UdonSharpBehaviour>();
                        }

                        foreach (UnityEngine.Object reference in references)
                        {
                            System.Type refType = reference.GetType();

                            if (objType.IsAssignableFrom(reference.GetType()))
                            {
                                return reference;
                            }
                            else if (reference is UdonBehaviour udonBehaviour && UdonSharpEditorUtility.IsUdonSharpBehaviour(udonBehaviour))
                            {
                                UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour);

                                if (proxy && objType.IsAssignableFrom(proxy.GetType()))
                                    return proxy;
                            }
                        }
                    }
                }
                else
                {
                    if (objType == typeof(UdonSharpBehaviour) ||
                        objType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                    {
                        foreach (UnityEngine.Object reference in references)
                        {
                            if (reference == null)
                                continue;

                            System.Type refType = reference.GetType();

                            if (objType.IsAssignableFrom(refType))
                            {
                                return reference;
                            }
                            else if (reference is GameObject referenceObject)
                            {
                                UnityEngine.Object foundRef = ValidateObjectReference(referenceObject.GetComponents<UdonSharpBehaviour>(), objType, null);

                                if (foundRef)
                                    return foundRef;
                            }
                            else if (reference is UdonBehaviour referenceBehaviour && UdonSharpEditorUtility.IsUdonSharpBehaviour(referenceBehaviour))
                            {
                                UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(referenceBehaviour);

                                if (proxy && objType.IsAssignableFrom(proxy.GetType()))
                                    return proxy;
                            }
                        }
                    }
                }

                return null;
            }

            delegate FieldInfo GetFieldInfoDelegate(SerializedProperty property, out System.Type type);
            static GetFieldInfoDelegate getFieldInfoFunc;

            public static bool DoObjectFieldProxy(ref System.Type objType, SerializedProperty property, ref object validator)
            {
                if (validator == null)
                {
                    if (objType != null && (objType == typeof(UdonSharpBehaviour) || objType.IsSubclassOf(typeof(UdonSharpBehaviour))))
                        validator = validationDelegate;
                    else if (property != null)
                    {
                        // Just in case, we don't want to blow up default Unity UI stuff if something goes wrong here.
                        try
                        {
                            if (getFieldInfoFunc == null)
                            {
                                Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies().First(e => e.GetName().Name == "UnityEditor");

                                System.Type scriptAttributeUtilityType = editorAssembly.GetType("UnityEditor.ScriptAttributeUtility");

                                MethodInfo fieldInfoMethod = scriptAttributeUtilityType.GetMethod("GetFieldInfoFromProperty", BindingFlags.NonPublic | BindingFlags.Static);

                                getFieldInfoFunc = (GetFieldInfoDelegate)Delegate.CreateDelegate(typeof(GetFieldInfoDelegate), fieldInfoMethod);
                            }

                            getFieldInfoFunc(property, out System.Type fieldType);

                            if (fieldType != null && (fieldType == typeof(UdonSharpBehaviour) || fieldType.IsSubclassOf(typeof(UdonSharpBehaviour))))
                            {
                                objType = fieldType;
                                validator = validationDelegate;
                            }
                        }
                        catch (Exception)
                        {
                            validator = null;
                        }
                    }
                }

                return true;
            }

            public static void PreBuildAssetBundles()
            {
                DestroyAllProxies();
                _skipSceneOpen = true;
            }

            public static void PostBuildAssetBundles()
            {
                CreateProxyBehaviours(GetAllUdonBehaviours());
                _skipSceneOpen = false;
            }

#if ODIN_INSPECTOR_3
            public static void OdinInspectorOverride()
            {
                UdonBehaviourDrawerOverride.OverrideUdonBehaviourDrawer();
            }
#endif
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

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                UdonSharpEditorCache.ResetInstance();
                if (UdonSharpEditorCache.Instance.LastBuildType == UdonSharpEditorCache.DebugInfoType.Client)
                {
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                }

                RunAllUpdates();
            }
            else if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (UdonSharpEditorCache.Instance.LastBuildType == UdonSharpEditorCache.DebugInfoType.Client)
                {
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                }
            }

            UdonSharpEditorCache.SaveOnPlayExit(state);
        }

        static void RunAllUpdates(List<UdonBehaviour> allBehaviours = null)
        {
            UdonSharpEditorUtility.SetIgnoreEvents(false);

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

                if (scene.isLoaded)
                {
                    int rootCount = scene.rootCount;

                    scene.GetRootGameObjects(rootObjects);

                    for (int j = 0; j < rootCount; ++j)
                    {
                        behaviourList.AddRange(rootObjects[j].GetComponentsInChildren<UdonBehaviour>(true));
                    }
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
                System.Type symbolUSharpType = behaviourUSharpAsset.GetClass();

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
        static bool VerifyArrayValidity(ref object rootArray, ref bool modifiedArray, System.Type rootArrayType, System.Type currentTargetType, int arrayDimensionCount, int currentDepth, string behaviourName, string variableName)
        {
            if (rootArray == null)
                return true;

            System.Type arrayStorageType = UdonSharpUtils.UserTypeToUdonType(rootArrayType);

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
                else if (rootArray.GetType() != arrayStorageType)
                {
                    System.Type targetElementType = arrayStorageType.GetElementType();

                    if (!targetElementType.IsArray /*&& (rootArray.GetType().GetElementType() == null || !rootArray.GetType().GetElementType().IsArray)*/)
                    {
                        Array rootArrayArr = (Array)rootArray;
                        int arrayLen = rootArrayArr.Length;
                        Array newArray = (Array)Activator.CreateInstance(arrayStorageType, new object[] { arrayLen });
                        rootArray = newArray;
                        modifiedArray = true;

                        for (int i = 0; i < arrayLen; ++i)
                        {
                            object oldValue = rootArrayArr.GetValue(i);

                            if (!oldValue.IsUnityObjectNull())
                            {
                                System.Type oldType = oldValue.GetType();

                                if (targetElementType.IsAssignableFrom(oldType))
                                {
                                    newArray.SetValue(oldValue, i);
                                }
                                else if (targetElementType.IsExplicitlyAssignableFrom(oldType))
                                {
                                    object newValue;
                                    try
                                    {
                                        newValue = Convert.ChangeType(oldValue, targetElementType);
                                    }
                                    catch (Exception e) when (e is InvalidCastException || e is OverflowException)
                                    {
                                        MethodInfo castMethod = oldType.GetCastMethod(targetElementType);

                                        if (castMethod != null)
                                            newValue = castMethod.Invoke(null, new object[] { oldValue });
                                        else
                                            newValue = targetElementType.IsValueType ? Activator.CreateInstance(targetElementType) : null;
                                    }

                                    newArray.SetValue(newValue, i);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (currentDepth == 1)
                            return false;
                        else
                        {
                            rootArray = null;
                            modifiedArray = true;
                        }
                    }
                }
            }
            else
            {
                Array array = rootArray as Array;
                if (array == null)
                    return false;

                if (array.GetType() != UdonSharpUtils.UserTypeToUdonType(currentTargetType))
                    return false;

                int arrayLen = array.Length;
                for (int i = 0; i < arrayLen; ++i)
                {
                    object elementObj = array.GetValue(i);

                    if (!VerifyArrayValidity(ref elementObj, ref modifiedArray, rootArrayType, currentTargetType.GetElementType(), arrayDimensionCount, currentDepth + 1, behaviourName, variableName))
                        return false;

                    array.SetValue(elementObj, i);
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

                        bool foundValue = publicVariables.TryGetVariableValue(variableSymbol, out object symbolValue);

                        // Remove this variable from the publicVariable list since UdonBehaviours set all null GameObjects, UdonBehaviours, and Transforms to the current behavior's equivalent object regardless of if it's marked as a `null` heap variable or `this`
                        // This default behavior is not the same as Unity, where the references are just left null. And more importantly, it assumes that the user has interacted with the inspector on that object at some point which cannot be guaranteed. 
                        // Specifically, if the user adds some public variable to a class, and multiple objects in the scene reference the program asset, 
                        //   the user will need to go through each of the objects' inspectors to make sure each UdonBehavior has its `publicVariables` variable populated by the inspector
                        if (foundValue &&
                            symbolValue.IsUnityObjectNull() &&
                            (publicFieldType == typeof(GameObject) || publicFieldType == typeof(UdonBehaviour) || publicFieldType == typeof(Transform)))
                        {
                            behaviour.publicVariables.RemoveVariable(variableSymbol);
                            updatedBehaviourVariables++;
                            continue;
                        }

                        System.Type programSymbolType = fieldDefinition.fieldSymbol.symbolCsType;

                        if (!symbolValue.IsUnityObjectNull())
                        {
                            System.Type valueType = symbolValue.GetType();

                            if (!programSymbolType.IsAssignableFrom(valueType))
                            {
                                updatedBehaviourVariables++;

                                if (programSymbolType.IsExplicitlyAssignableFrom(valueType))
                                {
                                    object convertedValue;
                                    try
                                    {
                                        convertedValue = Convert.ChangeType(symbolValue, programSymbolType);
                                    }
                                    catch (Exception e) when (e is InvalidCastException || e is OverflowException)
                                    {
                                        MethodInfo castMethod = valueType.GetCastMethod(programSymbolType);

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
                            else if (publicFieldType != programSymbolType) // It's assignable but the storage type is wrong
                            {
                                updatedBehaviourVariables++;
                                publicVariables.RemoveVariable(variableSymbol);
                                IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, symbolValue });
                                publicVariables.TryAddVariable(newVariable);
                            }
                        }
                        else if (publicFieldType != programSymbolType) // It's a null value, but the storage type is wrong so reassign the correct storage type
                        {
                            updatedBehaviourVariables++;
                            publicVariables.RemoveVariable(variableSymbol);
                            IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, null });
                            publicVariables.TryAddVariable(newVariable);
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

                            bool modifiedArray = false;
                            object arrayObject = symbolValue;

                            if (!VerifyArrayValidity(ref arrayObject, ref modifiedArray, currentType.MakeArrayType(), userType, arrayDepth, 1, behaviourName, variableSymbol))
                            {
                                publicVariables.RemoveVariable(variableSymbol);
                                object defaultValue = programAsset.GetPublicVariableDefaultValue(variableSymbol);
                                IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, defaultValue });
                                publicVariables.TryAddVariable(newVariable);
                                updatedBehaviourVariables++;
                            }
                            else if (modifiedArray)
                            {
                                publicVariables.RemoveVariable(variableSymbol);
                                IUdonVariable newVariable = (IUdonVariable)Activator.CreateInstance(typeof(UdonVariable<>).MakeGenericType(programSymbolType), new object[] { variableSymbol, arrayObject });
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
                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(udonBehaviour))
                    UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour, ProxySerializationPolicy.NoSerialization);
            }
        }

        static void DestroyAllProxies()
        {
            var allBehaviours = GetAllUdonBehaviours();

            foreach (UdonBehaviour behaviour in allBehaviours)
            {
                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(behaviour))
                {
                    UdonSharpBehaviour proxy = UdonSharpEditorUtility.FindProxyBehaviour(behaviour, ProxySerializationPolicy.NoSerialization);

                    if (proxy)
                        UnityEngine.Object.DestroyImmediate(proxy);
                }
            }
        }
    }
}
