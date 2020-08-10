
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor;

namespace UdonSharpEditor
{
    [CustomEditor(typeof(UdonSharpBehaviour), true)]
    [CanEditMultipleObjects]
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
        static void OverrideUdonBehaviourDrawer() 
        {
            if (customEditorField == null)
            {
                Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies().First(e => e.GetName().Name == "UnityEditor");

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
            }

            listClearMethod.Invoke(editorTypeList, null);
            listAddTypeMethod.Invoke(editorTypeList, listCreateParams);

            removeTypeMethod.Invoke(customEditorDictionary, udonBehaviourTypeArr);

            addTypeInvokeParams[1] = editorTypeList;
            addTypeMethod.Invoke(customEditorDictionary, addTypeInvokeParams);
        }
    }
    #endregion

    [InitializeOnLoad]
    static class UdonSharpCustomEditorManager
    {
        static Dictionary<System.Type, System.Type> _typeInspectorMap;

        static UdonSharpCustomEditorManager()
        {
            _typeInspectorMap = new Dictionary<Type, Type>();
            FieldInfo inspectedTypeField = typeof(CustomEditor).GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (Assembly asm in UdonSharpUtils.GetLoadedEditorAssemblies())
            {
                foreach (Type editorType in asm.GetTypes())
                {
                    CustomEditor editorAttribute = editorType.GetCustomAttribute<CustomEditor>();

                    if (editorAttribute != null)
                    {
                        Type inspectedType = (Type)inspectedTypeField.GetValue(editorAttribute);

                        if (inspectedType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                        {
                            _typeInspectorMap.Add(inspectedType, editorType);
                        }
                    }
                }
            }
        }

        public static System.Type GetInspectorEditorType(System.Type udonSharpBehaviourType)
        {
            System.Type editorType;
            _typeInspectorMap.TryGetValue(udonSharpBehaviourType, out editorType);

            return editorType;
        }
    }

    /// <summary>
    /// Custom U# editor for UdonBehaviours that can have custom behavior for drawing stuff like sync position and the program asset info
    /// Will also allow people to override the inspector for their own custom inspectors
    /// </summary>
    internal class UdonBehaviourOverrideEditor : Editor
    {
        Editor baseEditor;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            UdonEditorManager.Instance.WantRepaint += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            UdonEditorManager.Instance.WantRepaint -= Repaint;
        }

        void OnUndoRedo()
        {
            UdonSharpBehaviour inspectorTarget = UdonSharpEditorUtility.GetProxyBehaviour(target as UdonBehaviour, false);

            if (inspectorTarget)
            {
                UdonSharpEditorUtility.CopyProxyToBacker(inspectorTarget);
            }
        }

        private void OnDestroy()
        {
            if (baseEditor)
                DestroyImmediate(baseEditor);
        }

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

            System.Type customEditorType = UdonSharpCustomEditorManager.GetInspectorEditorType(((UdonSharpProgramAsset)behaviour.programSource).sourceCsScript.GetClass());
            if (customEditorType != null)
            {
                if (baseEditor != null && baseEditor.GetType() != customEditorType)
                    DestroyImmediate(baseEditor);

                UdonSharpBehaviour inspectorTarget = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);
                inspectorTarget.enabled = false;

                Editor.CreateCachedEditorWithContext(inspectorTarget, this, customEditorType, ref baseEditor);

                baseEditor.OnInspectorGUI();
                if (GUI.changed)
                    UdonSharpEditorUtility.CopyProxyToBacker(inspectorTarget);

                return;
            }

            DrawDefaultUdonSharpInspector();
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

            UdonSharpGUI.DrawUILine(Color.gray, 2, 4);

            udonSharpProgramAsset.DrawErrorTextAreas();

            bool dirty = false;
            UdonSharpGUI.DrawPublicVariables(behaviour, udonSharpProgramAsset, ref dirty);
        }

        // Force repaint for variable update in play mode
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }
    }
}
