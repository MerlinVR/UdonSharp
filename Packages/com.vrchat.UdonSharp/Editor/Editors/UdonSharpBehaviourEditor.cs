
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UdonSharp;
using UdonSharp.Compiler.Symbols;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Udon;
using VRC.Udon.Editor;
using VRC.Udon.Serialization.OdinSerializer;
using Object = UnityEngine.Object;

#if ODIN_INSPECTOR_3
using UdonSharpEditor;
using Sirenix.OdinInspector.Editor;
[assembly: DefaultUdonSharpBehaviourEditor(typeof(OdinInspectorHandler), "Odin Inspector")]
#endif

/// <summary>
/// Example use of how to register a default inspector
/// </summary>
#if false
using UdonSharpEditor;

[assembly:DefaultUdonSharpBehaviourEditor(typeof(DemoDefaultBehaviourEditor), "UdonSharp Demo Inspector")]
#endif

namespace UdonSharpEditor
{
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class DefaultUdonSharpBehaviourEditorAttribute : Attribute
    {
        internal readonly Type inspectorType;
        internal readonly string inspectorDisplayName;

        public DefaultUdonSharpBehaviourEditorAttribute(Type inspectorType, string inspectorDisplayName)
        {
            this.inspectorType = inspectorType;
            this.inspectorDisplayName = inspectorDisplayName;
        }
    }

    /// <summary>
    /// Basic demo inspector that just draws fields using the Unity handling. Not intended to be used.
    /// </summary>
    internal class DemoDefaultBehaviourEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;

            base.OnInspectorGUI();
        }
    }

#if ODIN_INSPECTOR_3
    internal class OdinInspectorHandler : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            DrawTree();
        }
    }
