
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharp.Updater;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Editor;
using Object = UnityEngine.Object;

#if ODIN_INSPECTOR_3
using Sirenix.OdinInspector.Editor;
#endif

namespace UdonSharpEditor
{
    [InitializeOnLoad]
    internal class UdonSharpEditorManager
    {
        static UdonSharpEditorManager()
        {
            RuntimeLogWatcher.InitLogWatcher();

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnChangePlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += RunPreAssemblyBuildRefresh;
            AssemblyReloadEvents.afterAssemblyReload += RunPostAssemblyBuildRefresh;
        }

        private static bool _skipSceneOpen;

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (_skipSceneOpen) 
                return;

            RunAllUpgrades();
            
            List<UdonBehaviour> udonBehaviours = GetAllUdonBehaviours();

            RunAllUpdates(udonBehaviours);
        }

        [PostProcessScene]
        private static void OnSceneBuild()
        {
            Scene currentScene = SceneManager.GetActiveScene();

            GameObject[] rootObjects = currentScene.GetRootGameObjects();

            HashSet<UdonBehaviour> allBehaviours = new HashSet<UdonBehaviour>();

            foreach (var rootObject in rootObjects)
            {
                foreach (var behaviour in rootObject.GetComponentsInChildren<UdonSharpBehaviour>(true))
                {
                    try
                    {
                        UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
                        
                        if (backingBehaviour)
                        {
                            UdonSharpEditorUtility.CopyProxyToUdon(behaviour, ProxySerializationPolicy.All);

                            allBehaviours.Add(backingBehaviour);
                        }
                    }
                    catch (Exception e)
                    {
                        UdonSharpUtils.LogError(e);
                    }
                }
            }
            
            PrepareUdonSharpBehavioursForPlay(allBehaviours, BuildPipeline.isBuildingPlayer);
            
            // We only nuke the components when building the actual level for game since we need the components for UI in play mode.
            if (!BuildPipeline.isBuildingPlayer)
                return;

            foreach (var rootObject in rootObjects)
            {
                foreach (var behaviour in rootObject.GetComponentsInChildren<UdonSharpBehaviour>(true))
                {
                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        internal static void RunPostBuildSceneFixup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RunAllUpdates();

            UdonEditorManager.Instance.RefreshQueuedProgramSources();
        }

        private static void RunPreAssemblyBuildRefresh()
        {
            // Make sure in progress compiles finish instead of getting orphaned and leaving Unity in an intermediate state waiting for the compile to finish.
            UdonSharpCompilerV1.WaitForCompile();
        }
        
        private static void RunPostAssemblyBuildRefresh()
        {
            InjectUnityEventInterceptors();
            
            if (!RunAllUpgrades())
                UdonSharpProgramAsset.CompileAllCsPrograms();
        }

        private const string HARMONY_ID = "UdonSharp.Editor.EventPatch";
        
        public static bool ConstructorWarningsDisabled { get; set; }

        private static void InjectUnityEventInterceptors()
        {
            Harmony harmony = new Harmony(HARMONY_ID);

            using (new UdonSharpUtils.UdonSharpAssemblyLoadStripScope())
                harmony.UnpatchAll(HARMONY_ID);

            MethodInfo injectedEvent = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.EventInterceptor), BindingFlags.Static | BindingFlags.Public);
            HarmonyMethod injectedMethod = new HarmonyMethod(injectedEvent);

            void InjectEvent(Type behaviourType, string eventName)
            {
                const BindingFlags eventBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                MethodInfo eventInfo = behaviourType.GetMethods(eventBindingFlags).FirstOrDefault(e => e.Name == eventName && e.ReturnType == typeof(void));

                try
                {
                    if (eventInfo != null) harmony.Patch(eventInfo, injectedMethod);
                }
                catch (Exception e)
                {
                    UdonSharpUtils.LogWarning($"Failed to patch event {eventInfo} on {behaviourType}\nException:\n{e}");
                }
            }

            using (new UdonSharpUtils.UdonSharpAssemblyLoadStripScope())
            {
                // We will allow user scripts to run their events if they are marked with ExecuteInEditMode. While running in editor, they will use the regular Unity C# runtime.
                // In theory cuts out a bit of overhead from patching methods when reloading in editor
                // Commented out for now since Unity is dumb
                // if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    foreach (Type udonSharpBehaviourType in TypeCache.GetTypesDerivedFrom<UdonSharpBehaviour>())
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

                        // Stuff that would get fired if the behaviour is enabled
                        InjectEvent(udonSharpBehaviourType, "Start");
                        InjectEvent(udonSharpBehaviourType, "Update");
                        InjectEvent(udonSharpBehaviourType, "FixedUpdate");
                        InjectEvent(udonSharpBehaviourType, "LateUpdate");
                        InjectEvent(udonSharpBehaviourType, "OnGUI");
                    }
                }

