
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

        // https://stackoverflow.com/questions/12898282/type-gettype-not-working 
        static System.Type FindTypeInAllAssemblies(string qualifiedTypeName)
        {
            System.Type t = System.Type.GetType(qualifiedTypeName);

            if (t != null)
            {
                return t;
            }
            else
            {
                foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(qualifiedTypeName);
                    if (t != null)
                        return t;
                }

                return null;
            }
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
                System.Type editorAttributesClass = FindTypeInAllAssemblies("UnityEditor.CustomEditorAttributes");
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

    /// <summary>
    /// Custom U# editor for UdonBehaviours that can have custom behavior for drawing stuff like sync position and the program asset info
    /// Will also allow people to override the inspector for their own custom inspectors
    /// </summary>
    internal class UdonBehaviourOverrideEditor : Editor
    {
        Editor baseEditor;
        static FieldInfo serializedAssetField;
        static FieldInfo hasInteractField;
        static readonly GUIContent ownershipTransferOnCollisionContent = new GUIContent("Allow Ownership Transfer on Collision", 
                                                                                        "Transfer ownership on collision, requires a Collision component on the same game object");

        public override void OnInspectorGUI()
        {
            if (serializedAssetField == null)
            {
                serializedAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);
                hasInteractField = typeof(UdonSharpProgramAsset).GetField("hasInteractEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            UdonBehaviour behaviour = target as UdonBehaviour;

            // Fall back to the default Udon inspector if not a U# behaviour
            if (behaviour.programSource == null || !(behaviour.programSource is UdonSharpProgramAsset udonSharpProgram))
            {
                Editor.CreateCachedEditor(targets, typeof(UdonBehaviourEditor), ref baseEditor);
                baseEditor.OnInspectorGUI();
                return;
            }

            // Program source
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            
            EditorGUI.BeginChangeCheck();
            AbstractUdonProgramSource newProgramSource = (AbstractUdonProgramSource)EditorGUILayout.ObjectField("Program Source", behaviour.programSource, typeof(AbstractUdonProgramSource), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(behaviour, "Change program source");
                behaviour.programSource = newProgramSource;
                serializedAssetField.SetValue(behaviour, newProgramSource != null ? newProgramSource.SerializedProgramAsset : null);
            }

            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Program Script", ((UdonSharpProgramAsset)behaviour.programSource)?.sourceCsScript, typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            // Sync settings
            EditorGUI.BeginChangeCheck();
            bool newSyncPos = EditorGUILayout.Toggle("Synchronize Position", behaviour.SynchronizePosition);
            bool newCollisionTransfer = behaviour.AllowCollisionOwnershipTransfer;
            if (behaviour.GetComponent<Collider>() != null)
            {
                newCollisionTransfer = EditorGUILayout.Toggle(ownershipTransferOnCollisionContent, behaviour.AllowCollisionOwnershipTransfer);
            }
            //else
            //{
            //    EditorGUI.BeginDisabledGroup(true);
            //    newCollisionTransfer = EditorGUILayout.Toggle(ownershipTransferOnCollisionContent, false);
            //    EditorGUI.EndDisabledGroup();
            //}

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(behaviour, "Change sync setting");
                behaviour.SynchronizePosition = newSyncPos;
                behaviour.AllowCollisionOwnershipTransfer = newCollisionTransfer;
            }

            EditorGUI.EndDisabledGroup();

            // Interact settings
            if ((bool)hasInteractField.GetValue(udonSharpProgram))
            {
                //EditorGUILayout.Space();
                //EditorGUILayout.LabelField("Interact", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                string newInteractText = EditorGUILayout.TextField("Interaction Text", behaviour.interactText);
                float newProximity = EditorGUILayout.Slider("Proximity", behaviour.proximity, 0f, 100f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(behaviour, "Change interact property");

                    behaviour.interactText = newInteractText;
                    behaviour.proximity = newProximity;
                }

                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                if (GUILayout.Button("Trigger Interact", GUILayout.Height(22f)))
                    behaviour.SendCustomEvent("_interact");
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space();

            // Variable drawing
        }

        // Force repaint for variable update in play mode
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }
    }
}