#endif

    // [CustomEditor(typeof(UdonSharpBehaviour), true)]
    // [CanEditMultipleObjects]
    [CustomUdonBehaviourInspector(typeof(UdonSharpProgramAsset))]
    internal class UdonSharpBehaviourEditor : Editor
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
            
            string chosenFilePath = EditorUtility.SaveFilePanel("Save UdonSharp File", folderPath, "", "cs");

            if (chosenFilePath.Length > 0)
            {
                chosenFilePath = UdonSharpSettings.SanitizeScriptFilePath(chosenFilePath);
                string chosenFileName = Path.GetFileNameWithoutExtension(chosenFilePath).Replace(" ", "").Replace("#", "Sharp");
                string assetFilePath = Path.Combine(Path.GetDirectoryName(chosenFilePath), $"{chosenFileName}.asset");

                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetFilePath) != null)
                {
                    if (!EditorUtility.DisplayDialog("File already exists", $"Corresponding asset file '{assetFilePath}' already found for new UdonSharp script. Overwrite?", "Ok", "Cancel"))
                        return;
                }

                string fileContents = UdonSharpSettings.GetProgramTemplateString(chosenFileName);

                File.WriteAllText(chosenFilePath, fileContents, System.Text.Encoding.UTF8);

                AssetDatabase.ImportAsset(chosenFilePath, ImportAssetOptions.ForceSynchronousImport);
                MonoScript newScript = AssetDatabase.LoadAssetAtPath<MonoScript>(chosenFilePath);

                UdonSharpProgramAsset newProgramAsset = CreateInstance<UdonSharpProgramAsset>();
                newProgramAsset.sourceCsScript = newScript;

                AssetDatabase.CreateAsset(newProgramAsset, assetFilePath);

                AssetDatabase.Refresh();
            }
        }

        private enum BehaviourInspectorErrorState
        {
            Success,
            NoValidUSharpProgram,
            PrefabNeedsUpgrade,
        }

        private BehaviourInspectorErrorState errorState;

        public void OnEnable()
        {
            errorState = BehaviourInspectorErrorState.Success;
            bool needsUpgradePass = false;
            
            foreach (Object target in targets)
            {
                UdonBehaviour targetBehaviour = (UdonBehaviour)target;

                if (!UdonSharpEditorUtility.IsUdonSharpBehaviour(targetBehaviour))
                {
                    UdonSharpUtils.LogWarning($"UdonBehaviour '{targetBehaviour}' is not using a valid U# program asset", targetBehaviour);
                    errorState = BehaviourInspectorErrorState.NoValidUSharpProgram;
                    break;
                }

                if (UdonSharpEditorUtility.GetProxyBehaviour(targetBehaviour) != null) 
                    continue;
                
                needsUpgradePass = true;
                    
                if (PrefabUtility.IsPartOfPrefabInstance(targetBehaviour) &&
                    !PrefabUtility.IsAddedComponentOverride(targetBehaviour))
                {
                    UdonSharpUtils.LogWarning($"UdonBehaviour '{targetBehaviour}' needs upgrade on source prefab asset.", targetBehaviour);
                    errorState = BehaviourInspectorErrorState.PrefabNeedsUpgrade;
                    break;
                }
            }

            if (!needsUpgradePass || errorState != BehaviourInspectorErrorState.Success)
                return;

            foreach (Object target in targets)
            {
                UdonBehaviour targetBehaviour = (UdonBehaviour)target;
                UdonSharpBehaviour udonSharpBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(targetBehaviour);

                // Needs setup
                if (udonSharpBehaviour == null)
                {
                    // In particularly bad cases, Unity can drop the script data and make the U# behaviour effectively null temporarily. 
                    // We do not want to create additional components in this case since it's possible for users to recover the original state sometimes
                    // So we only run setup if the version has not already been set since UdonSharpBehaviours without versions can be created by populating a U# program asset on a UdonBehaviour
                    if (UdonSharpEditorUtility.GetBehaviourVersion(targetBehaviour) < UdonSharpBehaviourVersion.V1)
                    {
                        UdonSharpEditorUtility.SetIgnoreEvents(true);
                    
                        try
                        {
                            udonSharpBehaviour = (UdonSharpBehaviour)Undo.AddComponent(targetBehaviour.gameObject, UdonSharpEditorUtility.GetUdonSharpBehaviourType(targetBehaviour));

                            UdonSharpEditorUtility.SetBackingUdonBehaviour(udonSharpBehaviour, targetBehaviour);
                            UdonSharpEditorUtility.MoveComponentRelativeToComponent(udonSharpBehaviour, targetBehaviour, true);
                            UdonSharpEditorUtility.SetBehaviourVersion(targetBehaviour, UdonSharpBehaviourVersion.CurrentVersion);
                            UdonSharpEditorUtility.SetSceneBehaviourUpgraded(targetBehaviour);
                        }
                        finally
                        {
                            UdonSharpEditorUtility.SetIgnoreEvents(false);
                        }
                    }
                    else
                    {
                        UdonSharpUtils.LogWarning($"Inspected UdonBehaviour '{targetBehaviour.name}' does not have an associated U# behaviour, but has a version that indicates it should.", targetBehaviour);
                    }
                }
                
                if (udonSharpBehaviour != null)
                    udonSharpBehaviour.enabled = targetBehaviour.enabled;
                
            #if !UDONSHARP_DEBUG
                targetBehaviour.hideFlags = HideFlags.HideInInspector;
            #else
                targetBehaviour.hideFlags = HideFlags.None;
            #endif
            }
        }

        public override void OnInspectorGUI()
        {
            switch (errorState)
            {
                case BehaviourInspectorErrorState.Success:
                #if UDONSHARP_DEBUG
                    EditorGUILayout.HelpBox("UDONSHARP_DEBUG is defined; backing UdonBehaviour is shown", MessageType.Info);
                #else
                    EditorGUILayout.HelpBox("Something probably went wrong, you should not be able to see the underlying UdonBehaviour for UdonSharpBehaviours", MessageType.Warning);
                #endif
            
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("Linked U# Behaviour", UdonSharpEditorUtility.GetProxyBehaviour((UdonBehaviour)target), typeof(UdonSharpBehaviour), true);
                    EditorGUILayout.ObjectField("Program Source", ((UdonBehaviour)target).programSource, typeof(AbstractUdonProgramSource), false);
                    EditorGUI.EndDisabledGroup();
                    break;
                case BehaviourInspectorErrorState.PrefabNeedsUpgrade:
                    EditorGUILayout.HelpBox("U# behaviour needs upgrade on prefab, make sure you didn't have any issues during importing the prefab.", MessageType.Error);
                    break;
                case BehaviourInspectorErrorState.NoValidUSharpProgram:
                    EditorGUILayout.HelpBox("U# behaviour is not pointing to a valid U# program asset.", MessageType.Error);
                    
                    UdonSharpProgramAsset programAsset = ((UdonBehaviour)target).programSource as UdonSharpProgramAsset;

                    if (programAsset && programAsset.sourceCsScript == null)
                    {
                        UdonSharpGUI.DrawCreateScriptButton(programAsset);
                    }
                    break;
            }
        }
    }

