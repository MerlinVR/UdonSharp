
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor;

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
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class DefaultUdonSharpBehaviourEditorAttribute : Attribute
    {
        internal System.Type inspectorType;
        internal string inspectorDisplayName;

        public DefaultUdonSharpBehaviourEditorAttribute(System.Type inspectorType, string inspectorDisplayName)
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

    [CustomEditor(typeof(UdonSharpBehaviour), true)]
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
            
            string chosenFilePath = EditorUtility.SaveFilePanelInProject("Save UdonSharp File", "", "cs", "Save UdonSharp file", folderPath);

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

                UdonSharpProgramAsset newProgramAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                newProgramAsset.sourceCsScript = newScript;

                AssetDatabase.CreateAsset(newProgramAsset, assetFilePath);

                AssetDatabase.Refresh();
            }
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(targets))
                return;

            base.OnInspectorGUI();
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

        static FieldInfo customEditorField;
        static MethodInfo removeTypeMethod;
        static MethodInfo addTypeMethod;

        static System.Type monoEditorTypeType;
        static System.Type monoEditorTypeListType;
        static MethodInfo listAddTypeMethod;
        static MethodInfo listClearMethod;
        static FieldInfo monoEditorTypeInspectedTypeField;
        static FieldInfo monoEditorTypeInspectorTypeField;

        static readonly object[] udonBehaviourTypeArr = new object[] { typeof(UdonBehaviour) };
        static readonly object[] addTypeInvokeParams = new object[] { typeof(UdonBehaviour), null };
        static readonly object[] listCreateParams = new object[] { 1 };

        static object customEditorDictionary;
        static object editorTypeList;
        static object editorTypeObject;

        /// <summary>
        /// Handles removing the reference to the default UdonBehaviourEditor and injecting our own custom editor UdonBehaviourOverrideEditor
        /// </summary>
        public static void OverrideUdonBehaviourDrawer() 
        {
        #if !UNITY_2019_4_OR_NEWER
            if (customEditorField == null)
        #endif
            {
                Assembly editorAssembly = typeof(UnityEditor.Editor).Assembly;

                System.Type editorAttributesClass = editorAssembly.GetType("UnityEditor.CustomEditorAttributes");
                customEditorField = editorAttributesClass.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static);

                System.Type fieldType = customEditorField.FieldType;

                removeTypeMethod = fieldType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                            .FirstOrDefault(e => e.Name == "Remove" &&
                                                                 e.GetParameters().Length == 1 &&
                                                                 e.GetParameters()[0].ParameterType == typeof(System.Type));

                monoEditorTypeType = editorAttributesClass.GetNestedType("MonoEditorType", BindingFlags.NonPublic);
                monoEditorTypeInspectedTypeField = monoEditorTypeType.GetField("m_InspectedType", BindingFlags.Public | BindingFlags.Instance);
                monoEditorTypeInspectorTypeField = monoEditorTypeType.GetField("m_InspectorType", BindingFlags.Public | BindingFlags.Instance);

                monoEditorTypeListType = typeof(List<>).MakeGenericType(monoEditorTypeType);


                addTypeMethod = fieldType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                         .FirstOrDefault(e => e.Name == "Add" &&
                                                              e.GetParameters().Length == 2 &&
                                                              e.GetParameters()[0].ParameterType == typeof(System.Type) &&
                                                              e.GetParameters()[1].ParameterType == monoEditorTypeListType);

                listAddTypeMethod = monoEditorTypeListType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                          .FirstOrDefault(e => e.Name == "Add" &&
                                                                               e.GetParameters().Length == 1 &&
                                                                               e.GetParameters()[0].ParameterType == monoEditorTypeType);

                listClearMethod = monoEditorTypeListType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                        .FirstOrDefault(e => e.Name == "Clear" &&
                                                                             e.GetParameters().Length == 0);

                customEditorDictionary = customEditorField.GetValue(null);

                editorTypeObject = Activator.CreateInstance(monoEditorTypeType);
                monoEditorTypeInspectedTypeField.SetValue(editorTypeObject, typeof(UdonBehaviour));
                monoEditorTypeInspectorTypeField.SetValue(editorTypeObject, typeof(UdonBehaviourOverrideEditor));

                editorTypeList = Activator.CreateInstance(monoEditorTypeListType);

                listCreateParams[0] = editorTypeObject;
                
            #if UNITY_2019_4_OR_NEWER
                FieldInfo initializedField = editorAttributesClass.GetField("s_Initialized", BindingFlags.Static | BindingFlags.NonPublic);

                if (!(bool) initializedField.GetValue(null))
                {
                    MethodInfo rebuildMethod =
                        editorAttributesClass.GetMethod("Rebuild", BindingFlags.Static | BindingFlags.NonPublic);

                    rebuildMethod.Invoke(null, null);

                    initializedField.SetValue(null, true);
                }
            #endif
            }
            

            listClearMethod.Invoke(editorTypeList, null);
            listAddTypeMethod.Invoke(editorTypeList, listCreateParams);

            removeTypeMethod.Invoke(customEditorDictionary, udonBehaviourTypeArr);

            addTypeInvokeParams[1] = editorTypeList;
            addTypeMethod.Invoke(customEditorDictionary, addTypeInvokeParams);
        }
    }