                // Add method for checking if events need to be skipped
                InjectedMethods.shouldSkipEventsMethod = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), typeof(UdonSharpBehaviour).GetMethod("ShouldSkipEvents", BindingFlags.Static | BindingFlags.NonPublic));

                // Patch GUI object field drawer
                MethodInfo doObjectFieldMethod = typeof(EditorGUI).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault(e => e.Name == "DoObjectField" && e.GetParameters().Length == 9);

                HarmonyMethod objectFieldProxy = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.DoObjectFieldProxy)));
                harmony.Patch(doObjectFieldMethod, objectFieldProxy);

                Type validatorDelegateType = typeof(EditorGUI).GetNestedType("ObjectFieldValidator", BindingFlags.Static | BindingFlags.NonPublic);
                InjectedMethods.validationDelegate = Delegate.CreateDelegate(validatorDelegateType, typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.ValidateObjectReference)));

                InjectedMethods.objectValidatorMethod = typeof(EditorGUI).GetMethod("ValidateObjectReferenceValue", BindingFlags.NonPublic | BindingFlags.Static);

                MethodInfo crossSceneRefCheckMethod = typeof(EditorGUI).GetMethod("CheckForCrossSceneReferencing", BindingFlags.NonPublic | BindingFlags.Static);
                InjectedMethods.crossSceneRefCheckMethod = (Func<UnityEngine.Object, UnityEngine.Object, bool>)Delegate.CreateDelegate(typeof(Func<UnityEngine.Object, UnityEngine.Object, bool>), crossSceneRefCheckMethod);

                // Patch post BuildAssetBundles fixup function
                MethodInfo buildAssetBundlesMethod = typeof(BuildPipeline).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).First(e => e.Name == "BuildAssetBundles" && e.GetParameters().Length == 5);

                MethodInfo postBuildMethod = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.PostBuildAssetBundles), BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod postBuildHarmonyMethod = new HarmonyMethod(postBuildMethod);

                MethodInfo preBuildMethod = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.PreBuildAssetBundles), BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod preBuildHarmonyMethod = new HarmonyMethod(preBuildMethod);

                harmony.Patch(buildAssetBundlesMethod, preBuildHarmonyMethod, postBuildHarmonyMethod);

                // Patch a workaround for errors in Unity's APIUpdaterHelper when in a Japanese locale
                MethodInfo findTypeInLoadedAssemblies = typeof(Editor).Assembly.GetType("UnityEditor.Scripting.Compilers.APIUpdaterHelper").GetMethod("FindTypeInLoadedAssemblies", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo injectedFindType = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.FindTypeInLoadedAssembliesPrefix), BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod injectedFindTypeHarmonyMethod = new HarmonyMethod(injectedFindType);

                harmony.Patch(findTypeInLoadedAssemblies, injectedFindTypeHarmonyMethod);
                
            #if ODIN_INSPECTOR_3
                try
                {
                    MethodInfo setupInspectorsMethodsetupInspectorsMethod = typeof(InspectorConfig).GetMethod("UpdateOdinEditors", BindingFlags.Public | BindingFlags.Instance);

                    MethodInfo odinInspectorOverrideMethod = typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.OdinInspectorOverride), BindingFlags.Public | BindingFlags.Static);
                    HarmonyMethod odinInspectorOverrideHarmonyMethod = new HarmonyMethod(odinInspectorOverrideMethod);

                    harmony.Patch(setupInspectorsMethodsetupInspectorsMethod, null, odinInspectorOverrideHarmonyMethod);
                }
                catch (Exception e)
                {
                    UdonSharpUtils.LogWarning($"Failed to patch Odin inspector fix for U#\nException: {e}");
                }
            #endif
                
                // Unity sucks
                // This is currently cursed, do not include with the user-facing stuff
                // This will prevent Unity from trying to compile dirty scripts when entering/exiting play mode which is great for iteration time, 
                //  however it will not compile the modified scripts after exiting play mode and reimporting until you actually change another script
                // MethodInfo dirtyScriptMethod = typeof(Editor).Assembly
                //     .GetType("UnityEditor.Scripting.ScriptCompilation.EditorCompilation").GetMethod(
                //         "DirtyScript",
                //         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                //
                // MethodInfo injectedDirtyScriptMethod = typeof(InjectedMethods).GetMethod(
                //     nameof(InjectedMethods.DirtyScriptOverride), BindingFlags.Static | BindingFlags.Public);
                //
                // HarmonyMethod injectedHarmonyCompileMethod = new HarmonyMethod(injectedDirtyScriptMethod);
                //
                // harmony.Patch(dirtyScriptMethod, injectedHarmonyCompileMethod);
                
                // Patch out the constructor check for U# behaviours since we instantiate them to retrieve the field values
                MethodBase monoBehaviourConstructorMethod = (MethodBase)typeof(MonoBehaviour).GetMember(".ctor", BindingFlags.CreateInstance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First();
                HarmonyMethod monoBehaviourConstructorHarmonyPatch = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.SkipMonoBehaviourConstructor)));

                harmony.Patch(monoBehaviourConstructorMethod, monoBehaviourConstructorHarmonyPatch);
                
                // Remove "Fetching fresh local config" message from SDK
                // var debugLogMethods = typeof(Debug).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == nameof(Debug.Log));
                // HarmonyMethod debugLogSilencer = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.DebugLogSilencer)));
                //
                // foreach (var debugLogMethod in debugLogMethods)
                // {
                //     harmony.Patch(debugLogMethod, debugLogSilencer);
                // }
                
                // Make title bars report when you are using a U# script
                MethodInfo inspectorTitleMethod = typeof(ObjectNames).GetMethod(nameof(ObjectNames.GetInspectorTitle), BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod inspectorTitleReplacement = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.GetInspectorTitleForUdon), BindingFlags.Public | BindingFlags.Static));

                harmony.Patch(inspectorTitleMethod, inspectorTitleReplacement);
                
                // Quick addition of handling for when an UdonBehaviour gets destroyed in play mode
                // todo: add proper handling to SDK
                MethodInfo udonBehaviourDestroyMethod = typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.OnDestroy), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                HarmonyMethod udonBehaviourDestroyPostfix = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.UdonBehaviourDestroyCleanup), BindingFlags.Public | BindingFlags.Static));

                harmony.Patch(udonBehaviourDestroyMethod, null, udonBehaviourDestroyPostfix);
                
                // Runtime sync the `enabled` property on UdonSharpBehaviour proxies
                // Wraps the title bar drawing via prefix+postfix to check if enabled state has changed 
                // This is done to allow the script to change its enabled state properly without getting overwritten when the user is viewing the inspector
                MethodInfo doInspectorTitlebarMethod = typeof(EditorGUI).GetMethod("DoInspectorTitlebar", BindingFlags.NonPublic | BindingFlags.Static, null, new[]
                    { typeof(Rect), typeof(int), typeof(bool), typeof(Object[]), typeof(SerializedProperty), typeof(GUIStyle) }, null);
                
                HarmonyMethod titlebarPrefix = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.DoInspectorTitlebarPrefix)));
                HarmonyMethod titlebarPostfix = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.DoInspectorTitlebarPostfix)));

                harmony.Patch(doInspectorTitlebarMethod, titlebarPrefix, titlebarPostfix);
                
                MethodInfo udonBehaviourOnEnable = typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.OnEnable), BindingFlags.Public | BindingFlags.Instance);
                MethodInfo udonBehaviourOnDisable = typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.OnDisable), BindingFlags.Public | BindingFlags.Instance);
                
                HarmonyMethod enableDisablePostfix = new HarmonyMethod(typeof(InjectedMethods).GetMethod(nameof(InjectedMethods.OnEnableDisablePostfix), BindingFlags.Public | BindingFlags.Static));

                harmony.Patch(udonBehaviourOnEnable, null, enableDisablePostfix);
                harmony.Patch(udonBehaviourOnDisable, null, enableDisablePostfix);
            }
        }

        private static class InjectedMethods
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
                            references = gameObject.GetComponents<UdonBehaviour>();
                        }

                        foreach (UnityEngine.Object reference in references)
                        {
                            System.Type refType = reference.GetType();

                            if (objType.IsInstanceOfType(reference))
                            {
                                return reference;
                            }
                            else if (reference is UdonSharpBehaviour udonBehaviour)
                            {
                                UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(udonBehaviour);

                                if (backingBehaviour)
                                    return backingBehaviour;
                            }
                        }
                    }
                }
                else
                {
                    if (objType == typeof(UdonBehaviour))
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
                                UnityEngine.Object foundRef = ValidateObjectReference(referenceObject.GetComponents<UdonBehaviour>(), objType, null);

                                if (foundRef)
                                    return foundRef;
                            }
                            else if (reference is UdonSharpBehaviour udonBehaviour)
                            {
                                UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(udonBehaviour);

                                if (backingBehaviour)
                                    return backingBehaviour;
                            }
                        }
                    }
                }

                return null;
            }

            delegate FieldInfo GetFieldInfoDelegate(SerializedProperty property, out Type type);

            private static GetFieldInfoDelegate _getFieldInfoFunc;

            public static bool DoObjectFieldProxy(ref System.Type objType, SerializedProperty property, ref object validator)
            {
                if (validator == null)
                {
                    if (objType != null && objType == typeof(UdonBehaviour))
                        validator = validationDelegate;
                    else if (property != null)
                    {
                        // Just in case, we don't want to blow up default Unity UI stuff if something goes wrong here.
                        try
                        {
                            if (_getFieldInfoFunc == null)
                            {
                                Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies().First(e => e.GetName().Name == "UnityEditor");

                                Type scriptAttributeUtilityType = editorAssembly.GetType("UnityEditor.ScriptAttributeUtility");

                                MethodInfo fieldInfoMethod = scriptAttributeUtilityType.GetMethod("GetFieldInfoFromProperty", BindingFlags.NonPublic | BindingFlags.Static);

                                _getFieldInfoFunc = (GetFieldInfoDelegate)Delegate.CreateDelegate(typeof(GetFieldInfoDelegate), fieldInfoMethod);
                            }

                            _getFieldInfoFunc(property, out Type fieldType);

                            if (fieldType != null && fieldType == typeof(UdonBehaviour))
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
                // DestroyAllProxies();
                _skipSceneOpen = true;
            }

            public static void PostBuildAssetBundles()
            {
                // CreateProxyBehaviours(GetAllUdonBehaviours());
                _skipSceneOpen = false;
            }

            public static bool FindTypeInLoadedAssembliesPrefix(Func<System.Type, bool> predicate, ref System.Type __result)
            {
                __result = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && !assembly.Location.StartsWith("data") && !IsIgnoredAssembly(assembly.GetName()))
                    .SelectMany(GetValidTypesIn)
                    .FirstOrDefault(predicate);

                return false;
            }

            static IEnumerable<System.Type> GetValidTypesIn(System.Reflection.Assembly assembly)
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                return types.Where(e => e != null);
            }

            private static string[] _ignoredAssemblies = { "^UnityScript$", "^System\\..*", "^mscorlib$" };

            static bool IsIgnoredAssembly(AssemblyName assemblyName)
            {
                string name = assemblyName.Name;
                return _ignoredAssemblies.Any(candidate => System.Text.RegularExpressions.Regex.IsMatch(name, candidate));
            }

        #if ODIN_INSPECTOR_3
            public static void OdinInspectorOverride()
            {
                UdonBehaviourDrawerOverride.OverrideUdonBehaviourDrawer();
            }
        #endif

            public static bool DirtyScriptOverride(string path, string assemblyFilename)
            {
                return !EditorApplication.isPlayingOrWillChangePlaymode;
            }

            public static bool SkipMonoBehaviourConstructor() => !UdonSharpEditorManager.ConstructorWarningsDisabled;

            public static bool DebugLogSilencer(object message)
            {
                if (message is string messageStr &&
                    (messageStr.Contains("Fetching fresh local config") ||
                     messageStr.StartsWith("AmplitudeAPI")))
                {
                    return false;
                }

                return true;
            }
            
            private static readonly MethodInfo _getScriptClassNameMethod = typeof(MonoBehaviour).GetMethod("GetScriptClassName", BindingFlags.Instance | BindingFlags.NonPublic);

            public static bool GetInspectorTitleForUdon(UnityEngine.Object obj, ref string __result)
            {
                if (obj != null && obj is UdonSharpBehaviour udonSharpBehaviour)
                {
                    // __result = $"{ObjectNames.NicifyVariableName((string)_getScriptClassNameMethod.Invoke(udonSharpBehaviour, Array.Empty<object>()))} (<color=#{(EditorGUIUtility.isProSkin ? "8B7FC6" : "665D91")}>U#</color> Script)";
                    // __result = $"<color=#{(EditorGUIUtility.isProSkin ? "8B7FC6" : "665D91")}>{ObjectNames.NicifyVariableName((string)_getScriptClassNameMethod.Invoke(udonSharpBehaviour, Array.Empty<object>()))} (U# Script)</color>";
                    // __result = $"{ObjectNames.NicifyVariableName((string)_getScriptClassNameMethod.Invoke(udonSharpBehaviour, Array.Empty<object>()))} (<i>U# Script</i>)";
                    __result = $"{ObjectNames.NicifyVariableName((string)_getScriptClassNameMethod.Invoke(udonSharpBehaviour, Array.Empty<object>()))} (U# Script)";
                    return false;
                }
                
                return true;
            }

            public static void UdonBehaviourDestroyCleanup(UdonBehaviour __instance)
            {
                if (!EditorApplication.isPlaying || !UdonSharpEditorUtility.IsUdonSharpBehaviour(__instance))
                    return;

                UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(__instance);

                if (proxy)
                {
                    Object.Destroy(proxy);
                }
            }

            public class InspectorEnabledStateTracker
            {
                public bool[] enabledStates;

                public InspectorEnabledStateTracker(bool[] enabledStates)
                {
                    this.enabledStates = enabledStates;
                }
            }
            
            public static void DoInspectorTitlebarPrefix(Object[] targetObjs, out InspectorEnabledStateTracker __state)
            {
                if (targetObjs != null && targetObjs.Length > 0 && targetObjs[0] is UdonSharpBehaviour)
                {
                    __state = new InspectorEnabledStateTracker(targetObjs.Cast<MonoBehaviour>().Select(e => e.enabled).ToArray());
                }
                else
                {
                    __state = null;
                }
            }

            public static void DoInspectorTitlebarPostfix(Object[] targetObjs, InspectorEnabledStateTracker __state)
            {
                if (__state == null)
                    return;
                
                for (int i = 0; i < targetObjs.Length; ++i)
                {
                    UdonSharpBehaviour proxyBehaviour = targetObjs[i] as UdonSharpBehaviour;
                    
                    if (!proxyBehaviour)
                        continue;

                    UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxyBehaviour);

                    if (!backingBehaviour)
                        continue;
                    
                    if (proxyBehaviour.enabled == __state.enabledStates[i] && 
                        proxyBehaviour.enabled == backingBehaviour.enabled)
                        continue;

                    // User has caused input that needs to change the backing behaviour
                    if (proxyBehaviour.enabled != backingBehaviour.enabled && 
                        proxyBehaviour.enabled != __state.enabledStates[i])
                    {
                        SerializedObject udonBehaviourObj = new SerializedObject(backingBehaviour);
                        udonBehaviourObj.FindProperty("m_Enabled").boolValue = proxyBehaviour.enabled;
                        udonBehaviourObj.ApplyModifiedProperties();
                    }
                    // Script has caused input that needs the proxy to change
                    else if (EditorApplication.isPlaying)
                    {
                        ((MonoBehaviour)targetObjs[i]).enabled = backingBehaviour.enabled;
                    }
                }
            }

            public static void OnEnableDisablePostfix(UdonBehaviour __instance)
            {
                if (!UdonSharpEditorUtility.IsUdonSharpBehaviour(__instance)) 
                    return;
                
                UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(__instance);

                if (!proxy)
                    return;

                if (proxy.enabled != __instance.enabled)
                    proxy.enabled = __instance.enabled;
            }
        }
        
        internal const string UPGRADE_MESSAGE = "U# scripts have been upgraded, wait for Unity to finish compiling changes and enter play mode again.";
        private const string ERROR_BLOCK_PLAYMODE = "All U# compile errors have to be fixed before you can enter playmode!";

        private static void OnChangePlayMode(PlayModeStateChange state)
        {
            UdonSharpCompilerV1.WaitForCompile();
            
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                {
                    // If there are errors, give an extra chance to compile and fix errors as it's possible that the user just Ctrl+Z'd a broken change which left file hashes the same and didn't run a compile to clear the error as a result.
                    UdonSharpCompilerV1.CompileSync();

                    if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                    {
                        // Prevent people from entering play mode when there are compile errors, like normal Unity C#
                        // READ ME
                        // --------
                        // If you think you know better and are about to edit this out, be aware that you gain nothing by doing so. 
                        // If a script hits a compile error, it will not update until the compile errors are resolved.
                        // You will just be left wondering "why aren't my scripts changing when I edit them?" since the old copy of the script will be used until the compile errors are resolved.
                        // --------
                        
                        EditorApplication.isPlaying = false;
                        UdonSharpUtils.ShowEditorNotification(ERROR_BLOCK_PLAYMODE);
                        UdonSharpUtils.LogError(ERROR_BLOCK_PLAYMODE);

                        return;
                    }
                }
                
                if (UdonSharpEditorCache.Instance.LastBuildType == UdonSharpEditorCache.DebugInfoType.Client)
                    UdonSharpCompilerV1.CompileSync();
                
                if (RunAllUpgrades())
                {
                    EditorApplication.isPlaying = false;
                    UdonSharpUtils.ShowEditorNotification(UPGRADE_MESSAGE);
                    UdonSharpUtils.LogWarning(UPGRADE_MESSAGE);

                    return;
                }
                    
                RunAllUpdates();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                UdonSharpEditorCache.ResetInstance();
                if (UdonSharpEditorCache.Instance.LastBuildType == UdonSharpEditorCache.DebugInfoType.Client)
                {
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                }

                RunAllUpdates();
                
                UdonSharpEditorUtility.DeletePrefabBuildAssets();
            }

            UdonSharpEditorCache.SaveOnPlayExit(state);
        }

        private static void RunAllUpdates(List<UdonBehaviour> allBehaviours = null)
        {
            UdonSharpEditorUtility.SetIgnoreEvents(false);

            if (allBehaviours == null)
                allBehaviours = GetAllUdonBehaviours();

            RepairPrefabProgramAssets(allBehaviours);
            RepairProgramAssetLinks(allBehaviours);
            UpdateSyncModes(allBehaviours);
            ValidateHideFlagsOnEmptyUdonBehaviours(allBehaviours);
            SanitizeProxyBehaviours(GetAllSceneGameObjectsWithUdonBehavioursOrUdonSharpBehaviours(), true, out _);
            ValidateMatchingProgramSource(allBehaviours);
            UpdateSerializedProgramAssets(allBehaviours);
        }

        private static bool _didSceneUpgrade;
        
        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlaying)
                return;
            
            AddProjectDefine();
            UpgradeAssetsIfNeeded();

            if (!_didSceneUpgrade && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                UdonSharpEditorUtility.UpgradeSceneBehaviours(GetAllUdonBehaviours());
                _didSceneUpgrade = true;
            }
        }

        private static bool _hasCheckedDefines;

        private static void AddProjectDefine()
        {
            if (_hasCheckedDefines || EditorApplication.isUpdating || EditorApplication.isCompiling)
                return;
            
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            string[] defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup).Split(';');

            if (!defines.Contains("UDONSHARP", StringComparer.OrdinalIgnoreCase))
            {
                defines = defines.AddItem("UDONSHARP").ToArray();
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", defines));
                UdonSharpUtils.Log("Updated scripting defines");
            }

            _hasCheckedDefines = true;
        }

        // Rely on assembly reload to clear this since it indicates the user needs to change a script
        private static bool _upgradeDeferredByScriptError;

        private static void UpgradeAssetsIfNeeded()
        {
            if (UdonSharpEditorCache.Instance.Info.projectNeedsUpgrade && 
                !EditorApplication.isCompiling && !EditorApplication.isUpdating && !_upgradeDeferredByScriptError)
            {
                if (UdonSharpUpgrader.UpgradeScripts())
                {
                    UdonSharpUtils.LogWarning("Needed to update scripts, deferring asset update.");
                    return;
                }

                if (UdonSharpUtils.DoesUnityProjectHaveCompileErrors())
                {
                    UdonSharpUtils.LogWarning("C# scripts have compile errors, prefab upgrade deferred until script errors are resolved.");
                    _upgradeDeferredByScriptError = true;
                    return;
                }
                
                UdonSharpProgramAsset.CompileAllCsPrograms();
                UdonSharpCompilerV1.WaitForCompile();
                
                if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                {
                    // Give chance to compile and resolve errors in case they are fixed already
                    UdonSharpCompilerV1.CompileSync();
                    
                    if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                    {
                        UdonSharpUtils.LogWarning("U# scripts have compile errors, prefab upgrade deferred until script errors are resolved.");
                        _upgradeDeferredByScriptError = true;
                        return;
                    }
                }

                try
                {
                    UdonSharpEditorUtility.UpgradePrefabs(GetAllPrefabsWithUdonSharpBehaviours());
                }
                finally
                {
                    UdonSharpEditorCache.Instance.ClearUpgradePassQueue();
                }
            }
        }

        private static List<UdonBehaviour> GetAllUdonBehaviours()
        {
            int sceneCount = EditorSceneManager.loadedSceneCount;
            
            List<GameObject> rootObjects = new List<GameObject>();
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

        private static List<UdonBehaviour> GetAllUdonBehaviours(Scene scene)
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

        private static GameObject[] GetAllSceneGameObjectsWithUdonBehavioursOrUdonSharpBehaviours()
        {
            List<GameObject> rootObjects = new List<GameObject>();
            HashSet<GameObject> foundGameObjects = new HashSet<GameObject>();
            
            int sceneCount = EditorSceneManager.loadedSceneCount;

            for (int i = 0; i < sceneCount; ++i)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);

                if (scene.isLoaded)
                {
                    int rootCount = scene.rootCount;
                    scene.GetRootGameObjects(rootObjects);

                    for (int j = 0; j < rootCount; ++j)
                    {
                        foundGameObjects.UnionWith(rootObjects[j].GetComponentsInChildren<UdonBehaviour>().Select(e => e.gameObject));
                        foundGameObjects.UnionWith(rootObjects[j].GetComponentsInChildren<UdonSharpBehaviour>().Select(e => e.gameObject));
                    }
                }
            }

            return foundGameObjects.ToArray();
        }

        private static IEnumerable<GameObject> GetAllPrefabsWithUdonSharpBehaviours()
        {
            List<GameObject> roots = new List<GameObject>();
            List<UdonBehaviour> behaviourScratch = new List<UdonBehaviour>();
            
            IEnumerable<string> allPrefabPaths = AssetDatabase.FindAssets("t:prefab").Select(AssetDatabase.GUIDToAssetPath);

            foreach (string prefabPath in allPrefabPaths)
            {
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefabRoot == null)
                    continue;
                
                var prefabAssetType = PrefabUtility.GetPrefabAssetType(prefabRoot);
                if (prefabAssetType == PrefabAssetType.Model || 
                    prefabAssetType == PrefabAssetType.MissingAsset)
                    continue;

                prefabRoot.GetComponentsInChildren<UdonBehaviour>(true, behaviourScratch);
                
                if (behaviourScratch.Count == 0)
                    continue;

                bool hasUdonSharpBehaviour = false;

                foreach (var behaviour in behaviourScratch)
                {
                    if (UdonSharpEditorUtility.IsUdonSharpBehaviour(behaviour))
                    {
                        hasUdonSharpBehaviour = true;
                        break;
                    }
                }

                if (hasUdonSharpBehaviour)
                    roots.Add(prefabRoot);
            }

            return roots;
        }

        private static IImmutableSet<GameObject> GetRootPrefabGameObjectRefs(IEnumerable<Object> unityObjects)
        {
            if (unityObjects == null)
                return ImmutableHashSet<GameObject>.Empty;
            
            HashSet<GameObject> newObjectSet = new HashSet<GameObject>();

            foreach (var unityObject in unityObjects)
            {
                if (unityObject == null)
                    continue;
                
                if (!(unityObject is Component || unityObject is GameObject))
                    continue;
                
                if (!PrefabUtility.IsPartOfAnyPrefab(unityObject))
                    continue;

                var prefabAssetType = PrefabUtility.GetPrefabAssetType(unityObject);
                if (prefabAssetType == PrefabAssetType.Model || 
                    prefabAssetType == PrefabAssetType.MissingAsset)
                    continue;

                Object sourceObject = unityObject;

                // Walk up the variant sources and add each root since people may have modifications to referenced prefabs in each variant and it's worth being conservative here
                while (sourceObject)
                {
                    GameObject currentGameObject = null;

                    if (sourceObject is GameObject gameObject)
                    {
                        currentGameObject = gameObject.transform.root.gameObject;
                    }
                    else if (sourceObject is Component component)
                    {
                        currentGameObject = component.transform.root.gameObject;
                    }

                    if (currentGameObject)
                        newObjectSet.Add(currentGameObject);

                    sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(sourceObject);
                }
            }

            return newObjectSet.ToImmutableHashSet();
        }

        private static readonly FieldInfo _serializedObjectReferencesField = typeof(UdonBehaviour).GetField("publicVariablesUnityEngineObjects", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _deserializePublicVariablesMethod = typeof(UdonBehaviour).GetMethod("DeserializePublicVariables", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _serializePublicVariablesMethod = typeof(UdonBehaviour).GetMethod("SerializePublicVariables", BindingFlags.NonPublic | BindingFlags.Instance);

        private static HashSet<Object> GetBehaviourComponentOrGameObjectReferences(UdonBehaviour behaviour)
        {
            HashSet<Object> unityObjects = new HashSet<Object>();

            if (behaviour == null)
                return unityObjects;
            
            var unityObjectList = (List<Object>)_serializedObjectReferencesField.GetValue(behaviour);

            if (unityObjectList == null)
                return unityObjects;

            foreach (var unityObject in unityObjectList)
            {
                if (unityObject is Component || unityObject is GameObject)
                    unityObjects.Add(unityObject);
            }
            
            return unityObjects;
        }

        private static HashSet<GameObject> CollectScenePrefabRoots(List<UdonBehaviour> allBehaviours)
        {
            HashSet<GameObject> prefabRoots = new HashSet<GameObject>();

            foreach (var udonBehaviour in allBehaviours)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                    continue;

                GameObject nearestInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(udonBehaviour);

                if (nearestInstanceRoot)
                {
                    prefabRoots.Add(PrefabUtility.GetCorrespondingObjectFromOriginalSource(nearestInstanceRoot));
                }
            }

            return prefabRoots;
        }

        private static IEnumerable<GameObject> CollectAllReferencedPrefabRoots(IEnumerable<UdonBehaviour> rootSet)
        {
            HashSet<GameObject> gameObjectsToVisit = new HashSet<GameObject>();

            foreach (var rootBehaviour in rootSet)
            {
                HashSet<Object> objects = GetBehaviourComponentOrGameObjectReferences(rootBehaviour);
                gameObjectsToVisit.UnionWith(GetRootPrefabGameObjectRefs(objects.Count != 0 ? objects : null));

                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(rootBehaviour))
                {
                    gameObjectsToVisit.UnionWith(GetRootPrefabGameObjectRefs(
                        UdonSharpEditorUtility.CollectUdonSharpBehaviourRootDependencies(
                            UdonSharpEditorUtility.GetProxyBehaviour(rootBehaviour))));
                }
            }
            
            HashSet<GameObject> visitedSet = new HashSet<GameObject>();

            while (gameObjectsToVisit.Count > 0)
            {
                HashSet<GameObject> newVisitSet = new HashSet<GameObject>();

                foreach (var gameObject in gameObjectsToVisit)
                {
                    if (visitedSet.Contains(gameObject))
                        continue;

                    visitedSet.Add(gameObject);
                    
                    foreach (var udonBehaviour in gameObject.GetComponentsInChildren<UdonBehaviour>(true))
                    {
                        HashSet<Object> objectRefs;

                        if (UdonSharpEditorUtility.IsUdonSharpBehaviour(udonBehaviour))
                        {
                            UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour);

                            if (proxy && !UdonSharpEditorUtility.BehaviourNeedsSetup(proxy))
                            {
                                objectRefs = new HashSet<Object>(UdonSharpEditorUtility.CollectUdonSharpBehaviourRootDependencies(proxy));
                            }
                            else
                            {
                                objectRefs = GetBehaviourComponentOrGameObjectReferences(udonBehaviour);
                            }
                        }
                        else
                        {
                            objectRefs = GetBehaviourComponentOrGameObjectReferences(udonBehaviour);
                        }
                        
                        foreach (var newObjRef in GetRootPrefabGameObjectRefs((objectRefs != null && objectRefs.Count != 0) ? objectRefs : null))
                        {
                            if (!visitedSet.Contains(newObjRef))
                                newVisitSet.Add(newObjRef);
                        }
                    }
                }

                gameObjectsToVisit = newVisitSet;
            }
            
            return visitedSet;
        }

        private static void RepairPrefabProgramAssets(List<UdonBehaviour> dependencyRoots)
        {
            Dictionary<AbstractSerializedUdonProgramAsset, UdonSharpProgramAsset> udonSharpProgramAssetLookup =
                new Dictionary<AbstractSerializedUdonProgramAsset, UdonSharpProgramAsset>();

            foreach (UdonSharpProgramAsset programAsset in UdonSharpProgramAsset.GetAllUdonSharpPrograms())
                udonSharpProgramAssetLookup.Add(programAsset.SerializedProgramAsset, programAsset);

            HashSet<UnityEngine.Object> dependencies = new HashSet<UnityEngine.Object>();
            
            // Yes, this is not as thorough as AssetDatabase.GetDependencies, it is however much faster and catches the important cases.
            // Notably does not gather indirect UdonBehaviour dependencies when one behaviour references a prefab and that prefab references another prefab, mostly because I'm too lazy to handle it at the moment
            // Also does not gather any dependencies from Unity component's that reference game objects since that is not something that people should be using for prefab references anyways
            foreach (UdonBehaviour dependencyRoot in dependencyRoots)
            {
                dependencies.Add(dependencyRoot.gameObject);
                
                IEnumerable<Object> behaviourDependencies = ((List<UnityEngine.Object>)_serializedObjectReferencesField.GetValue(dependencyRoot))?.Where(e => e != null);
                
                if (behaviourDependencies != null)
                    dependencies.UnionWith(behaviourDependencies);
            }
            
            HashSet<string> prefabsToRepair = new HashSet<string>();
            List<UdonBehaviour> foundBehaviours = new List<UdonBehaviour>();

            foreach (UnityEngine.Object dependencyObject in dependencies)
            {
                if (!PrefabUtility.IsPartOfAnyPrefab(dependencyObject))
                    continue;
                
                GameObject prefabRoot = null;

                if (PrefabUtility.IsPartOfPrefabAsset(dependencyObject))
                {
                    prefabRoot = dependencyObject as GameObject;
                }
                else if (dependencyObject is GameObject dependencyGameObject)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(dependencyGameObject))
                    {
                        prefabRoot = PrefabUtility.GetCorrespondingObjectFromOriginalSource(dependencyGameObject);
                    }
                    else if (PrefabUtility.IsPartOfPrefabInstance(dependencyObject))
                    {
                        prefabRoot = PrefabUtility.GetCorrespondingObjectFromOriginalSource(PrefabUtility.GetNearestPrefabInstanceRoot(dependencyObject));
                    }
                }

                if (prefabRoot)
                    prefabRoot.GetComponentsInChildren<UdonBehaviour>(true, foundBehaviours);
                else
                    foundBehaviours.Clear();
                
                if (foundBehaviours.Count == 0)
                    continue;

                foreach (UdonBehaviour behaviour in foundBehaviours)
                {
                    if (behaviour.programSource != null)
                        continue;

                    if (_serializedAssetField.GetValue(behaviour) != null)
                    {
                        prefabsToRepair.Add(AssetDatabase.GetAssetPath(prefabRoot));
                        break;
                    }
                }
            }
            
            foundBehaviours.Clear();

            foreach (string repairPrefab in prefabsToRepair)
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(repairPrefab);
                
                if (PrefabUtility.IsPartOfImmutablePrefab(prefabRoot))
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    continue;
                }

                try
                {
                    prefabRoot.GetComponentsInChildren<UdonBehaviour>(true, foundBehaviours);

                    foreach (UdonBehaviour behaviour in foundBehaviours)
                    {
                        if (behaviour.programSource != null)
                            continue;
                        
                        AbstractSerializedUdonProgramAsset serializedUdonProgramAsset =
                            (AbstractSerializedUdonProgramAsset) _serializedAssetField.GetValue(behaviour);
                        
                        if (serializedUdonProgramAsset == null)
                            continue;
                        
                        if (udonSharpProgramAssetLookup.TryGetValue(serializedUdonProgramAsset,
                            out UdonSharpProgramAsset foundProgramAsset))
                        {
                            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
                            serializedBehaviour.FindProperty(nameof(UdonBehaviour.programSource)).objectReferenceValue = foundProgramAsset;
                            serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
                        
                            UdonSharpUtils.LogWarning($"Repaired reference to {foundProgramAsset} on prefab {prefabRoot}", prefabRoot);
                        }
                    }
                    
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, repairPrefab);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void RepairProgramAssetLinks(List<UdonBehaviour> udonBehaviours)
        {
            Dictionary<AbstractSerializedUdonProgramAsset, UdonSharpProgramAsset> udonSharpProgramAssetLookup =
                new Dictionary<AbstractSerializedUdonProgramAsset, UdonSharpProgramAsset>();

            foreach (UdonSharpProgramAsset programAsset in UdonSharpProgramAsset.GetAllUdonSharpPrograms())
            {
                if (programAsset.SerializedProgramAsset)
                    udonSharpProgramAssetLookup.Add(programAsset.SerializedProgramAsset, programAsset);
            }

            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
                if (behaviour.programSource != null)
                    continue;
                
                SerializedObject serializedBehaviour = new SerializedObject(behaviour);
                SerializedProperty serializedProgramProperty = serializedBehaviour.FindProperty("serializedProgramAsset");

                AbstractSerializedUdonProgramAsset serializedUdonProgramAsset =
                    (AbstractSerializedUdonProgramAsset) serializedProgramProperty.objectReferenceValue;

                if (serializedUdonProgramAsset != null)
                {
                    if (udonSharpProgramAssetLookup.TryGetValue(serializedUdonProgramAsset,
                        out UdonSharpProgramAsset foundProgramAsset))
                    {
                        serializedBehaviour.FindProperty(nameof(UdonBehaviour.programSource)).objectReferenceValue = foundProgramAsset;
                        serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
                        
                        UdonSharpUtils.SetDirty(behaviour);
                        
                        UdonSharpUtils.LogWarning($"Repaired reference to {foundProgramAsset} on {behaviour}");
                    }
                    else
                    {
                        UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);

                        if (proxy == null)
                            continue;
                        
                        UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(proxy);

                        if (programAsset == null)
                            continue;
                        
                        serializedBehaviour.FindProperty(nameof(UdonBehaviour.programSource)).objectReferenceValue = programAsset;
                        serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
                        
                        UdonSharpUtils.SetDirty(behaviour);

                        UdonSharpUtils.LogWarning($"Repaired reference to {programAsset} on {behaviour} using proxy reference");
                    }
                }
                else
                {
                    UdonSharpUtils.LogWarning($"Empty UdonBehaviour found on {behaviour.gameObject}", behaviour);
                }
            }
        }

        private static readonly FieldInfo _serializedAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void UpdateSerializedProgramAssets(List<UdonBehaviour> udonBehaviours)
        {
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
                UdonSharpProgramAsset programAsset = behaviour.programSource as UdonSharpProgramAsset;
                if (programAsset == null)
                    continue;

                AbstractSerializedUdonProgramAsset serializedProgramAsset = _serializedAssetField.GetValue(behaviour) as AbstractSerializedUdonProgramAsset;

                if (serializedProgramAsset == null || 
                    serializedProgramAsset != programAsset.SerializedProgramAsset)
                {
                    SerializedObject serializedBehaviour = new SerializedObject(behaviour);
                    SerializedProperty serializedProgramProperty = serializedBehaviour.FindProperty("serializedProgramAsset");
                    serializedProgramProperty.objectReferenceValue = programAsset.SerializedProgramAsset;
                    serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void UpdateSyncModes(List<UdonBehaviour> udonBehaviours)
        {
            int modificationCount = 0;

            HashSet<GameObject> behaviourGameObjects = new HashSet<GameObject>();

            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
                if (behaviour.programSource == null || !(behaviour.programSource is UdonSharpProgramAsset programAsset))
                    continue;

                behaviourGameObjects.Add(behaviour.gameObject);

                if (programAsset.behaviourSyncMode == BehaviourSyncMode.Any ||
                    programAsset.behaviourSyncMode == BehaviourSyncMode.NoVariableSync)
                    continue;
                
                if (behaviour.SyncMethod == Networking.SyncType.None && programAsset.behaviourSyncMode != BehaviourSyncMode.None ||
                    behaviour.SyncMethod == Networking.SyncType.Continuous && programAsset.behaviourSyncMode != BehaviourSyncMode.Continuous ||
                    behaviour.SyncMethod == Networking.SyncType.Manual && programAsset.behaviourSyncMode != BehaviourSyncMode.Manual)
                {
                    switch (programAsset.behaviourSyncMode)
                    {
                        case BehaviourSyncMode.Continuous:
                            behaviour.SyncMethod = Networking.SyncType.Continuous;
                            break;
                        case BehaviourSyncMode.Manual:
                            behaviour.SyncMethod = Networking.SyncType.Manual;
                            break;
                        case BehaviourSyncMode.None:
                            behaviour.SyncMethod = Networking.SyncType.None;
                            break;
                    }

                    modificationCount++;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }
            }

            // Validation for mixed sync modes which can break sync on things and auto update NoVariableSync behaviours to match the sync mode of other behaviours on the GameObject
            foreach (GameObject gameObject in behaviourGameObjects)
            {
                UdonBehaviour[] objectBehaviours = gameObject.GetComponents<UdonBehaviour>();

                bool hasManual = false;
                bool hasContinuous = false;
                bool hasUdonPositionSync = false;
                bool hasNoSync = false;

                foreach (UdonBehaviour objectBehaviour in objectBehaviours)
                {
                    if (UdonSharpEditorUtility.IsUdonSharpBehaviour(objectBehaviour) &&
                        ((UdonSharpProgramAsset)objectBehaviour.programSource).behaviourSyncMode == BehaviourSyncMode.NoVariableSync)
                    {
                        hasNoSync = true;
                        continue;
                    }

                    if (objectBehaviour.SyncMethod == Networking.SyncType.Manual)
                        hasManual = true;
                    else if (objectBehaviour.SyncMethod == Networking.SyncType.Continuous)
                        hasContinuous = true;

#pragma warning disable CS0618 // Type or member is obsolete
                    if (objectBehaviour.SynchronizePosition)
                        hasUdonPositionSync = true;
#pragma warning restore CS0618 // Type or member is obsolete
                }

                if (hasManual)
                {
                    if (hasContinuous)
                        UdonSharpUtils.LogWarning($"UdonBehaviours on GameObject '{gameObject.name}' have conflicting synchronization methods, this can cause sync to work unexpectedly.", gameObject);

                    if (gameObject.GetComponent<VRC.SDK3.Components.VRCObjectSync>())
                        UdonSharpUtils.LogWarning($"UdonBehaviours on GameObject '{gameObject.name}' are using manual sync while VRCObjectSync is on the GameObject, this can cause sync to work unexpectedly.", gameObject);

                    if (hasUdonPositionSync)
                        UdonSharpUtils.LogWarning($"UdonBehaviours on GameObject '{gameObject.name}' are using manual sync while position sync is enabled on an UdonBehaviour on the GameObject, this can cause sync to work unexpectedly.", gameObject);
                }

                if (hasNoSync)
                {
                    int conflictCount = 0;

                    if (hasManual && hasContinuous)
                        ++conflictCount;
                    if (hasManual && (hasUdonPositionSync || gameObject.GetComponent<VRC.SDK3.Components.VRCObjectSync>()))
                        ++conflictCount;
                    
                    if (conflictCount > 0)
                    {
                        UdonSharpUtils.LogWarning($"Cannot update sync mode on UdonSharpBehaviour with NoVariableSync on '{gameObject}' because there are conflicting sync types on the GameObject", gameObject);
                        continue;
                    }

                    foreach (UdonBehaviour behaviour in objectBehaviours)
                    {
                        if (behaviour.programSource is UdonSharpProgramAsset programAsset && programAsset.behaviourSyncMode == BehaviourSyncMode.NoVariableSync)
                        {
                            if (hasManual && behaviour.SyncMethod != Networking.SyncType.Manual)
                            {
                                behaviour.SyncMethod = Networking.SyncType.Manual;
                                modificationCount++;

                                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                            }
                            else if (behaviour.SyncMethod == Networking.SyncType.Manual)
                            {
                                behaviour.SyncMethod = Networking.SyncType.Continuous;
                                modificationCount++;

                                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                            }
                        }
                    }
                }
            }

            if (modificationCount > 0)
                EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// If a user deletes a U# script+program asset without clearing components in the scene referencing it beforehand, they can be stuck with an invisible UdonBehaviour that's orphaned from its proxy.
        /// So we make a pass and check that UdonBehaviours without a program asset reference aren't hidden and unhide them + warn the user if we find any.
        /// </summary>
        private static void ValidateHideFlagsOnEmptyUdonBehaviours(List<UdonBehaviour> allBehaviours)
        {
            foreach (UdonBehaviour udonBehaviour in allBehaviours)
            {
                if (udonBehaviour.programSource == null && (udonBehaviour.hideFlags & HideFlags.HideInInspector) != 0)
                {
                    UdonSharpUtils.LogWarning($"Hidden UdonBehaviour '{udonBehaviour.name}' with no linked program source was found and unhidden, check if you have recently deleted any UdonSharpProgramAssets, or have not imported a package with U# scripts fully.", udonBehaviour);
                    udonBehaviour.hideFlags &= ~(HideFlags.HideInInspector);
                    // We don't set dirty to make it more likely to pester the user until they fix the issue :)
                    // UdonSharpUtils.SetDirty(udonBehaviour);
                }
            }
        }

        /// <summary>
        /// Validates that all UdonBehaviour program source references match with their linked proxy behaviour's type, if they do not, we find the expected program asset type and assign it.
        /// </summary>
        private static void ValidateMatchingProgramSource(List<UdonBehaviour> allBehaviours)
        {
            foreach (UdonBehaviour behaviour in allBehaviours)
            {
                if (!UdonSharpEditorUtility.IsUdonSharpBehaviour(behaviour))
                    continue;
                
                UdonSharpProgramAsset programAsset = behaviour.programSource as UdonSharpProgramAsset;

                UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);
                
                if (proxy == null)
                    continue;

                Type scriptType = programAsset.sourceCsScript.GetClass();
                Type proxyType = proxy.GetType();
                
                if (scriptType != null && programAsset.sourceCsScript.GetClass() != proxyType)
                {
                    UdonSharpProgramAsset scriptAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(proxyType);

                    if (scriptAsset == null)
                    {
                        UdonSharpUtils.LogError($"Could not find program asset for UdonSharpBehaviour script type '{proxyType}'", proxy);
                        continue;
                    }

                    if (scriptAsset.sourceCsScript == null)
                    {
                        UdonSharpUtils.LogError($"Script asset '{scriptAsset}' found for type '{proxyType}' has null source script, this is not valid.", proxy);
                        continue;
                    }

                    if (scriptAsset.sourceCsScript.GetType() != proxyType)
                    {
                        UdonSharpUtils.LogError($"Script asset '{scriptAsset}' found for type '{proxyType}' actually has type '{scriptAsset.sourceCsScript.GetType()}' this is not valid.", proxy);
                        continue;
                    }
                    
                    if (scriptAsset)
                    {
                        UdonSharpUtils.LogWarning($"Script type on UdonSharpBehaviour proxy '{proxy}' did not match assigned UdonSharpProgramAsset '{behaviour.programSource}', assigned matching program asset '{programAsset}' for type.", proxy);
                        behaviour.programSource = programAsset;
                        UdonSharpUtils.SetDirty(behaviour);
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that there are no conflicting proxy behaviours and the associations of 1 proxy to 1 UdonBehaviour are maintained.
        /// </summary>
        private static void SanitizeProxyBehaviours(GameObject[] allGameObjects, bool applyUpdates, out bool neededUpdates)
        {
            neededUpdates = false;
            
            // Check that all U# behaviours are setup fully
            foreach (GameObject gameObject in allGameObjects)
            {
                UdonSharpBehaviour[] proxyBehaviours = gameObject.GetComponents<UdonSharpBehaviour>();

                foreach (UdonSharpBehaviour proxyBehaviour in proxyBehaviours)
                {
                    if ((proxyBehaviour.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    {
                        UdonSharpUtils.LogWarning("Obsolete proxy instance found from U# 0.X, cleaning up proxy.", proxyBehaviour.gameObject);
                        
                        Object.DestroyImmediate(proxyBehaviour);
                        continue;
                    }

                    UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(proxyBehaviour);
                    
                    if (programAsset.ScriptVersion < UdonSharpProgramVersion.V1SerializationUpdate)
                        continue;

                    UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxyBehaviour);

                    if (backingBehaviour && backingBehaviour.gameObject != proxyBehaviour.gameObject)
                    {
                        if (applyUpdates)
                        {
                            UdonSharpUtils.LogWarning($"Invalid backing behaviour on {proxyBehaviour}, clearing reference.", proxyBehaviour);
                            UdonSharpEditorUtility.SetBackingUdonBehaviour(proxyBehaviour, null);
                        }

                        neededUpdates = true;
                    }

                    if (UdonSharpEditorUtility.BehaviourNeedsSetup(proxyBehaviour))
                    {
                        if (applyUpdates)
                        {
                            UdonSharpUtils.LogWarning($"UdonSharpBehaviour on {proxyBehaviour.gameObject} has not been fully setup, running setup.", proxyBehaviour);
                            UdonSharpEditorUtility.RunBehaviourSetup(proxyBehaviour);
                        }

                        neededUpdates = true;
                    }
                }
            }
            
            // Verifies that there aren't dangling references to behaviours, this may be changed to do the upgrade pass
            foreach (UdonBehaviour behaviour in allGameObjects.SelectMany(e => e.GetComponents<UdonBehaviour>()))
            {
                if (!UdonSharpEditorUtility.IsUdonSharpBehaviour(behaviour))
                    continue;

                UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)behaviour.programSource;
                
                if (programAsset.ScriptVersion < UdonSharpProgramVersion.V1SerializationUpdate)
                    continue;

                UdonSharpBehaviour proxyBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);

                if (proxyBehaviour == null)
                {
                    UdonSharpUtils.LogWarning($"UdonBehaviour '{behaviour}' pointing to U# program '{programAsset.name}' does not have a proxy behaviour associated with it.", behaviour);
                    behaviour.hideFlags &= ~HideFlags.HideInInspector;
                }
            }
            
            foreach (UdonSharpBehaviour behaviour in allGameObjects.SelectMany(e => e.GetComponents<UdonSharpBehaviour>()))
            {
                UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviour);
                
                if (programAsset.ScriptVersion < UdonSharpProgramVersion.V1SerializationUpdate)
                    continue;

                if (!UdonSharpEditorUtility.BehaviourNeedsSetup(behaviour))
                    continue;
                
                if (applyUpdates)
                {
                    UdonSharpUtils.LogWarning($"UdonBehaviour on {behaviour.gameObject} has not been fully setup, running setup.", behaviour);
                    UdonSharpEditorUtility.RunBehaviourSetup(behaviour);
                }
                
                neededUpdates = true;
            }
        }

        private static bool AreUdonSharpScriptsUpdated()
        {
            var allPrograms = UdonSharpProgramAsset.GetAllUdonSharpPrograms();

            foreach (var programAsset in allPrograms)
            {
                if (programAsset.ScriptVersion != UdonSharpProgramVersion.CurrentVersion)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Runs script, prefab, and scene upgrades. Then runs prefab/scene C#->Udon serialization conversion
        /// </summary>
        /// <returns>Returns if the build should be interrupted, usually due to some script upgrades needing to run first</returns>
        public static bool RunAllUpgrades()
        {
            if (UdonSharpUpgrader.UpgradeScripts())
                return true;

            UpgradeAssetsIfNeeded();
            
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                UdonSharpEditorUtility.UpgradeSceneBehaviours(GetAllUdonBehaviours());
            
            return false;
        }

        private class GameObjectPrefabMapping
        {
            private readonly GameObject _originalObject;
            private readonly GameObject _newObject;
            private Dictionary<Component, Component> _lazyComponentMap;

            public GameObjectPrefabMapping(GameObject originalObject, GameObject newObject)
            {
                _originalObject = originalObject;
                _newObject = newObject;
            }

            public Object GetRemappedObject(Object originalObject)
            {
                if (originalObject == null)
                    throw new NullReferenceException();

                if (originalObject is GameObject)
                {
                    if (originalObject != _originalObject)
                        throw new ArgumentException($"Incorrect prefab remapping used for given GameObject '{originalObject}', expected GameObject '{_originalObject}'");
                    
                    return _newObject;
                }

                // Component map hasn't been built, build it.
                if (_lazyComponentMap == null)
                {
                    Component[] originalComponents = _originalObject.GetComponents(typeof(Component));
                    Component[] newComponents = _newObject.GetComponents(typeof(Component));

                    if (originalComponents.Length != newComponents.Length)
                        throw new InvalidOperationException($"Component layout mismatch in prefab mapping {_originalObject} -> {_newObject}");

                    Dictionary<Component, Component> componentMap = new Dictionary<Component, Component>();

                    for (int i = 0; i < originalComponents.Length; ++i)
                    {
                        Component original = originalComponents[i];
                        Component newComponent = newComponents[i];
                        if (original.GetType() != newComponent.GetType())
                            throw new InvalidOperationException($"Component type mismatch in prefab mapping {original} -> {newComponent}");
                        
                        componentMap.Add(originalComponents[i], newComponents[i]);
                    }

                    _lazyComponentMap = componentMap;
                }

                return _lazyComponentMap[(Component)originalObject];
            }
        }

        private class PrefabMappingManager
        {
            private Dictionary<GameObject, GameObjectPrefabMapping> _prefabMappings =
                new Dictionary<GameObject, GameObjectPrefabMapping>();

            public bool TryRemapObject(Object originalObject, out Object newObject)
            {
                newObject = null;
                
                if (originalObject == null)
                    return false;

                switch (originalObject)
                {
                    case GameObject gameObject when _prefabMappings.ContainsKey(gameObject):
                        newObject = Remap(originalObject);
                        return true;
                    case Component component when _prefabMappings.ContainsKey(component.gameObject):
                        newObject = Remap(originalObject);
                        return true;
                    default:
                        return false;
                }
            }
            
            private Object Remap(Object originalObject)
            {
                if (originalObject is GameObject originalGameObject)
                    return _prefabMappings[originalGameObject].GetRemappedObject(originalObject);

                if (originalObject is Component originalComponent)
                    return _prefabMappings[originalComponent.gameObject].GetRemappedObject(originalComponent);

                throw new ArgumentException("'originalObject' must be either a GameObject or Component");
            }

            public void AddMappingForRoot(GameObject originalRoot, GameObject newRoot)
            {
                Transform originalTransform = originalRoot.transform;
                Transform newTransform = newRoot.transform;
                
                int originalChildCount = originalTransform.childCount;
                int newChildCount = newTransform.childCount;

                if (originalChildCount != newChildCount)
                    throw new InvalidOperationException($"Child count does not match on remapping {originalRoot} -> {newRoot}");
                
                _prefabMappings.Add(originalRoot, new GameObjectPrefabMapping(originalRoot, newRoot));

                for (int i = 0; i < originalChildCount; ++i)
                {
                    AddMappingForRoot(originalTransform.GetChild(i).gameObject, newTransform.GetChild(i).gameObject);
                }
            }
        }

        /// <summary>
        /// Copies all data from proxies into their associated UdonBehaviours and replaces prefab references with built prefabs that have updated data and optionally stripped components for build.
        /// </summary>
        /// <param name="rootBehaviours"></param>
        /// <param name="stripBehavioursForBuild">Whether to destroy UdonSharpBehaviour proxy components to prevent including them in builds redundantly</param>
        private static void PrepareUdonSharpBehavioursForPlay(IEnumerable<UdonBehaviour> rootBehaviours, bool stripBehavioursForBuild)
        {
            Stopwatch timer = Stopwatch.StartNew();

            rootBehaviours = rootBehaviours.ToArray();
            IEnumerable<GameObject> prefabRoots = CollectAllReferencedPrefabRoots(rootBehaviours);
            
            string prefabDataPath = UdonSharpLocator.IntermediatePrefabPath;
            
            PrefabMappingManager mappingManager = new PrefabMappingManager();

            GameObject instantiatedObjectRoot = new GameObject("__UdonSharpInstantiatedPrefabs");

            List<string> newPrefabRoots = new List<string>();
            
            try
            {
                List<(GameObject, GameObject, string)> prefabsToSave = new List<(GameObject, GameObject, string)>();
                
                // First pass over all prefabs referenced by scene objects
                foreach (GameObject prefabRoot in prefabRoots)
                {
                    if (!prefabRoot.GetComponentInChildren<UdonSharpBehaviour>(true))
                        continue;
                    
                    string originalPath = AssetDatabase.GetAssetPath(prefabRoot);

                    string newDataPath = Path.Combine(prefabDataPath, originalPath);

                    string dirPath = Path.GetDirectoryName(newDataPath);
                    if (!Directory.Exists(dirPath))
                        Directory.CreateDirectory(dirPath);

                    GameObject prefabInstance = Object.Instantiate(prefabRoot, instantiatedObjectRoot.transform);
                    
                    // Update the data on the U# behaviours
                    foreach (UdonSharpBehaviour behaviour in prefabInstance.GetComponentsInChildren<UdonSharpBehaviour>(true))
                    {
                        if (!UdonSharpEditorUtility.IsProxyBehaviour(behaviour))
                            continue;

                        UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
                        UdonSharpEditorUtility.ClearBehaviourVariables(backingBehaviour, true);
                        
                        UdonSharpEditorUtility.CopyProxyToUdon(behaviour, ProxySerializationPolicy.PreBuildSerialize);

                        SerializedObject behaviourObject = new SerializedObject(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));
                        SerializedProperty programAssetProperty = behaviourObject.FindProperty("serializedProgramAsset");

                        // Make sure the serialized asset reference is intact
                        if (programAssetProperty.objectReferenceValue == null)
                        {
                            programAssetProperty.objectReferenceValue = UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviour).SerializedProgramAsset;
                            behaviourObject.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                    
                    prefabsToSave.Add((prefabRoot, prefabInstance, newDataPath));
                    newPrefabRoots.Add(newDataPath);
                }

                // Delayed save so we can batch everything together and not spam the assetdatabase
                if (prefabsToSave.Count > 0)
                {
                    using (new AssetEditScope())
                    {
                        foreach ((_, GameObject prefabInstance, string newDataPath) in prefabsToSave)
                        {
                            PrefabUtility.SaveAsPrefabAsset(prefabInstance, newDataPath);
                        }
                    }

                    // Needs second pass since SaveAsPrefabAsset doesn't give a GameObject when you're using an edit scope.
                    foreach ((GameObject originalRoot, _, string newDataPath) in prefabsToSave)
                    {
                        GameObject newRoot = AssetDatabase.LoadAssetAtPath<GameObject>(newDataPath);
                        mappingManager.AddMappingForRoot(originalRoot, newRoot);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(instantiatedObjectRoot);
            }

            List<GameObject> prefabRootsToSave = new List<GameObject>();

            // Remap inter-prefab references and strip UdonSharpBehaviours from prefabs if needed
            foreach (string prefabRootPath in newPrefabRoots)
            {
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabRootPath);

                bool prefabModified = false;

                foreach (UdonBehaviour behaviour in prefabRoot.GetComponentsInChildren<UdonBehaviour>(true))
                {
                    List<Object> unityObjectList = (List<Object>)_serializedObjectReferencesField.GetValue(behaviour);

                    if (unityObjectList == null)
                        continue;

                    for (int i = 0; i < unityObjectList.Count; ++i)
                    {
                        if (mappingManager.TryRemapObject(unityObjectList[i], out var newObject))
                        {
                            unityObjectList[i] = newObject;
                            prefabModified = true;
                        }
                    }
                }

                if (stripBehavioursForBuild)
                {
                    foreach (UdonSharpBehaviour behaviour in prefabRoot.GetComponentsInChildren<UdonSharpBehaviour>(true))
                    {
                        Object.DestroyImmediate(behaviour, true);
                        prefabModified = true;
                    }
                }

                if (prefabModified)
                {
                    prefabRootsToSave.Add(prefabRoot);
                }
            }

            // Again, delayed save to batch
            if (prefabRootsToSave.Count > 0)
            {
                using (new AssetEditScope())
                {
                    foreach (GameObject rootToSave in prefabRootsToSave)
                    {
                        PrefabUtility.SavePrefabAsset(rootToSave);
                    }
                }
            }

            // Copy U# -> Udon for behaviours in scene
            foreach (UdonBehaviour rootBehaviour in rootBehaviours)
            {
                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(rootBehaviour))
                {
                    UdonSharpEditorUtility.ClearBehaviourVariables(rootBehaviour, true);
                    UdonSharpEditorUtility.CopyProxyToUdon(UdonSharpEditorUtility.GetProxyBehaviour(rootBehaviour), ProxySerializationPolicy.PreBuildSerialize);
                    _serializePublicVariablesMethod.Invoke(rootBehaviour, Array.Empty<object>());
                }
            }

            // Strip behaviours in scene
            if (stripBehavioursForBuild)
            {
                foreach (UdonBehaviour rootBehaviour in rootBehaviours)
                {
                    if (UdonSharpEditorUtility.IsUdonSharpBehaviour(rootBehaviour))
                    {
                        Object.DestroyImmediate(UdonSharpEditorUtility.GetProxyBehaviour(rootBehaviour));
                    }
                }
            }

            // Remap prefab references on behaviours in the scene
            foreach (UdonBehaviour rootBehaviour in rootBehaviours)
            {
                List<Object> unityObjectList = (List<Object>)_serializedObjectReferencesField.GetValue(rootBehaviour);

                if (unityObjectList == null)
                    continue;

                bool modifiedObject = false;

                for (int i = 0; i < unityObjectList.Count; ++i)
                {
                    if (mappingManager.TryRemapObject(unityObjectList[i], out Object newObject))
                    {
                        unityObjectList[i] = newObject;
                        modifiedObject = true;
                    }
                }

                if (modifiedObject)
                {
                    _deserializePublicVariablesMethod.Invoke(rootBehaviour, Array.Empty<object>());
                }
            }

            // UdonSharpUtils.Log($"Took {timer.Elapsed.TotalSeconds * 1000.0}ms to update UdonSharp scripts");
        }

        internal class AssetEditScope : IDisposable
        {
            public AssetEditScope()
            {
                AssetDatabase.StartAssetEditing();
            }
            
            public void Dispose()
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
    
    internal class UdonSharpPrefabPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            List<UdonBehaviour> behaviours = new List<UdonBehaviour>();
            
            foreach (string importedAsset in importedAssets)    
            {
                if (!importedAsset.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(importedAsset);
                
                prefabRoot.GetComponentsInChildren<UdonBehaviour>(true, behaviours);

                if (behaviours.Count == 0)
                    continue;

                bool needsUpdate = false;

                foreach (var behaviour in behaviours)
                {
                    if (!UdonSharpEditorUtility.IsUdonSharpBehaviour(behaviour))
                        continue;

                    if (UdonSharpEditorUtility.GetBehaviourVersion(behaviour) < UdonSharpBehaviourVersion.CurrentVersion)
                    {
                        needsUpdate = true;
                        break;
                    }
                }
                
                if (!needsUpdate)
                    continue;
                
                if (PrefabUtility.IsPartOfImmutablePrefab(prefabRoot))
                {
                    UdonSharpUtils.LogWarning($"Imported prefab with U# behaviour that needs update pass '{importedAsset}' is immutable");
                    continue;
                }

                UdonSharpEditorCache.Instance.QueueUpgradePass();
                break;
            }
        }
    }
}