#region Drawer override boilerplate
    [InitializeOnLoad]
    internal class UdonBehaviourDrawerOverride
    {
        static UdonBehaviourDrawerOverride()
        {
            OverrideUdonBehaviourDrawer();
        }

        /// <summary>
        /// Handles removing the reference to the default UdonBehaviourEditor and injecting our own custom editor UdonBehaviourOverrideEditor
        /// </summary>
        internal static void OverrideUdonBehaviourDrawer() 
        {
            Type editorAttributesClass = typeof(Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
            
            FieldInfo initializedField = editorAttributesClass.GetField("s_Initialized", BindingFlags.Static | BindingFlags.NonPublic);

            if (!(bool)initializedField.GetValue(null))
            {
                MethodInfo rebuildMethod = editorAttributesClass.GetMethod("Rebuild", BindingFlags.Static | BindingFlags.NonPublic);

                rebuildMethod.Invoke(null, null);

                initializedField.SetValue(null, true);
            }
            
            FieldInfo customEditorField = editorAttributesClass.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo customMultiEditorField = editorAttributesClass.GetField("kSCustomMultiEditors", BindingFlags.NonPublic | BindingFlags.Static);

            object customEditorDictionary = customEditorField.GetValue(null);
            object customMultiEditorDictionary = customMultiEditorField.GetValue(null);

            Type fieldType = customEditorField.FieldType;

            MethodInfo removeTypeMethod = fieldType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                   .First(e => e.Name == "Remove" &&
                                                          e.GetParameters().Length == 1 &&
                                                          e.GetParameters()[0].ParameterType == typeof(Type));

            Type monoEditorTypeType = editorAttributesClass.GetNestedType("MonoEditorType", BindingFlags.NonPublic);
            FieldInfo monoEditorTypeInspectedTypeField = monoEditorTypeType.GetField("m_InspectedType", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo monoEditorTypeInspectorTypeField = monoEditorTypeType.GetField("m_InspectorType", BindingFlags.Public | BindingFlags.Instance);

            Type monoEditorTypeListType = typeof(List<>).MakeGenericType(monoEditorTypeType);


            MethodInfo addTypeMethod = fieldType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                .First(e => e.Name == "Add" && 
                                                       e.GetParameters().Length == 2 &&
                                                       e.GetParameters()[0].ParameterType == typeof(Type) &&
                                                       e.GetParameters()[1].ParameterType == monoEditorTypeListType);

            MethodInfo listAddTypeMethod = monoEditorTypeListType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                                 .First(e => e.Name == "Add" &&
                                                                        e.GetParameters().Length == 1 &&
                                                                        e.GetParameters()[0].ParameterType == monoEditorTypeType);

            IEnumerable<Type> inspectedTypes = UdonSharpCustomEditorManager.GetInspectedTypes();

            foreach (Type inspectedType in inspectedTypes)
            {
                if (!inspectedType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                    continue;
                
                object editorTypeList = Activator.CreateInstance(monoEditorTypeListType);

                object editorTypeObject = Activator.CreateInstance(monoEditorTypeType);
            
                monoEditorTypeInspectedTypeField.SetValue(editorTypeObject, inspectedType);
                monoEditorTypeInspectorTypeField.SetValue(editorTypeObject, typeof(UdonSharpBehaviourOverrideEditor));
            
                listAddTypeMethod.Invoke(editorTypeList, new [] {editorTypeObject});

                removeTypeMethod.Invoke(customEditorDictionary, new object[] { inspectedType });

                addTypeMethod.Invoke(customEditorDictionary, new [] { inspectedType, editorTypeList });

                removeTypeMethod.Invoke(customMultiEditorDictionary, new object[] { inspectedType });
                addTypeMethod.Invoke(customMultiEditorDictionary, new [] { inspectedType, editorTypeList });
            }
        }
    }
#endregion

#region Editor Manager
    internal static class UdonSharpCustomEditorManager
    {
        private static Dictionary<Type, Type> _typeInspectorMap;
        private static Dictionary<string, (string, Type)> _defaultInspectorMap;

        internal static Dictionary<string, (string, Type)> DefaultInspectorMap
        {
            get
            {
                InitInspectorMap();
                return _defaultInspectorMap;
            }
        }

        private static bool _initialized; 

        private static void InitInspectorMap()
        {
            if (_initialized)
                return;
            
            _typeInspectorMap = new Dictionary<Type, Type>();
            _defaultInspectorMap = new Dictionary<string, (string, Type)>();

            FieldInfo inspectedTypeField = typeof(CustomEditor).GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo inspectsChildClassesField = typeof(CustomEditor).GetField("m_EditorForChildClasses", BindingFlags.NonPublic | BindingFlags.Instance);

            TypeCache.TypeCollection types = TypeCache.GetTypesWithAttribute<CustomEditor>();

            foreach (Type editorType in types)
            {
                IEnumerable<CustomEditor> editorAttributes = editorType.GetCustomAttributes<CustomEditor>();

                foreach (CustomEditor editorAttribute in editorAttributes)
                {
                    if (editorAttribute == null || editorAttribute.GetType() != typeof(CustomEditor)) // The CustomEditorForRenderPipeline attribute inherits from CustomEditor, but we do not want to take that into account.
                        continue;
                    
                    Type inspectedType = (Type)inspectedTypeField.GetValue(editorAttribute);

                    if (!inspectedType.IsSubclassOf(typeof(UdonSharpBehaviour))) 
                        continue;
                        
                    if (_typeInspectorMap.ContainsKey(inspectedType))
                    {
                        UdonSharpUtils.LogError($"Cannot register inspector '{editorType.Name}' for type '{inspectedType.Name}' since inspector '{_typeInspectorMap[inspectedType].Name}' is already registered");
                        continue;
                    }
                    
                    _typeInspectorMap.Add(inspectedType, editorType);
                }
            }

            foreach (Type udonSharpBehaviourType in TypeCache.GetTypesDerivedFrom<UdonSharpBehaviour>())
            {
                if (_typeInspectorMap.ContainsKey(udonSharpBehaviourType)) 
                    continue;
                
                Type currentType = udonSharpBehaviourType.BaseType;

                bool foundType = false;
                while (currentType != null && currentType != typeof(UdonSharpBehaviour))
                {
                    if (_typeInspectorMap.TryGetValue(currentType, out Type foundInspectorType) &&
                        foundInspectorType.GetCustomAttribute<CustomEditor>() != null &&
                        (bool)inspectsChildClassesField.GetValue(foundInspectorType.GetCustomAttribute<CustomEditor>()))
                    {
                        _typeInspectorMap.Add(udonSharpBehaviourType, foundInspectorType);
                        foundType = true;
                        break;
                    }
                        
                    currentType = currentType.BaseType;
                }

                if (!foundType)
                {
                    _typeInspectorMap.Add(udonSharpBehaviourType, typeof(UdonSharpBehaviourOverrideEditor));
                }
            }

            foreach (Assembly asm in UdonSharpUtils.GetLoadedEditorAssemblies())
            {
                foreach (DefaultUdonSharpBehaviourEditorAttribute editorAttribute in asm.GetCustomAttributes<DefaultUdonSharpBehaviourEditorAttribute>())
                {
                    if (!editorAttribute.inspectorType.IsSubclassOf(typeof(Editor)))
                    {
                        UdonSharpUtils.LogError($"Could not add default inspector '{editorAttribute.inspectorType}', custom inspectors must inherit from UnityEditor.Editor");
                        continue;
                    }

                    _defaultInspectorMap.Add(editorAttribute.inspectorType.ToString(), (editorAttribute.inspectorDisplayName, editorAttribute.inspectorType));
                }
            }

            _initialized = true;
        }

        public static IEnumerable<Type> GetInspectedTypes()
        {
            InitInspectorMap();

            return _typeInspectorMap.Keys;
        }

        public static Type GetInspectorEditorType(Type udonSharpBehaviourType)
        {
            InitInspectorMap();
            
            _typeInspectorMap.TryGetValue(udonSharpBehaviourType, out Type editorType);

            // Fall through and check for a default editor if no inspector is specified on the behaviour
            if (editorType != null && editorType != typeof(UdonSharpBehaviourOverrideEditor)) 
                return editorType;
            
            UdonSharpSettings settings = UdonSharpSettings.GetSettings();

            string defaultEditor = settings.defaultBehaviourInterfaceType;

            if (!string.IsNullOrEmpty(defaultEditor))
            {
                if (_defaultInspectorMap.TryGetValue(defaultEditor, out var defaultEditorType))
                {
                    editorType = defaultEditorType.Item2;
                }
            }

            return editorType;
        }
    }
#endregion

    /// <summary>
    /// Custom U# editor for UdonSharpBehaviours that has custom behavior for drawing stuff like sync position and the program asset info
    /// Also allows people to override the inspector for their own custom inspectors
    /// </summary>
    internal class UdonSharpBehaviourOverrideEditor : Editor
    {
        private Editor _userEditor;

        private void OnEnable()
        {
            if (EditorApplication.isPlaying)
            {
                foreach (Object targetBehaviour in targets)
                {
                    UdonSharpBehaviour proxy = (UdonSharpBehaviour)targetBehaviour;
                    UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);

                    if (proxy.enabled != backingBehaviour.enabled)
                        proxy.enabled = backingBehaviour.enabled;
                }
            }
            
            foreach (Object targetBehaviour in targets)
                UdonSharpEditorUtility.RunBehaviourSetupWithUndo((UdonSharpBehaviour)targetBehaviour);

            if (_userEditor != null) 
                return;
            
            Type userType = target.GetType();

            Type customEditorType = UdonSharpCustomEditorManager.GetInspectorEditorType(userType);

            if (customEditorType != null && customEditorType != typeof(UdonSharpBehaviourOverrideEditor))
                _userEditor = CreateEditorWithContext(targets, this, customEditorType);
        }

        private void OnDisable()
        {
            CleanupEditor();
        }

        private void OnDestroy()
        {
            CleanupEditor();
            CleanupBackingBehaviours();
        }

        private void CleanupEditor()
        {
            if (_userEditor)
            {
                DestroyImmediate(_userEditor);
                _userEditor = null;
            }
        }

        /// <summary>
        /// Cleans up the backing UdonBehaviour when the UdonSharpBehaviour has been deleted by the user from the UI Remove Component
        /// </summary>
        private void CleanupBackingBehaviours()
        {
            foreach (Object target in targets)
            {
                // Check if the target has been marked destroyed
                if (!ReferenceEquals(target, null) && target == null)
                {
                    UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)target);

                    if (backingBehaviour != null)
                    {
                        Undo.DestroyObjectImmediate(backingBehaviour);
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            if (!_userEditor)
                return;
            
            Type customEditorType = _userEditor.GetType();
        
            MethodInfo onSceneGUIMethod = customEditorType?.GetMethod("OnSceneGUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
        
            if (onSceneGUIMethod == null)
                return;
        
            _userEditor.serializedObject.Update();
        
            onSceneGUIMethod.Invoke(_userEditor, null);
        }

        private const string BAR_NAME = "UdonSharpScriptBar";
        private VisualElement _rootInspectorElement;
        private VisualElement _userInspectorElement;
        
        public override VisualElement CreateInspectorGUI()
        {
            if (_rootInspectorElement != null)
                return _rootInspectorElement;
            
            _rootInspectorElement = new VisualElement();
            _rootInspectorElement.name = "UdonSharpInspectorRoot";

            RebuildUserInspector();
            
            _rootInspectorElement.Add(_userInspectorElement);
            
            // Create the U# highlight bar on the left
            // A little jank atm, can bleed into some inspectors like the material inspector that Unity adds in some cases
            _rootInspectorElement.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                VisualElement editorElement = _rootInspectorElement.GetFirstAncestorOfType<InspectorElement>()?.parent;

                if (editorElement == null)
                    return;
                
                VisualElement bar = new VisualElement
                {
                    name = BAR_NAME,
                    style =
                    {
                        top = 2,
                        bottom = 0,
                        left = 0,
                        width = 3,
                        backgroundColor = (Color)new Color32(139, 127, 198, 220),
                        position = Position.Absolute
                    }
                };

                editorElement.style.paddingLeft = 4;

                List<VisualElement> removeList = new List<VisualElement>();

                foreach (VisualElement child in editorElement.Children())
                {
                    if (child.name == BAR_NAME)
                        removeList.Add(child);
                }

                foreach (VisualElement element in removeList)
                {
                    editorElement.Remove(element);
                }
        
                editorElement.Add(bar);
            });
            
            _rootInspectorElement.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                VisualElement editorElement = _rootInspectorElement.GetFirstAncestorOfType<InspectorElement>()?.parent;

                if (editorElement == null)
                    return;

                List<VisualElement> removeList = new List<VisualElement>();

                foreach (VisualElement child in editorElement.Children())
                {
                    if (child.name == BAR_NAME)
                        removeList.Add(child);
                }

                foreach (VisualElement element in removeList)
                {
                    editorElement.Remove(element);
                }
            });

            return _rootInspectorElement;
        }

        private void RebuildUserInspector()
        {
            VisualElement originalParent = _userInspectorElement?.parent;
            
            originalParent?.Remove(_userInspectorElement);
            _userInspectorElement = CreateCustomInspectorElement();
            originalParent?.Add(_userInspectorElement);
        }

        private VisualElement CreateCustomInspectorElement()
        {
            if (targets.Any(e => e == null || UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)e) == null))
            {
                return CreateIMGUIInspector(() =>
                {
                    EditorGUILayout.HelpBox("Selected U# behaviour is not setup, try reloading the scene.", MessageType.Error);
                }, true);
            }
            if (targets.Any(e => UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)e).programSource == null))
            {
                return CreateIMGUIInspector(() =>
                {
                    EditorGUILayout.HelpBox("Selected U# behaviour program source reference is not valid.", MessageType.Warning);
                    
                    UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset((UdonSharpBehaviour)target);

                    if (programAsset && 
                        programAsset.sourceCsScript == null &&
                        UdonSharpGUI.DrawCreateScriptButton(programAsset))
                    {
                        RebuildUserInspector();
                    }
                }, true);
            }

            if (!_userEditor) 
                return CreateDefaultUdonSharpInspectorElement();

            if (targets.Length > 1 && !_userEditor.GetType().IsDefined(typeof(CanEditMultipleObjects), false))
            {
                return CreateIMGUIInspector(() =>
                {
                    EditorGUILayout.HelpBox("Multi-object editing not supported.", MessageType.None);
                }, true);
            }
            
            VisualElement userEditorElement = _userEditor.CreateInspectorGUI();

            if (userEditorElement != null)
                return userEditorElement;

            return CreateIMGUIInspector(() =>
            {
                _userEditor.OnInspectorGUI();
            }, false, _userEditor.UseDefaultMargins());
        }

        private static readonly PropertyInfo _contextWidthProperty = typeof(EditorGUIUtility).GetProperty("contextWidth", BindingFlags.Static | BindingFlags.NonPublic);
        
        // Draws the blue bar on added component overrides
        private static readonly MethodInfo _drawAddedComponentMethod = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindowUtils").GetMethod("DrawAddedComponentBackground", BindingFlags.Static | BindingFlags.Public);
        
        private int _nullCounter;

        private IMGUIContainer CreateIMGUIInspector(Action imguiAction, bool skipSerialize = false, bool defaultMargins = true)
        {
            IMGUIContainer container = new IMGUIContainer();
            
            container.style.overflow = Overflow.Visible;
            
            container.onGUIHandler = () =>
            {
                if (targets.Any(e => e == null))
                {
                    if (_nullCounter++ > 5)
                        EditorGUILayout.HelpBox("This inspector is inspecting null behaviours!", MessageType.Error);
                        
                    return;
                }

                bool isAnimating = AnimationMode.InAnimationMode();
                
                if (isAnimating)
                    EditorGUILayout.HelpBox("U# Behaviours cannot be animated, disable animation recording/preview to edit fields.", MessageType.Error);
                
                bool previousHierarchyMode = EditorGUIUtility.hierarchyMode;
                bool previousWideMode = EditorGUIUtility.wideMode;
                
                EditorGUIUtility.hierarchyMode = true;
                EditorGUIUtility.wideMode = (float)_contextWidthProperty.GetValue(null) > 330f;
                GUI.changed = false;
                
                _drawAddedComponentMethod.Invoke(null, new object[] { container.contentRect, targets, 0f });
                
                EditorGUI.BeginDisabledGroup(isAnimating);
                
                EditorGUILayout.BeginVertical(defaultMargins ? EditorStyles.inspectorDefaultMargins : GUIStyle.none);
                
                try
                {
                    if (!skipSerialize && EditorApplication.isPlaying) // We only need this copy in play mode since U# now goes off the behaviour data for setting up UdonBehaviours
                    {
                        foreach (Object targetProxy in targets)
                            UdonSharpEditorUtility.CopyUdonToProxy((UdonSharpBehaviour)targetProxy, ProxySerializationPolicy.All);
                    }
                
                    if (_userEditor)
                        _userEditor.serializedObject.Update();

                    imguiAction();

                    if (!skipSerialize && EditorApplication.isPlaying)
                    {
                        foreach (Object targetProxy in targets)
                            UdonSharpEditorUtility.CopyProxyToUdon((UdonSharpBehaviour)targetProxy, ProxySerializationPolicy.All);
                    }
                }
                finally
                {
                    EditorGUILayout.EndVertical();
                    
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUIUtility.wideMode = previousWideMode;
                    EditorGUIUtility.hierarchyMode = previousHierarchyMode;
                }
            };

            return container;
        }

        private static readonly GUIContent _jaggedArrayHeader = new GUIContent("Jagged Arrays", "Fallback inspector handling for jagged arrays since Unity does not handle serializing them.");
        
        private VisualElement CreateDefaultUdonSharpInspectorElement()
        {
            return CreateIMGUIInspector(() =>
            {
                UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets);

                SerializedProperty fieldProp = serializedObject.GetIterator();
                if (fieldProp.NextVisible(true))
                {
                    do
                    {
                        if (fieldProp.propertyPath == "m_Script")
                            continue;

                        EditorGUILayout.PropertyField(fieldProp, true);

                    } while (fieldProp.NextVisible(false));
                }

            #if UDONSHARP_DEBUG
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(UdonSharpEditorUtility.BackingFieldName));
                EditorGUI.EndDisabledGroup();
            #endif

                serializedObject.ApplyModifiedProperties();

                // Fallback handling for jagged array drawer
                List<FieldInfo> jaggedArrayFields = null;

                foreach (FieldInfo field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (UdonSharpUtils.IsUserJaggedArray(field.FieldType) && 
                        field.IsDefined(typeof(OdinSerializeAttribute)) && // We only want Odin serialized fields since other jagged arrays will not be saved
                        !field.IsDefined(typeof(HideInInspector)))
                    {
                        if (jaggedArrayFields == null) 
                            jaggedArrayFields = new List<FieldInfo>();
                        
                        jaggedArrayFields.Add(field);
                    }
                }

                if (jaggedArrayFields == null)
                    return;

                bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(target);
                
                // Unity will not record changes to instances even if I force Odin to serialize so :shrug:
                EditorGUI.BeginDisabledGroup(isPrefab);

                EditorGUILayout.Space();
                    
                EditorGUILayout.LabelField(_jaggedArrayHeader, EditorStyles.boldLabel);

                if (targets.Length > 1)
                {
                    EditorGUILayout.HelpBox("Multi-edit is not supported on jagged array drawers", MessageType.None);
                    return;
                }

                if (isPrefab)
                    EditorGUILayout.HelpBox("Cannot edit jagged arrays on prefab instances", MessageType.None);

                bool dirtied = false;
                
                foreach (FieldInfo field in jaggedArrayFields)
                {
                    EditorGUI.BeginChangeCheck();
                    object newArrayVal = UdonSharpGUI.DrawFieldForType(target, field.Name, field.Name, field.GetValue(target), field.FieldType, field);

                    if (EditorGUI.EndChangeCheck())
                    {
                        dirtied = true;
                        field.SetValue(target, newArrayVal);
                    }
                }

                if (dirtied)
                {
                    UdonSharpUtils.SetDirty(target);
                }
                
                EditorGUI.EndDisabledGroup();
            });
        }

        public override bool RequiresConstantRepaint()
        {
            if (!_userEditor)
                return EditorApplication.isPlaying;

            return _userEditor.RequiresConstantRepaint();
        }

        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}