#endregion

#region Editor Manager
    [InitializeOnLoad]
    internal static class UdonSharpCustomEditorManager
    {
        static Dictionary<System.Type, System.Type> _typeInspectorMap;
        internal static Dictionary<string, (string, Type)> _defaultInspectorMap;

        static UdonSharpCustomEditorManager()
        {
            InitInspectorMap();
            Undo.postprocessModifications += OnPostprocessUndoModifications;
        }

        static void InitInspectorMap()
        {
            _typeInspectorMap = new Dictionary<Type, Type>();
            _defaultInspectorMap = new Dictionary<string, (string, Type)>();

            FieldInfo inspectedTypeField = typeof(CustomEditor).GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (Assembly asm in UdonSharpUtils.GetLoadedEditorAssemblies())
            {
                foreach (Type editorType in asm.GetTypes())
                {
                    IEnumerable<CustomEditor> editorAttributes = editorType.GetCustomAttributes<CustomEditor>();

                    foreach (CustomEditor editorAttribute in editorAttributes)
                    {
                        if (editorAttribute != null && editorAttribute.GetType() == typeof(CustomEditor)) // The CustomEditorForRenderPipeline attribute inherits from CustomEditor, but we do not want to take that into account.
                        {
                            Type inspectedType = (Type)inspectedTypeField.GetValue(editorAttribute);

                            if (inspectedType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                            {
                                if (_typeInspectorMap.ContainsKey(inspectedType))
                                {
                                    Debug.LogError($"Cannot register inspector '{editorType.Name}' for type '{inspectedType.Name}' since inspector '{_typeInspectorMap[inspectedType].Name}' is already registered");
                                    continue;
                                }

                                _typeInspectorMap.Add(inspectedType, editorType);
                            }
                        }
                    }
                }

                foreach (DefaultUdonSharpBehaviourEditorAttribute editorAttribute in asm.GetCustomAttributes<DefaultUdonSharpBehaviourEditorAttribute>())
                {
                    if (!editorAttribute.inspectorType.IsSubclassOf(typeof(Editor)))
                    {
                        Debug.LogError($"Could not add default inspector '{editorAttribute.inspectorType}', custom inspectors must inherit from UnityEditor.Editor");
                        continue;
                    }

                    _defaultInspectorMap.Add(editorAttribute.inspectorType.ToString(), (editorAttribute.inspectorDisplayName, editorAttribute.inspectorType));
                }
            }
        }

        /// <summary>
        /// Dirties the underlying UdonBehaviour when a proxy UdonSharpBehaviour is modified since Unity does not mark the scene dirty when a modified object is marked 'HideFlags.DontSaveInEditor'
        /// Has the downside that the scene will not be "undirtied" when a change that dirtied it is undone.
        /// </summary>
        /// <param name="propertyModifications"></param>
        /// <returns></returns>
        static UndoPropertyModification[] OnPostprocessUndoModifications(UndoPropertyModification[] propertyModifications)
        {
            if (!EditorApplication.isPlaying)
            {
                HashSet<UdonBehaviour> modifiedBehaviours = new HashSet<UdonBehaviour>();

                foreach (UndoPropertyModification propertyModification in propertyModifications)
                {
                    UnityEngine.Object target = propertyModification.currentValue?.target;

                    if (target != null && target is UdonSharpBehaviour udonSharpBehaviour)
                    {
                        UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(udonSharpBehaviour);

                        if (backingBehaviour)
                        {
                            modifiedBehaviours.Add(backingBehaviour);
                        }
                    }
                }

                if (modifiedBehaviours.Count > 0)
                {
                    foreach (UdonBehaviour behaviour in modifiedBehaviours)
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(behaviour))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                    }

                    EditorSceneManager.MarkAllScenesDirty();
                }
            }

            return propertyModifications;
        }

        public static System.Type GetInspectorEditorType(System.Type udonSharpBehaviourType)
        {
            System.Type editorType;
            _typeInspectorMap.TryGetValue(udonSharpBehaviourType, out editorType);

            if (editorType == null)
            {
                UdonSharpSettings settings = UdonSharpSettings.GetSettings();

                if (settings)
                {
                    string defaultEditor = settings.defaultBehaviourInterfaceType;

                    if (!string.IsNullOrEmpty(defaultEditor))
                    {
                        if (_defaultInspectorMap.TryGetValue(defaultEditor, out var defaultEditorType))
                        {
                            editorType = defaultEditorType.Item2;
                        }
                    }
                }
            }

            return editorType;
        }
    }
#endregion

    /// <summary>
    /// Custom U# editor for UdonBehaviours that can have custom behavior for drawing stuff like sync position and the program asset info
    /// Will also allow people to override the inspector for their own custom inspectors
    /// </summary>
    internal class UdonBehaviourOverrideEditor : Editor
    {
        Editor baseEditor;
        UdonSharpBehaviour currentProxyBehaviour;

        private void OnEnable()
        {
            UdonEditorManager.Instance.WantRepaint += Repaint;

            if (target && PrefabUtility.IsPartOfPrefabAsset(target))
                return;

            Undo.undoRedoPerformed += OnUndoRedo;

            if (target is UdonBehaviour udonBehaviour && UdonSharpEditorUtility.IsUdonSharpBehaviour(udonBehaviour))
            {
                UdonSharpBehaviour proxyBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour, ProxySerializationPolicy.NoSerialization);

                if (proxyBehaviour)
                    proxyBehaviour.hideFlags =
                        HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                        HideFlags.HideInInspector |
#endif
                        HideFlags.DontSaveInEditor;
            }
        }

        private void OnDisable()
        {
            UdonEditorManager.Instance.WantRepaint -= Repaint;

            if (target && PrefabUtility.IsPartOfPrefabAsset(target))
                return;

            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            UdonBehaviour behaviour = target as UdonBehaviour;
            UdonSharpBehaviour inspectorTarget = UdonSharpEditorUtility.FindProxyBehaviour(behaviour, ProxySerializationPolicy.NoSerialization);

            if (inspectorTarget)
            {
                System.Type customEditorType = UdonSharpCustomEditorManager.GetInspectorEditorType(inspectorTarget.GetType());

                if (customEditorType != null) // Only do the undo copying on things with a custom inspector
                {
                    if ((bool)typeof(UdonSharpBehaviour).GetField("_isValidForAutoCopy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inspectorTarget))
                    {
                        UdonSharpEditorUtility.CopyProxyToUdon(inspectorTarget, ProxySerializationPolicy.All);

                        if (PrefabUtility.IsPartOfPrefabInstance(behaviour))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (baseEditor)
                DestroyImmediate(baseEditor);

            CleanupProxy();
        }

        void CleanupProxy()
        {
            if (currentProxyBehaviour != null)
            {
                UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(currentProxyBehaviour);

                if (backingBehaviour == null)
                {
                    UdonSharpEditorUtility.SetIgnoreEvents(true);

                    try
                    {
                        UnityEngine.Object.DestroyImmediate(currentProxyBehaviour);
                    }
                    finally
                    {
                        UdonSharpEditorUtility.SetIgnoreEvents(false);
                    }
                }
            }
        }
        
        static FieldInfo _autoCopyValidField = null;

        public override void OnInspectorGUI()
        {
            UdonBehaviour behaviour = target as UdonBehaviour;

            // Fall back to the default Udon inspector if not a U# behaviour
            if (behaviour.programSource == null || !(behaviour.programSource is UdonSharpProgramAsset udonSharpProgram))
            {
                if (!baseEditor)
                    Editor.CreateCachedEditorWithContext(targets, this, typeof(UdonBehaviourEditor), ref baseEditor);

                baseEditor.OnInspectorGUI();
                return;
            }

            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)behaviour.programSource;
            programAsset.UpdateProgram();

            System.Type customEditorType = null;
            System.Type inspectedType = programAsset.GetClass();

            if (inspectedType != null)
                customEditorType = UdonSharpCustomEditorManager.GetInspectorEditorType(inspectedType);

            if (customEditorType != null && !PrefabUtility.IsPartOfPrefabAsset(target))
            {
                if (baseEditor != null && baseEditor.GetType() != customEditorType)
                {
                    DestroyImmediate(baseEditor);
                    baseEditor = null;
                }

                UdonSharpBehaviour inspectorTarget = UdonSharpEditorUtility.GetProxyBehaviour(behaviour, ProxySerializationPolicy.All);
                inspectorTarget.enabled = false;

                if (_autoCopyValidField == null)
                    _autoCopyValidField = typeof(UdonSharpBehaviour).GetField("_isValidForAutoCopy", BindingFlags.NonPublic | BindingFlags.Instance);

                _autoCopyValidField.SetValue(inspectorTarget, true);

                Editor.CreateCachedEditorWithContext(inspectorTarget, this, customEditorType, ref baseEditor);
                currentProxyBehaviour = inspectorTarget;

                baseEditor.serializedObject.Update();
                
                baseEditor.OnInspectorGUI();

                UdonSharpEditorUtility.CopyProxyToUdon(inspectorTarget, ProxySerializationPolicy.All);
            }
            else
            {
                // Create a proxy behaviour so that other things can find this object
                if (programAsset.sourceCsScript != null && !PrefabUtility.IsPartOfPrefabAsset(target))
                {
                    currentProxyBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(behaviour, ProxySerializationPolicy.NoSerialization);
                    if (currentProxyBehaviour)
                        currentProxyBehaviour.enabled = false;
                }

                DrawDefaultUdonSharpInspector();
            }
        }

        private void OnSceneGUI()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(target))
                return;

            UdonBehaviour behaviour = target as UdonBehaviour;

            if (behaviour.programSource == null ||
                !(behaviour.programSource is UdonSharpProgramAsset udonSharpProgram) ||
                udonSharpProgram.sourceCsScript == null)
                return;

            System.Type customEditorType = null;
            System.Type inspectedType = udonSharpProgram.GetClass();
            if (inspectedType != null)
                customEditorType = UdonSharpCustomEditorManager.GetInspectorEditorType(inspectedType);

            if (customEditorType == null)
                return;

            MethodInfo onSceneGUIMethod = customEditorType.GetMethod("OnSceneGUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);

            if (onSceneGUIMethod == null)
                return;

            udonSharpProgram.UpdateProgram();

            if (baseEditor != null && baseEditor.GetType() != customEditorType)
            {
                DestroyImmediate(baseEditor);
                baseEditor = null;
            }

            UdonSharpBehaviour inspectorTarget = UdonSharpEditorUtility.GetProxyBehaviour(behaviour, ProxySerializationPolicy.All);
            inspectorTarget.enabled = false;

            Editor.CreateCachedEditorWithContext(inspectorTarget, this, customEditorType, ref baseEditor);

            baseEditor.serializedObject.Update();

            onSceneGUIMethod.Invoke(baseEditor, null);

            UdonSharpEditorUtility.CopyProxyToUdon(inspectorTarget, ProxySerializationPolicy.All);
        }

        void DrawDefaultUdonSharpInspector()
        {
            UdonBehaviour behaviour = target as UdonBehaviour;

            if (UdonSharpGUI.DrawProgramSource(behaviour))
                return;

            UdonSharpGUI.DrawSyncSettings(behaviour);
            UdonSharpGUI.DrawInteractSettings(behaviour);

            UdonSharpProgramAsset udonSharpProgramAsset = (UdonSharpProgramAsset)behaviour.programSource;

            UdonSharpGUI.DrawUtilities(behaviour, udonSharpProgramAsset);

            UdonSharpGUI.DrawUILine();

            udonSharpProgramAsset.DrawErrorTextAreas();

            bool dirty = false;
            UdonSharpGUI.DrawPublicVariables(behaviour, udonSharpProgramAsset, ref dirty);
        }

        public override bool RequiresConstantRepaint()
        {
            // Force repaint for variable update in play mode
            bool requiresConstantRepaintDefaultReturnValue = Application.isPlaying;

            if (PrefabUtility.IsPartOfPrefabAsset(target))
                return requiresConstantRepaintDefaultReturnValue;

            UdonBehaviour behaviour = target as UdonBehaviour;

            if (behaviour.programSource == null ||
                !(behaviour.programSource is UdonSharpProgramAsset udonSharpProgram) ||
                udonSharpProgram.sourceCsScript == null)
                return requiresConstantRepaintDefaultReturnValue;

            System.Type customEditorType = null;
            System.Type inspectedType = udonSharpProgram.GetClass();
            if (inspectedType != null)
                customEditorType = UdonSharpCustomEditorManager.GetInspectorEditorType(inspectedType);

            if (customEditorType == null)
                return requiresConstantRepaintDefaultReturnValue;

            MethodInfo requiresConstantRepaintMethod = customEditorType.GetMethod("RequiresConstantRepaint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);

            if (requiresConstantRepaintMethod == null)
                return requiresConstantRepaintDefaultReturnValue;

            udonSharpProgram.UpdateProgram();

            if (baseEditor != null && baseEditor.GetType() != customEditorType)
            {
                DestroyImmediate(baseEditor);
                baseEditor = null;
            }

            UdonSharpBehaviour inspectorTarget = UdonSharpEditorUtility.GetProxyBehaviour(behaviour, ProxySerializationPolicy.All);
            inspectorTarget.enabled = false;

            Editor.CreateCachedEditorWithContext(inspectorTarget, this, customEditorType, ref baseEditor);

            baseEditor.serializedObject.Update();

            bool requiresConstantRepaintReturnValue = (bool)requiresConstantRepaintMethod.Invoke(baseEditor, null);

            UdonSharpEditorUtility.CopyProxyToUdon(inspectorTarget, ProxySerializationPolicy.All);

            return requiresConstantRepaintReturnValue;
        }
    }
}
