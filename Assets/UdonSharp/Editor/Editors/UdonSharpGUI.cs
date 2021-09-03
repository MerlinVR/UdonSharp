
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace UdonSharpEditor
{
    #region Sync mode menu editor
    internal class SyncModeMenu : EditorWindow
    {
        static SyncModeMenu menu;

        UdonBehaviour udonBehaviour;
        int selectedIdx = -1;

        private static GUIStyle selectionStyle;
        private static GUIStyle descriptionStyle;

        private static readonly List<(GUIContent, GUIContent)> Labels = new List<(GUIContent, GUIContent)>(new[] {
            (new GUIContent("None"), new GUIContent("Replication will be disabled. Variables cannot be synced, and this behaviour will not receive network events.")),
            (new GUIContent("Continuous"), new GUIContent("Continuous replication is intended for frequently-updated variables of small size, and will be tweened.")),
            (new GUIContent("Manual"), new GUIContent("Manual replication is intended for infrequently-updated variables of small or large size, and will not be tweened.")),
        });

        private static Rect GetAreaRect(Rect rect)
        {
            const float borderWidth = 1f;

            Rect areaRect = new Rect(0, 0, rect.width, rect.height);

            areaRect.x += borderWidth;
            areaRect.y += borderWidth;
            areaRect.width -= borderWidth * 2f;
            areaRect.height -= borderWidth * 2f;
            return areaRect;
        }

        private void OnGUI()
        {
            Rect areaRect = GetAreaRect(position);

            GUILayout.BeginArea(areaRect, EditorStyles.textArea);

            for (int i = 0; i < Labels.Count; ++i)
            {
                DrawSelectionOption(Labels[i].Item1, Labels[i].Item2, i);
            }

            GUILayout.EndArea();
        }

        void DrawSelectionOption(GUIContent title, GUIContent descriptor, int index)
        {
            EditorGUILayout.BeginHorizontal();

            GUIStyle checkboxStyle = new GUIStyle();
            checkboxStyle.padding.top = 5;
            checkboxStyle.padding.right = 0;
            checkboxStyle.margin.right = 0;

            if (udonBehaviour.SyncMethod == (Networking.SyncType)(index + 1))
                EditorGUILayout.LabelField("✔", checkboxStyle, GUILayout.Width(10f));
            else
                EditorGUILayout.LabelField("", checkboxStyle, GUILayout.Width(10f));
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(descriptor, descriptionStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Selection handling
            Rect selectionRect = GUILayoutUtility.GetLastRect();

            if (index == selectedIdx)
                DrawSelectionOutline(selectionRect);

            if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            {
                if (selectedIdx != index && selectionRect.Contains(Event.current.mousePosition))
                {
                    selectedIdx = index;
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseUp && selectionRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                SelectIndex(index);
            }
        }

        private void SelectIndex(int idx)
        {
            selectedIdx = idx;

            if (udonBehaviour.SyncMethod != (Networking.SyncType)(selectedIdx + 1))
            {
                Undo.RecordObject(udonBehaviour, "Change sync mode");
                udonBehaviour.SyncMethod = (Networking.SyncType)(selectedIdx + 1);

                PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
            }

            Close();
            GUIUtility.ExitGUI();
        }

        static GUIStyle outlineStyle;

        void DrawSelectionOutline(Rect rect)
        {
            if (outlineStyle == null)
            {
                Texture2D clearColorDarkTex = new Texture2D(1, 1);
                clearColorDarkTex.SetPixel(0, 0, new Color32(64, 128, 223, 255));
                clearColorDarkTex.Apply();

                outlineStyle = new GUIStyle();
                outlineStyle.normal.background = clearColorDarkTex;
            }

            const float outlineWidth = 2f;

            GUI.Box(new Rect(rect.x, rect.y, rect.width, outlineWidth), GUIContent.none, outlineStyle);
            GUI.Box(new Rect(rect.x - outlineWidth, rect.y, outlineWidth, rect.height + outlineWidth + 1f), GUIContent.none, outlineStyle);
            GUI.Box(new Rect(rect.x + rect.width, rect.y, outlineWidth, rect.height + outlineWidth + 1f), GUIContent.none, outlineStyle);
            GUI.Box(new Rect(rect.x - outlineWidth, rect.y + rect.height + outlineWidth + 1f, rect.width + outlineWidth * 2f, outlineWidth), GUIContent.none, outlineStyle);
        }

        private static Rect GUIToScreenRect(Rect rect)
        {
            Vector2 point = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            rect.x = point.x;
            rect.y = point.y;
            return rect;
        }

        public static void Show(Rect controlRect, UdonBehaviour[] behaviours)
        {
            if (selectionStyle == null || descriptionStyle == null)
            {
                selectionStyle = new GUIStyle(EditorStyles.helpBox);
                selectionStyle.font = EditorStyles.label.font;
                selectionStyle.fontSize = EditorStyles.label.fontSize;

                descriptionStyle = new GUIStyle(EditorStyles.label);
                descriptionStyle.wordWrap = true;
            }

            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(typeof(SyncModeMenu));

            foreach (UnityEngine.Object window in windows)
            {
                try
                {
                    if (window is EditorWindow editorWindow)
                        editorWindow.Close();
                }
                catch
                {
                    DestroyImmediate(window);
                }
            }

            Event.current.Use();

            controlRect = GUIToScreenRect(controlRect);

            Vector2 dropdownSize = CalculateDropdownSize(controlRect);

            menu = CreateInstance<SyncModeMenu>();
            menu.udonBehaviour = behaviours[0];
            menu.wantsMouseMove = true;
            //menu.ShowAsDropDown(controlRect, dropdownSize);

            menu.ShowDropDown(controlRect, dropdownSize);
        }

        private static Vector2 CalculateDropdownSize(Rect controlRect)
        {
            Rect areaRect = GetAreaRect(controlRect);
            areaRect.width -= 30f; // Checkbox width

            float totalHeight = 0f;

            for (int i = 0; i < Labels.Count; ++i)
            {
                totalHeight += EditorStyles.boldLabel.CalcHeight(Labels[i].Item1, areaRect.width);
                totalHeight += 13f; // Space()
                totalHeight += descriptionStyle.CalcHeight(Labels[i].Item2, areaRect.width);
                totalHeight += selectionStyle.margin.vertical;
                totalHeight += selectionStyle.padding.vertical;
            }
            
            totalHeight += EditorStyles.textArea.margin.vertical;

            return new Vector2(controlRect.width, totalHeight);
        }

        private static Array _popupLocationArray;

        void ShowDropDown(Rect controlRect, Vector2 size)
        {
            if (_popupLocationArray == null)
            {
                System.Type popupLocationType = AppDomain.CurrentDomain.GetAssemblies().First(e => e.GetName().Name == "UnityEditor").GetType("UnityEditor.PopupLocation");

                _popupLocationArray = (Array)Activator.CreateInstance(popupLocationType.MakeArrayType(), 2);
                _popupLocationArray.SetValue(0, 0); // PopupLocation.Below
                _popupLocationArray.SetValue(4, 1); // PopupLocation.Overlay
            }

            MethodInfo showAsDropDownMethod = typeof(EditorWindow).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).First(e => e.GetParameters().Length == 3);

            showAsDropDownMethod.Invoke(this, new object[] { controlRect, size, _popupLocationArray });
        }
    }
    #endregion

    public static class UdonSharpGUI
    {
        private static GUIStyle errorTextStyle;
        private static GUIStyle undoLabelStyle;
        private static GUIContent undoArrowLight;
        private static GUIContent undoArrowDark;
        private static GUIContent undoArrowContent;
        private static Texture2D clearColorLight;
        private static Texture2D clearColorDark;
        private static GUIStyle clearColorStyle;

        /// <summary>
        /// Draws compile errors if there are any, shows nothing if there are no compile errors
        /// </summary>
        /// <param name="udonSharpProgram"></param>
        [PublicAPI]
        public static void DrawCompileErrorTextArea(UdonSharpProgramAsset udonSharpProgram)
        {
            if (udonSharpProgram.compileErrors == null || udonSharpProgram.compileErrors.Count == 0)
                return;

            if (errorTextStyle == null)
            {
                errorTextStyle = new GUIStyle(EditorStyles.textArea);
                errorTextStyle.normal.textColor = new Color32(211, 34, 34, 255);
                errorTextStyle.focused.textColor = errorTextStyle.normal.textColor;
            }

            // todo: convert this to a tree view that just has a list of selectable items that jump to the error
            EditorGUILayout.LabelField($"Compile Error{(udonSharpProgram.compileErrors.Count > 1 ? "s" : "")}", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.Join("\n", udonSharpProgram.compileErrors.Select(e => e.Replace("[<color=#FF00FF>UdonSharp</color>] ", ""))), errorTextStyle);
        }

        private static void SetupGUI()
        {
            if (undoLabelStyle == null ||
                undoArrowLight == null ||
                undoArrowDark == null ||
                clearColorLight == null ||
                clearColorDark == null ||
                clearColorStyle == null)
            {
                undoLabelStyle = new GUIStyle(EditorStyles.label);
                undoLabelStyle.alignment = TextAnchor.MiddleCenter;
                undoLabelStyle.padding = new RectOffset(0, 0, 1, 0);
                undoLabelStyle.margin = new RectOffset(0, 0, 0, 0);
                undoLabelStyle.border = new RectOffset(0, 0, 0, 0);
                undoLabelStyle.stretchWidth = false;
                undoLabelStyle.stretchHeight = false;

                undoArrowLight = new GUIContent((Texture)EditorGUIUtility.Load("Assets/UdonSharp/Editor/Resources/UndoArrowLight.png"), "Reset to default value");
                undoArrowDark = new GUIContent((Texture)EditorGUIUtility.Load("Assets/UdonSharp/Editor/Resources/UndoArrowBlack.png"), "Reset to default value");

                Texture2D clearColorLightTex = new Texture2D(1, 1);
                clearColorLightTex.SetPixel(0, 0, new Color32(194, 194, 194, 255));
                clearColorLightTex.Apply();

                clearColorLight = clearColorLightTex;

                Texture2D clearColorDarkTex = new Texture2D(1, 1);
                clearColorDarkTex.SetPixel(0, 0, new Color32(56, 56, 56, 255));
                clearColorDarkTex.Apply();

                clearColorDark = clearColorDarkTex;

                clearColorStyle = new GUIStyle();
            }

            undoArrowContent = EditorGUIUtility.isProSkin ? undoArrowLight : undoArrowDark;
            clearColorStyle.normal.background = EditorGUIUtility.isProSkin ? clearColorDark : clearColorLight;
        }

        class USharpEditorState
        {
            public bool showExtraOptions;
            public bool showProgramUasm;
            public bool showProgramDisassembly;
            public string customEventName = "";
        }

        private static Dictionary<UdonSharpProgramAsset, USharpEditorState> _editorStates = new Dictionary<UdonSharpProgramAsset, USharpEditorState>();
        private static USharpEditorState GetEditorState(UdonSharpProgramAsset programAsset)
        {
            USharpEditorState editorState;
            if (!_editorStates.TryGetValue(programAsset, out editorState))
            {
                editorState = new USharpEditorState();
                editorState.showExtraOptions = programAsset.showUtilityDropdown;
                _editorStates.Add(programAsset, editorState);
            }

            return editorState;
        }
        
        internal static void DrawUtilities(UdonBehaviour udonBehaviour, UdonSharpProgramAsset programAsset)
        {
            USharpEditorState editorState = GetEditorState(programAsset);

            if (!udonBehaviour)
            {
                if (GUILayout.Button("Compile Program"))
                {
                    programAsset.CompileCsProgram();
                }

                if (GUILayout.Button("Compile All UdonSharp Programs"))
                {
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                }
            }

            if (udonBehaviour)
            {
                editorState.showExtraOptions = programAsset.showUtilityDropdown = EditorGUILayout.Foldout(editorState.showExtraOptions, "Utilities", true);
                if (editorState.showExtraOptions)
                {
                    if (GUILayout.Button("Compile All UdonSharp Programs"))
                    {
                        UdonSharpProgramAsset.CompileAllCsPrograms(true);
                    }

                    EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

                    if (GUILayout.Button("Send Custom Event"))
                    {
                        if (udonBehaviour != null)
                        {
                            bool needsProxyCall = false;

                            UdonSharpBehaviour proxy = UdonSharpEditorUtility.FindProxyBehaviour(udonBehaviour);

                            if (proxy)
                            {
                                System.Type inspectorType = UdonSharpCustomEditorManager.GetInspectorEditorType(proxy.GetType());
                                if (inspectorType != null)
                                    needsProxyCall = true;
                            }

                            if (needsProxyCall)
                                UdonSharpEditorUtility.CopyProxyToUdon(proxy, ProxySerializationPolicy.All);

                            if (udonBehaviour != null)
                                udonBehaviour.SendCustomEvent(editorState.customEventName);

                            if (needsProxyCall)
                                UdonSharpEditorUtility.CopyUdonToProxy(proxy, ProxySerializationPolicy.All);
                        }
                    }

                    editorState.customEventName = EditorGUILayout.TextField("Event Name:", editorState.customEventName);

                    EditorGUI.EndDisabledGroup();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

                EditorGUILayout.Space();

                if (GUILayout.Button("Export to Assembly Asset"))
                {
                    string savePath = EditorUtility.SaveFilePanelInProject("Assembly asset save location", Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(programAsset.sourceCsScript)), "asset", "Choose a save location for the assembly asset");

                    if (savePath.Length > 0)
                    {
                        UdonSharpEditorUtility.UdonSharpProgramToAssemblyProgram(programAsset, savePath);
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                editorState.showProgramUasm = EditorGUILayout.Foldout(editorState.showProgramUasm, "Compiled C# Udon Assembly", true);
                if (editorState.showProgramUasm)
                {
                    programAsset.DrawAssemblyText();
                }

                if (programAsset.GetRealProgram() != null)
                {
                    editorState.showProgramDisassembly = EditorGUILayout.Foldout(editorState.showProgramDisassembly, "Program Disassembly", true);
                    if (editorState.showProgramDisassembly)
                        programAsset.DrawProgramDisassembly();
                }
            }
        }

        /// <summary>
        /// Draws the default utilities for UdonSharpBehaviours, these are currently the compile all scripts button and the send custom event button
        /// </summary>
        /// <param name="target"></param>
        [PublicAPI]
        public static void DrawUtilities(UnityEngine.Object target)
        {
            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)target);

            if (backingBehaviour && backingBehaviour.programSource)
            {
                DrawUtilities(backingBehaviour, (UdonSharpProgramAsset)backingBehaviour.programSource);
            }
        }

        #region Default UdonSharpBehaviour drawing
        internal static bool DrawCreateScriptButton(UdonSharpProgramAsset programAsset)
        {
            if (GUILayout.Button("Create Script"))
            {
                string thisPath = AssetDatabase.GetAssetPath(programAsset);
                string fileName = Path.GetFileNameWithoutExtension(thisPath).Replace(" Udon C# Program Asset", "").Replace(" ", "").Replace("#", "Sharp");
                string chosenFilePath = EditorUtility.SaveFilePanelInProject("Save UdonSharp File", fileName, "cs", "Save UdonSharp file", Path.GetDirectoryName(thisPath));

                if (chosenFilePath.Length > 0)
                {
                    chosenFilePath = UdonSharpSettings.SanitizeScriptFilePath(chosenFilePath);

                    string fileContents = UdonSharpSettings.GetProgramTemplateString(Path.GetFileNameWithoutExtension(chosenFilePath));

                    File.WriteAllText(chosenFilePath, fileContents, System.Text.Encoding.UTF8);

                    AssetDatabase.ImportAsset(chosenFilePath, ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.Refresh();

                    programAsset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(chosenFilePath);

                    return true;
                }
            }

            return false;
        }

        private static MonoScript currentUserScript;
        private static UnityEngine.Object ValidateObjectReference(UnityEngine.Object[] references, System.Type objType, SerializedProperty property, Enum options = null)
        {
            if (property != null)
                throw new System.ArgumentException("Serialized property on validate object reference should be null!");

            if (currentUserScript != null ||
                objType == typeof(UdonBehaviour) ||
                objType == typeof(UdonSharpBehaviour))
            {
                foreach (UnityEngine.Object reference in references)
                {
                    GameObject referenceObject = reference as GameObject;
                    UdonBehaviour referenceBehaviour = reference as UdonBehaviour;

                    if (referenceObject != null)
                    {
                        UdonBehaviour[] components = referenceObject.GetComponents<UdonBehaviour>();

                        UdonBehaviour foundComponent = null;

                        foreach (UdonBehaviour component in components)
                        {
                            foundComponent = ValidateObjectReference(new UnityEngine.Object[] { component }, objType, null, UdonSyncMode.NotSynced /* just any enum, we don't care */) as UdonBehaviour;

                            if (foundComponent != null)
                            {
                                return foundComponent;
                            }
                        }
                    }
                    else if (referenceBehaviour != null)
                    {
                        if (referenceBehaviour.programSource != null &&
                            referenceBehaviour.programSource is UdonSharpProgramAsset udonSharpProgram &&
                            udonSharpProgram.sourceCsScript != null)
                        {
                            if (currentUserScript == null || // If this is null, the field is referencing a generic UdonBehaviour or UdonSharpBehaviour instead of a behaviour of a certain type that inherits from UdonSharpBehaviour.
                                udonSharpProgram.sourceCsScript == currentUserScript)
                                return referenceBehaviour;
                        }
                    }
                }
            }
            else
            {
                // Fallback to default handling if the user has not compiled with the new info
                if (references[0] != null && references[0] is GameObject && typeof(Component).IsAssignableFrom(objType))
                {
                    GameObject gameObject = (GameObject)references[0];
                    references = gameObject.GetComponents(typeof(Component));
                }
                foreach (UnityEngine.Object component in references)
                {
                    if (component != null && objType.IsAssignableFrom(component.GetType()))
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static bool IsNormalUnityObject(System.Type declaredType, FieldDefinition fieldDefinition)
        {
            return !UdonSharpUtils.IsUserDefinedBehaviour(declaredType) && (fieldDefinition == null || fieldDefinition.fieldSymbol.userCsType == null || !fieldDefinition.fieldSymbol.IsUserDefinedBehaviour());
        }

        private static UdonSharpProgramAsset _currentProgramAsset;
        private static UdonBehaviour _currentBehaviour;

        private static object DrawUnityObjectField(GUIContent fieldName, string symbol, (object value, System.Type declaredType, FieldDefinition symbolField) publicVariable, ref bool dirty)
        {
            (object value, System.Type declaredType, FieldDefinition symbolField) = publicVariable;

            FieldDefinition fieldDefinition = symbolField;

            if (IsNormalUnityObject(declaredType, fieldDefinition))
                return EditorGUILayout.ObjectField(fieldName, (UnityEngine.Object)value, declaredType, true);

            MethodInfo doObjectFieldMethod = typeof(EditorGUI).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Where(e => e.Name == "DoObjectField" && e.GetParameters().Length == 8).FirstOrDefault();

            if (doObjectFieldMethod == null)
                throw new System.Exception("Could not find DoObjectField() method");

            Rect objectRect = EditorGUILayout.GetControlRect();
            Rect originalRect = objectRect;
            int id = GUIUtility.GetControlID(typeof(UnityEngine.Object).GetHashCode(), FocusType.Keyboard, originalRect);

            objectRect = EditorGUI.PrefixLabel(objectRect, id, new GUIContent(fieldName));

            System.Type searchType = fieldDefinition.userBehaviourSource != null ? fieldDefinition.userBehaviourSource.GetClass() : typeof(UdonSharpBehaviour);

            UnityEngine.Object objectFieldValue = (UnityEngine.Object)doObjectFieldMethod.Invoke(null, new object[] {
                objectRect,
                objectRect,
                id,
                (UnityEngine.Object)value,
                searchType,
                null,
                null,
                true
            });

            if (objectFieldValue != null &&
                objectFieldValue is UdonSharpBehaviour udonSharpBehaviour &&
                UdonSharpEditorUtility.IsProxyBehaviour(udonSharpBehaviour))
            {
                objectFieldValue = UdonSharpEditorUtility.GetBackingUdonBehaviour(udonSharpBehaviour);
            }
            else if (!(objectFieldValue is UdonBehaviour))
            {
                objectFieldValue = null;
            }

            string labelText;
            System.Type variableType = fieldDefinition.fieldSymbol.userCsType;

            while (variableType.IsArray)
                variableType = variableType.GetElementType();

            if (objectFieldValue == null)
            {
                labelText = $"None ({ObjectNames.NicifyVariableName(variableType.Name)})";
            }
            else
            {
                UdonBehaviour targetBehaviour = objectFieldValue as UdonBehaviour;
                UdonSharpProgramAsset targetProgramAsset = targetBehaviour?.programSource as UdonSharpProgramAsset;
                if (targetProgramAsset?.GetClass() != null)
                    variableType = targetProgramAsset.GetClass();

                labelText = $"{objectFieldValue.name} ({variableType.Name})";
            }

            // Overwrite any content already on the background from drawing the normal object field
            GUI.Box(originalRect, GUIContent.none, clearColorStyle);

            // Manually draw this using the same ID so that we can get some of the style information to bleed over
            objectRect = EditorGUI.PrefixLabel(originalRect, id, new GUIContent(fieldName));
            if (Event.current.type == EventType.Repaint)
                EditorStyles.objectField.Draw(objectRect, new GUIContent(labelText, objectFieldValue == null ? null : AssetPreview.GetMiniThumbnail(_currentProgramAsset)), id);

            return objectFieldValue;
        }
        
        private static Dictionary<UdonBehaviour, Dictionary<string, bool>> _foldoutStates = new Dictionary<UdonBehaviour, Dictionary<string, bool>>();

        private static bool GetFoldoutState(UdonBehaviour behaviour, string foldoutIdentifier)
        {
            if (behaviour == null)
                return false;

            Dictionary<string, bool> foldoutDict;
            if (!_foldoutStates.TryGetValue(behaviour, out foldoutDict))
                return false;

            bool foldoutState;
            if (!foldoutDict.TryGetValue(foldoutIdentifier, out foldoutState))
                return false;

            return foldoutState;
        }

        private static void SetFoldoutState(UdonBehaviour behaviour, string foldoutIdentifier, bool value)
        {
            if (behaviour == null)
                return;

            Dictionary<string, bool> foldoutDict;
            if (!_foldoutStates.TryGetValue(behaviour, out foldoutDict))
            {
                foldoutDict = new Dictionary<string, bool>();
                _foldoutStates.Add(behaviour, foldoutDict);
            }
            
            if (!foldoutDict.ContainsKey(foldoutIdentifier))
            {
                foldoutDict.Add(foldoutIdentifier, false);
            }
            else
            {
                foldoutDict[foldoutIdentifier] = value;
            }
        }

        private static object DrawFieldForType(string fieldName, string symbol, (object value, Type declaredType, FieldDefinition symbolField) publicVariable, System.Type currentType, ref bool dirty, bool enabled)
        {
            bool isArrayElement = fieldName != null;

            (object value, Type declaredType, FieldDefinition symbolField) = publicVariable;

            FieldDefinition fieldDefinition = symbolField;

            if (fieldName == null)
                fieldName = ObjectNames.NicifyVariableName(symbol);

            GUIContent fieldLabel = null;

            TooltipAttribute tooltip = fieldDefinition?.GetAttribute<TooltipAttribute>();

            if (tooltip != null)
                fieldLabel = new GUIContent(fieldName, tooltip.tooltip);
            else
                fieldLabel = new GUIContent(fieldName);

            if (declaredType.IsArray)
            {
                bool foldoutEnabled = GetFoldoutState(_currentBehaviour, symbol);

                Event tempEvent = new Event(Event.current);

                Rect foldoutRect = EditorGUILayout.GetControlRect();
                foldoutEnabled = EditorGUI.Foldout(foldoutRect, foldoutEnabled, fieldLabel, true);

                SetFoldoutState(_currentBehaviour, symbol, foldoutEnabled);

                Type arrayDataType = currentType;

                bool canCopyPlace = true;

                if (UdonSharpUtils.IsUserJaggedArray(currentType))
                {
                    canCopyPlace = false;
                    arrayDataType = typeof(object[]);
                }
                else if (currentType.IsArray && UdonSharpUtils.IsUserDefinedBehaviour(currentType))
                {
                    arrayDataType = typeof(Component[]);
                }

                switch (tempEvent.type)
                {
                    case EventType.DragExited:
                        if (GUI.enabled)
                            HandleUtility.Repaint();
                        break;
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if (foldoutRect.Contains(tempEvent.mousePosition) && GUI.enabled && canCopyPlace)
                        {
                            int foldoutId = (int)typeof(EditorGUIUtility).GetField("s_LastControlID", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

                            UnityEngine.Object[] references = DragAndDrop.objectReferences;
                            UnityEngine.Object[] objArray = new UnityEngine.Object[1];

                            bool acceptedDrag = false;

                            List<UnityEngine.Object> draggedReferences = new List<UnityEngine.Object>();

                            currentUserScript = fieldDefinition?.userBehaviourSource;
                            foreach (UnityEngine.Object obj in references)
                            {
                                objArray[0] = obj;
                                UnityEngine.Object validatedObject = ValidateObjectReference(objArray, currentType.GetElementType(), null);

                                if (validatedObject != null)
                                {
                                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                    if (tempEvent.type == EventType.DragPerform)
                                    {
                                        draggedReferences.Add(validatedObject);
                                        acceptedDrag = true;
                                        DragAndDrop.activeControlID = 0;
                                    }
                                    else
                                    {
                                        DragAndDrop.activeControlID = foldoutId;
                                    }
                                }
                            }
                            currentUserScript = null;

                            if (acceptedDrag)
                            {
                                Array oldArray = (Array)value;

                                Array newArray = Activator.CreateInstance(UdonSharpUtils.RemapBaseType(arrayDataType), new object[] { oldArray.Length + draggedReferences.Count }) as Array;
                                Array.Copy(oldArray, newArray, oldArray.Length);
                                Array.Copy(draggedReferences.ToArray(), 0, newArray, oldArray.Length, draggedReferences.Count);

                                GUI.changed = true;
                                Event.current.Use();
                                DragAndDrop.AcceptDrag();

                                return newArray;
                            }
                        }

                        break;
                }

                if (foldoutEnabled)
                {
                    System.Type elementType = currentType.GetElementType();

                    if (value == null)
                    {
                        GUI.changed = true;
                        return System.Activator.CreateInstance(UdonSharpUtils.RemapBaseType(arrayDataType), new object[] { 0 });
                    }

                    EditorGUI.indentLevel++;

                    Array valueArray = value as Array;

                    using (EditorGUILayout.VerticalScope verticalScope = new EditorGUILayout.VerticalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        int newLength = EditorGUILayout.DelayedIntField("Size", valueArray.Length);
                        if (newLength < 0)
                        {
                            Debug.LogError("Array size must be non-negative.");
                            newLength = valueArray.Length;
                        }

                        // We need to resize the array
                        if (EditorGUI.EndChangeCheck())
                        {
                            Array newArray = Activator.CreateInstance(UdonSharpUtils.RemapBaseType(arrayDataType), new object[] { newLength }) as Array;

                            for (int i = 0; i < newLength && i < valueArray.Length; ++i)
                            {
                                newArray.SetValue(valueArray.GetValue(i), i);
                            }

                            // Fill the empty elements with the last element's value when expanding the array
                            if (valueArray.Length > 0 && newLength > valueArray.Length)
                            {
                                object lastElementVal = valueArray.GetValue(valueArray.Length - 1);
                                if (!(lastElementVal is Array)) // We do not want copies of the reference to a jagged array element to be copied
                                {
                                    for (int i = valueArray.Length; i < newLength; ++i)
                                    {
                                        newArray.SetValue(lastElementVal, i);
                                    }
                                }
                            }

                            EditorGUI.indentLevel--;
                            return newArray;
                        }

                        for (int i = 0; i < valueArray.Length; ++i)
                        {
                            var elementData = (valueArray.GetValue(i), elementType, fieldDefinition);

                            EditorGUI.BeginChangeCheck();
                            object newArrayVal = DrawFieldForType($"Element {i}", $"{symbol}_element{i}", elementData, currentType.GetElementType(), ref dirty, enabled);

                            if (EditorGUI.EndChangeCheck())
                            {
                                valueArray = (Array)valueArray.Clone();
                                valueArray.SetValue(newArrayVal, i);
                            }
                        }

                        EditorGUI.indentLevel--;

                        return valueArray;
                    }
                }
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
            {
                return DrawUnityObjectField(fieldLabel, symbol, (value, declaredType, symbolField), ref dirty);
            }
            else if (declaredType == typeof(string))
            {
                TextAreaAttribute textArea = fieldDefinition == null ? null : fieldDefinition.GetAttribute<TextAreaAttribute>();

                if (textArea != null)
                {
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(fieldLabel);
                    string textAreaText = EditorGUILayout.TextArea((string)value);
                    EditorGUILayout.EndVertical();

                    return textAreaText;
                }
                else
                {
                    return EditorGUILayout.TextField(fieldLabel, (string)value);
                }
            }
            else if (declaredType == typeof(float))
            {
                RangeAttribute range = fieldDefinition?.GetAttribute<RangeAttribute>();

                if (range != null)
                    return EditorGUILayout.Slider(fieldLabel, (float?)value ?? default, range.min, range.max);
                else
                    return EditorGUILayout.FloatField(fieldLabel, (float?)value ?? default);
            }
            else if (declaredType == typeof(double))
            {
                RangeAttribute range = fieldDefinition?.GetAttribute<RangeAttribute>();

                if (range != null)
                    return EditorGUILayout.Slider(fieldLabel, (float)((double?)value ?? default), range.min, range.max);
                else
                    return EditorGUILayout.DoubleField(fieldLabel, (double?)value ?? default);
            }
            else if (declaredType == typeof(int))
            {
                RangeAttribute range = fieldDefinition?.GetAttribute<RangeAttribute>();

                if (range != null)
                    return EditorGUILayout.IntSlider(fieldLabel, (int?)value ?? default, (int)range.min, (int)range.max);
                else
                    return EditorGUILayout.IntField(fieldLabel, (int?)value ?? default);
            }
            else if (declaredType == typeof(bool))
            {
                return EditorGUILayout.Toggle(fieldLabel, (bool?)value ?? default);
            }
            else if (declaredType == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(fieldLabel, (Vector2?)value ?? default);
            }
            else if (declaredType == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(fieldLabel, (Vector3?)value ?? default);
            }
            else if (declaredType == typeof(Vector4))
            {
                return EditorGUILayout.Vector4Field(fieldLabel, (Vector4?)value ?? default);
            }
            else if (declaredType == typeof(Color))
            {
                ColorUsageAttribute colorUsage = fieldDefinition?.GetAttribute<ColorUsageAttribute>();

                if (colorUsage != null)
                {
                    return EditorGUILayout.ColorField(fieldLabel, (Color?)value ?? default, false, colorUsage.showAlpha, colorUsage.hdr);
                }
                else
                {
                    return EditorGUILayout.ColorField(fieldLabel, (Color?)value ?? default);
                }
            }
            else if (declaredType == typeof(Color32))
            {
                ColorUsageAttribute colorUsage = fieldDefinition?.GetAttribute<ColorUsageAttribute>();

                if (colorUsage != null)
                {
                    return (Color32)EditorGUILayout.ColorField(fieldLabel, (Color32?)value ?? default, false, colorUsage.showAlpha, false);
                }
                else
                {
                    return (Color32)EditorGUILayout.ColorField(fieldLabel, (Color32?)value ?? default);
                }
            }
            else if (declaredType == typeof(Quaternion))
            {
                Quaternion quatVal = (Quaternion?)value ?? default;
                Vector4 newQuat = EditorGUILayout.Vector4Field(fieldLabel, new Vector4(quatVal.x, quatVal.y, quatVal.z, quatVal.w));
                return new Quaternion(newQuat.x, newQuat.y, newQuat.z, newQuat.w);
            }
            else if (declaredType == typeof(Bounds))
            {
                return EditorGUILayout.BoundsField(fieldLabel, (Bounds?)value ?? default);
            }
            else if (declaredType == typeof(BoundsInt))
            {
                return EditorGUILayout.BoundsIntField(fieldLabel, (BoundsInt?)value ?? default);
            }
            else if (declaredType == typeof(ParticleSystem.MinMaxCurve))
            {
                // This is just matching the standard Udon editor's capability at the moment, I want to eventually switch it to use the proper curve editor, but that will take a chunk of work
                ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve?)value ?? default;

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(fieldLabel);
                EditorGUI.indentLevel++;
                minMaxCurve.curveMultiplier = EditorGUILayout.FloatField("Multiplier", minMaxCurve.curveMultiplier);
                minMaxCurve.curveMin = EditorGUILayout.CurveField("Min Curve", minMaxCurve.curveMin);
                minMaxCurve.curveMax = EditorGUILayout.CurveField("Max Curve", minMaxCurve.curveMax);

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();

                return minMaxCurve;
            }
            else if (declaredType == typeof(LayerMask)) // Lazy layermask support, todo: make it more like the editor layer mask and also don't do all these LINQ operations and such every draw
            {
                // Using 'Everything' with this method does not actually enable all layers correctly when you have unused layers so it's not a functional solution
                //return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUILayout.MaskField(fieldLabel, InternalEditorUtility.LayerMaskToConcatenatedLayersMask((LayerMask?)value ?? default), InternalEditorUtility.layers));
                return (LayerMask)EditorGUILayout.MaskField(fieldLabel, (LayerMask?)value ?? default, Enumerable.Range(0, 32).Select(e => LayerMask.LayerToName(e).Length > 0 ? e + ": " + LayerMask.LayerToName(e) : "").ToArray());
            }
            else if (declaredType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(fieldLabel, (Enum)(value ?? Activator.CreateInstance(declaredType)));
            }
            else if (declaredType == typeof(System.Type))
            {
                string typeName = value != null ? ((Type)value).FullName : "null";
                EditorGUILayout.LabelField(fieldLabel, typeName);
            }
            else if (declaredType == typeof(Gradient))
            {
                GradientUsageAttribute gradientUsage = fieldDefinition == null ? null : fieldDefinition.GetAttribute<GradientUsageAttribute>();

                if (value == null)
                {
                    value = new Gradient();
                    GUI.changed = true;
                }

                if (gradientUsage != null)
                {
                    return EditorGUILayout.GradientField(fieldLabel, (Gradient)value, gradientUsage.hdr);
                }
                else
                {
                    return EditorGUILayout.GradientField(fieldLabel, (Gradient)value);
                }
            }
            else if (declaredType == typeof(AnimationCurve))
            {
                return EditorGUILayout.CurveField(fieldLabel, (AnimationCurve)value ?? new AnimationCurve());
            }
            else if (declaredType == typeof(char))
            {
                string stringVal = EditorGUILayout.TextField(fieldLabel, (((char?)value) ?? default).ToString());
                if (stringVal.Length > 0)
                    return stringVal[0];
                else
                    return (char?)value ?? default;
            }
            else if (declaredType == typeof(uint))
            {
                return (uint)Math.Min(Math.Max(EditorGUILayout.LongField(fieldLabel, (uint?)value ?? default), uint.MinValue), uint.MaxValue);
            }
            else if (declaredType == typeof(long))
            {
                return EditorGUILayout.LongField(fieldLabel, (long?)value ?? default);
            }
            else if (declaredType == typeof(byte))
            {
                return (byte)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (byte?)value ?? default), byte.MinValue, byte.MaxValue);
            }
            else if (declaredType == typeof(sbyte))
            {
                return (sbyte)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (sbyte?)value ?? default), sbyte.MinValue, sbyte.MaxValue);
            }
            else if (declaredType == typeof(short))
            {
                return (short)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (short?)value ?? default), short.MinValue, short.MaxValue);
            }
            else if (declaredType == typeof(ushort))
            {
                return (ushort)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (ushort?)value ?? default), ushort.MinValue, ushort.MaxValue);
            }
            else if (declaredType == typeof(Rect))
            {
                return EditorGUILayout.RectField(fieldLabel, (Rect?)value ?? default);
            }
            else if (declaredType == typeof(RectInt))
            {
                return EditorGUILayout.RectIntField(fieldLabel, (RectInt?)value ?? default);
            }
            else if (declaredType == typeof(VRC.SDKBase.VRCUrl))
            {
                VRC.SDKBase.VRCUrl url = (VRC.SDKBase.VRCUrl)value ?? new VRC.SDKBase.VRCUrl("");
                url = new VRC.SDKBase.VRCUrl(EditorGUILayout.TextField(fieldLabel, url.Get()));
                return url;
            }
            else
            {
                EditorGUILayout.LabelField($"{fieldName}: no drawer for type {declaredType}");

                return value;
            }

            return value;
        }

        private static object DrawPublicVariableField(UdonBehaviour currentBehaviour, UdonSharpProgramAsset programAsset, string symbol, object variableValue, Type variableType, ref bool dirty, bool enabled)
        {
            bool shouldUseRuntimeValue = EditorApplication.isPlaying && currentBehaviour != null && GUI.enabled; // GUI.enabled is determined in DrawProgramSourceGUI

            EditorGUI.BeginDisabledGroup(!enabled);

            bool shouldDraw = true;
            bool isArray = variableType.IsArray;

            FieldDefinition symbolField;
            if (programAsset.fieldDefinitions != null && programAsset.fieldDefinitions.TryGetValue(symbol, out symbolField))
            {
                HideInInspector hideAttribute = symbolField.GetAttribute<HideInInspector>();

                if (hideAttribute != null)
                {
                    shouldDraw = false;
                }

                foreach (Attribute attribute in symbolField.fieldAttributes)
                {
                    if (attribute == null)
                        continue;

                    if (attribute is HeaderAttribute)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField((attribute as HeaderAttribute).header, EditorStyles.boldLabel);
                    }
                    else if (attribute is SpaceAttribute)
                    {
                        GUILayout.Space((attribute as SpaceAttribute).height);
                    }
                }
            }
            else
            {
                symbolField = new FieldDefinition(null);
            }

            if (shouldDraw)
            {
                if (shouldUseRuntimeValue)
                {
                    variableValue = currentBehaviour.GetProgramVariable(symbol);
                }

                if (!isArray) // Drawing horizontal groups on arrays screws them up, there's probably better handling for this using a manual rect
                    EditorGUILayout.BeginHorizontal();

                FieldDefinition fieldDefinition = null;
                if (programAsset.fieldDefinitions != null)
                    programAsset.fieldDefinitions.TryGetValue(symbol, out fieldDefinition);

                EditorGUI.BeginChangeCheck();
                object newValue = DrawFieldForType(null, symbol, (variableValue, variableType, fieldDefinition), fieldDefinition != null ? fieldDefinition.fieldSymbol.userCsType : null, ref dirty, enabled);

                bool changed = EditorGUI.EndChangeCheck();

                if (changed)
                {
                    if (variableType == typeof(double)) newValue = Convert.ToDouble(newValue);

                    if (shouldUseRuntimeValue)
                    {
                        currentBehaviour.SetProgramVariable(symbol, newValue);
                    }
                    else
                    {
                        dirty = true;
                        variableValue = newValue;
                    }
                }

                if (symbolField.fieldSymbol != null && symbolField.fieldSymbol.syncMode != UdonSyncMode.NotSynced)
                {
                    if (symbolField.fieldSymbol.syncMode == UdonSyncMode.None)
                        GUILayout.Label("synced", GUILayout.Width(55f));
                    else
                        GUILayout.Label($"sync: {Enum.GetName(typeof(UdonSyncMode), symbolField.fieldSymbol.syncMode)}", GUILayout.Width(85f));
                }

                if (!isArray)
                {
                    object originalValue = programAsset.GetRealProgram().Heap.GetHeapVariable(programAsset.GetRealProgram().SymbolTable.GetAddressFromSymbol(symbol));

                    if (originalValue != null && !originalValue.Equals(variableValue))
                    {
                        int originalIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        // Check if changed because otherwise the UI throw an error since we changed that we want to draw the undo arrow in the middle of drawing when we're modifying stuff like colors
                        if (!changed && GUI.Button(EditorGUILayout.GetControlRect(GUILayout.Width(14f), GUILayout.Height(11f)), undoArrowContent, undoLabelStyle))
                        {
                            if (shouldUseRuntimeValue)
                            {
                                currentBehaviour.SetProgramVariable(symbol, originalValue);
                            }
                            else
                            {
                                dirty = true;
                                variableValue = originalValue;
                            }
                        }
                        EditorGUI.indentLevel = originalIndent;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.EndDisabledGroup();

            return variableValue;
        }
        #endregion

        /// <summary>
        /// The default drawing for UdonSharpBehaviour public variables
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="programAsset"></param>
        /// <param name="dirty"></param>
        [PublicAPI]
        public static void DrawPublicVariables(UdonBehaviour behaviour, UdonSharpProgramAsset programAsset, ref bool dirty)
        {
            SetupGUI();

            programAsset.UpdateProgram();

            _currentProgramAsset = programAsset;
            _currentBehaviour = behaviour;

            IUdonVariable CreateUdonVariable(string symbolName, object value, System.Type type)
            {
                System.Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(type);
                return (IUdonVariable)Activator.CreateInstance(udonVariableType, symbolName, value);
            }

            IUdonVariableTable publicVariables = null;
            if (behaviour)
                publicVariables = behaviour.publicVariables;

            IUdonProgram program = programAsset?.GetRealProgram();
            if (program?.SymbolTable == null)
            {
                return;
            }

            IUdonSymbolTable symbolTable = program.SymbolTable;

            ImmutableArray<string> exportedSymbolNames = symbolTable.GetExportedSymbols();

            EditorGUI.BeginChangeCheck();

            foreach (string exportedSymbol in exportedSymbolNames)
            {
                System.Type symbolType = symbolTable.GetSymbolType(exportedSymbol);
                if (publicVariables == null)
                {
                    DrawPublicVariableField(behaviour, programAsset, exportedSymbol, programAsset.GetPublicVariableDefaultValue(exportedSymbol), symbolType, ref dirty, false);
                    continue;
                }
                
                if (!publicVariables.TryGetVariableValue(exportedSymbol, out object variableValue))
                {
                    variableValue = programAsset.GetPublicVariableDefaultValue(exportedSymbol);
                    dirty = true;
                }

                variableValue = DrawPublicVariableField(behaviour, programAsset, exportedSymbol, variableValue, symbolType, ref dirty, true);
                if (!dirty)
                    continue;

                Undo.RecordObject(behaviour, "Modify variable");

                if (!publicVariables.TrySetVariableValue(exportedSymbol, variableValue))
                {
                    if (!publicVariables.TryAddVariable(CreateUdonVariable(exportedSymbol, variableValue, symbolType)))
                    {
                        Debug.LogError($"Failed to set public variable '{exportedSymbol}' value.");
                    }
                }
            }

            if (behaviour)
            {
                foreach (string exportedSymbolName in exportedSymbolNames)
                {
                    bool foundValue = behaviour.publicVariables.TryGetVariableValue(exportedSymbolName, out var variableValue);
                    bool foundType = behaviour.publicVariables.TryGetVariableType(exportedSymbolName, out var variableType);

                    // Remove this variable from the publicVariable list since UdonBehaviours set all null GameObjects, UdonBehaviours, and Transforms to the current behavior's equivalent object regardless of if it's marked as a `null` heap variable or `this`
                    // This default behavior is not the same as Unity, where the references are just left null. And more importantly, it assumes that the user has interacted with the inspector on that object at some point which cannot be guaranteed. 
                    // Specifically, if the user adds some public variable to a class, and multiple objects in the scene reference the program asset, 
                    //   the user will need to go through each of the objects' inspectors to make sure each UdonBehavior has its `publicVariables` variable populated by the inspector
                    if (foundValue && foundType &&
                        variableValue.IsUnityObjectNull() &&
                        (variableType == typeof(GameObject) || variableType == typeof(UdonBehaviour) || variableType == typeof(Transform)))
                    {
                        behaviour.publicVariables.RemoveVariable(exportedSymbolName);
                        GUI.changed = true;
                    }
                }
            }

            if (EditorGUI.EndChangeCheck() && PrefabUtility.IsPartOfPrefabInstance(behaviour))
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
        }

        // https://forum.unity.com/threads/horizontal-line-in-editor-window.520812/#post-3534861
        [PublicAPI]
        public static void DrawUILine(Color color, int thickness = 2, int padding = 4)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }

        [PublicAPI]
        public static void DrawUILine()
        {
            DrawUILine(Color.gray);
        }

        static FieldInfo serializedAssetField;
        
        internal static bool DrawProgramSource(UdonBehaviour behaviour, bool drawScript = true)
        {
            if (serializedAssetField == null)
                serializedAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);

            // Program source
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.BeginChangeCheck();
            AbstractUdonProgramSource newProgramSource = (AbstractUdonProgramSource)EditorGUILayout.ObjectField("Program Source", behaviour.programSource, typeof(AbstractUdonProgramSource), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(behaviour, "Change program source");
                behaviour.programSource = newProgramSource;
                serializedAssetField.SetValue(behaviour, newProgramSource != null ? newProgramSource.SerializedProgramAsset : null);
            }
            EditorGUI.EndDisabledGroup();

            if (((UdonSharpProgramAsset)behaviour.programSource).sourceCsScript == null)
            {
                if (UdonSharpGUI.DrawCreateScriptButton((UdonSharpProgramAsset)behaviour.programSource))
                {
                    EditorUtility.SetDirty(behaviour.programSource);
                }
                return true;
            }
            else if (drawScript)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("Program Script", ((UdonSharpProgramAsset)behaviour.programSource)?.sourceCsScript, typeof(MonoScript), false);
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
            }

            return false;
        }

        /// <summary>
        /// Draws the program asset field, and program source field optionally. Also handles drawing the create script button when the script on the program asset is null.
        /// Returns true if the rest of the inspector drawing should early out due to an empty program script
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="drawScript"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool DrawProgramSource(UnityEngine.Object target, bool drawScript = true)
        {
            UdonSharpBehaviour targetBehaviour = (UdonSharpBehaviour)target;

            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(targetBehaviour);

            if (backingBehaviour == null)
            {
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(targetBehaviour), typeof(MonoScript), false);

                EditorGUI.EndDisabledGroup();
                return false;
            }

            return DrawProgramSource(backingBehaviour, drawScript);
        }

        static readonly GUIContent ownershipTransferOnCollisionContent = new GUIContent("Allow Ownership Transfer on Collision",
                                                                                        "Transfer ownership on collision, requires a Collision component on the same game object");
        
        static MethodInfo dropdownButtonMethod;

        internal static void DrawSyncSettings(UdonBehaviour behaviour)
        {
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)behaviour.programSource;

            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            UdonBehaviour[] behavioursOnObject = behaviour.GetComponents<UdonBehaviour>();

            // Sanity checking for mixed sync modes
            if (behavioursOnObject.Length > 1)
            {
                bool hasContinuousSync = false;
                bool hasReliableSync = false;

                foreach (UdonBehaviour otherBehaviour in behavioursOnObject)
                {
                    if (otherBehaviour.programSource is UdonSharpProgramAsset otherBehaviourProgram && otherBehaviourProgram.behaviourSyncMode == BehaviourSyncMode.NoVariableSync)
                        continue;

                    if (otherBehaviour.SyncMethod == Networking.SyncType.Manual)
                        hasReliableSync = true;
                    else
                        hasContinuousSync = true;
                }

                if (hasContinuousSync && hasReliableSync)
                {
                    //if (programAsset.behaviourSyncMode == BehaviourSyncMode.NoVariableSync)
                    //    EditorGUILayout.HelpBox("NoVariableSync mode uses Continuous sync mode internally. You are mixing sync methods between UdonBehaviours on the same game object, this will cause all behaviours to use the sync method of the last component on the game object.", MessageType.Error);
                    //else
                    if (programAsset.behaviourSyncMode != BehaviourSyncMode.NoVariableSync)
                        EditorGUILayout.HelpBox("You are mixing sync methods between UdonBehaviours on the same game object, this will cause all behaviours to use the sync method of the last component on the game object.", MessageType.Error);
                }
            }

            // Dropdown for the sync settings
            if (programAsset.behaviourSyncMode != BehaviourSyncMode.NoVariableSync && programAsset.behaviourSyncMode != BehaviourSyncMode.None)
            {
                bool allowsSyncConfig = programAsset.behaviourSyncMode == BehaviourSyncMode.Any;

                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || !allowsSyncConfig);

                Rect syncMethodRect = EditorGUILayout.GetControlRect();
                int id = GUIUtility.GetControlID("DropdownButton".GetHashCode(), FocusType.Keyboard, syncMethodRect);
                GUIContent dropdownContent = allowsSyncConfig ? new GUIContent("Synchronization Method") : new GUIContent("Synchronization Method", "This sync mode is currently set by the UdonBehaviourSyncMode attribute on the script");

                Rect dropdownRect = EditorGUI.PrefixLabel(syncMethodRect, id, dropdownContent);

                if (dropdownButtonMethod == null)
                    dropdownButtonMethod = typeof(EditorGUI).GetMethod("DropdownButton", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(int), typeof(Rect), typeof(GUIContent), typeof(GUIStyle) }, null);

                string dropdownText;
                switch (behaviour.SyncMethod)
                {
                    case Networking.SyncType.Continuous:
                        dropdownText = "Continuous";
                        break;
                    case Networking.SyncType.Manual:
                        dropdownText = "Manual";
                        break;
                    default:
                        dropdownText = "None";
                        break;
                }
                
                if ((bool)dropdownButtonMethod.Invoke(null, new object[] { id, dropdownRect, new GUIContent(dropdownText), EditorStyles.miniPullDown }))
                {
                    SyncModeMenu.Show(syncMethodRect, new UdonBehaviour[] { behaviour });

                    GUIUtility.ExitGUI();
                }

                EditorGUI.EndDisabledGroup();

                bool newReliableState = behaviour.SyncMethod == Networking.SyncType.Manual;

                // Handle auto setting of sync mode if the component has just been created
                if (programAsset.behaviourSyncMode == BehaviourSyncMode.Continuous && behaviour.SyncMethod == Networking.SyncType.Manual)
                    newReliableState = false;
                else if (programAsset.behaviourSyncMode == BehaviourSyncMode.Manual && behaviour.SyncMethod != Networking.SyncType.Manual)
                    newReliableState = true;

                if (newReliableState != (behaviour.SyncMethod == Networking.SyncType.Manual))
                {
                    Undo.RecordObject(behaviour, "Update sync mode");
                    behaviour.SyncMethod = newReliableState ? Networking.SyncType.Manual : Networking.SyncType.Continuous;
                }
            }

            // Validate that we don't have a VRC Object Sync on continuous synced objects since it is not valid
            if (behaviour.Reliable)
            {
                var objSync = behaviour.GetComponent<VRC.SDK3.Components.VRCObjectSync>();

#pragma warning disable CS0618 // Type or member is obsolete
                if (behaviour.SynchronizePosition)
#pragma warning restore CS0618 // Type or member is obsolete
                    EditorGUILayout.HelpBox("Manual sync cannot be used on GameObjects with Position Sync", MessageType.Error);
                else if (objSync)
                    EditorGUILayout.HelpBox("Manual sync cannot be used on GameObjects with VRC Object Sync", MessageType.Error);
            }

            EditorGUI.BeginChangeCheck();

            // Position sync upgrade warnings & collision transfer handling
#pragma warning disable CS0618 // Type or member is obsolete
            // Force collision ownership transfer off on UdonBehaviours since it is no longer respected when used on UdonBehaviours.
            if (behaviour.AllowCollisionOwnershipTransfer)
            {
                behaviour.AllowCollisionOwnershipTransfer = false;
                GUI.changed = true;
            }

            // For now we'll do a warning, later on we may add a validation pass that just converts everything automatically
            if (behaviour.SynchronizePosition)
            {
                var objectSync = behaviour.GetComponent<VRC.SDK3.Components.VRCObjectSync>();

                if (!objectSync)
                {
                    EditorGUILayout.HelpBox("This behaviour has sync position enabled on it, sync position is deprecated and you should now use the VRC Object Sync script.", MessageType.Warning);
                    if (GUILayout.Button("Switch to VRC Object Sync"))
                    {
                        var newObjSync = Undo.AddComponent<VRC.SDK3.Components.VRCObjectSync>(behaviour.gameObject);
                        while (UnityEditorInternal.ComponentUtility.MoveComponentUp(newObjSync)) { }

                        UdonBehaviour[] behaviours = behaviour.GetComponents<UdonBehaviour>();
                        
                        foreach (UdonBehaviour otherBehaviour in behaviours)
                        {
                            Undo.RecordObject(behaviour, "Convert to VRC Object Sync");
                            behaviour.SynchronizePosition = false;
                            behaviour.AllowCollisionOwnershipTransfer = false;
                        }

                        Undo.RecordObject(newObjSync, "Object sync collision transfer");
                        newObjSync.AllowCollisionOwnershipTransfer = false;
                    }
                }
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (EditorGUI.EndChangeCheck())
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);

            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Draws the syncing settings for a behaviour, currently this is the position sync and collision ownership transfer check boxes, but will be updated when new Udon SDKs are released.
        /// </summary>
        /// <param name="target"></param>
        [PublicAPI]
        public static void DrawSyncSettings(UnityEngine.Object target)
        {
            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)target);

            if (backingBehaviour)
                DrawSyncSettings(backingBehaviour);
        }
        
        /// <summary>
        /// Draws the interact settings for UdonBehaviours, this is the interact text and proximity settings. These settings will only show if the script has an Interact() event.
        /// </summary>
        /// <param name="behaviour"></param>
        [PublicAPI]
        public static void DrawInteractSettings(UdonBehaviour behaviour)
        {
            if (((UdonSharpProgramAsset)behaviour.programSource).hasInteractEvent)
            {
                EditorGUI.BeginChangeCheck();
                string newInteractText = EditorGUILayout.TextField("Interaction Text", behaviour.interactText);
                float newProximity = EditorGUILayout.Slider("Proximity", behaviour.proximity, 0f, 100f);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(behaviour, "Change interact property");

                    behaviour.interactText = newInteractText;
                    behaviour.proximity = newProximity;

                    if (PrefabUtility.IsPartOfPrefabInstance(behaviour))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }

                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                if (GUILayout.Button("Trigger Interact"/*, GUILayout.Height(22f)*/))
                {
                    bool needsProxyCall = false;

                    UdonSharpBehaviour proxy = UdonSharpEditorUtility.FindProxyBehaviour(behaviour);

                    if (proxy)
                    {
                        System.Type inspectorType = UdonSharpCustomEditorManager.GetInspectorEditorType(proxy.GetType());
                        if (inspectorType != null)
                            needsProxyCall = true;
                    }

                    if (needsProxyCall)
                        UdonSharpEditorUtility.CopyProxyToUdon(proxy, ProxySerializationPolicy.All);
                    
                    behaviour.SendCustomEvent("_interact");

                    if (needsProxyCall)
                        UdonSharpEditorUtility.CopyUdonToProxy(proxy, ProxySerializationPolicy.All);
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        /// <summary>
        /// Draws the interact settings for UdonBehaviours, this is the interact text and proximity settings. These settings will only show if the script has an Interact() event.
        /// </summary>
        [PublicAPI]
        public static void DrawInteractSettings(UnityEngine.Object target)
        {
            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)target);

            if (backingBehaviour)
                DrawInteractSettings(backingBehaviour);
        }

        const string CONVERT_WARN_TEXT = "Udon Sharp Behaviours need to be converted to Udon Behaviours to work in game. Click the convert button below to automatically convert the script.";

        /// <summary>
        /// Draws the button to convert UdonSharpBehaviours to UdonBehaviours
        /// 
        /// Should be used as such in OnInspectorGUI to avoid errors:
        /// if (DrawConvertToUdonBehaviourButton(target)) return;
        /// </summary>
        /// <param name="targets"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool DrawConvertToUdonBehaviourButton(UnityEngine.Object target)
        {
            if (!(target is UdonSharpBehaviour behaviour))
                return false;

            if (!UdonSharpEditorUtility.IsProxyBehaviour(behaviour))
            {
                EditorGUILayout.HelpBox(CONVERT_WARN_TEXT, MessageType.Warning);

                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                if (GUILayout.Button("Convert to UdonBehaviour", GUILayout.Height(25)))
                {
                    UdonSharpEditorUtility.ConvertToUdonBehavioursInternal(new[] { behaviour }, true, true, true);
                    EditorGUI.EndDisabledGroup();

                    return true;
                }
                EditorGUI.EndDisabledGroup();
            }

            return false;
        }

        /// <summary>
        /// Draws the button to convert UdonSharpBehaviours to UdonBehaviours
        /// 
        /// Should be used as such in OnInspectorGUI to avoid errors:
        /// if (DrawConvertToUdonBehaviourButton(targets)) return;
        /// </summary>
        /// <param name="targets"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool DrawConvertToUdonBehaviourButton(UnityEngine.Object[] targets)
        {
            bool anyNotProxy = false;

            foreach (UnityEngine.Object target in targets)
            {
                UdonSharpBehaviour targetBehaviour = target as UdonSharpBehaviour;

                if (targetBehaviour == null)
                    continue;

                if (!UdonSharpEditorUtility.IsProxyBehaviour(targetBehaviour))
                {
                    anyNotProxy = true;
                    break;
                }
            }

            if (anyNotProxy)
            {
                EditorGUILayout.HelpBox(CONVERT_WARN_TEXT, MessageType.Warning);

                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                if (GUILayout.Button("Convert to UdonBehaviour", GUILayout.Height(25)))
                {
                    UdonSharpEditorUtility.ConvertToUdonBehavioursInternal(Array.ConvertAll(targets, e => e as UdonSharpBehaviour).Where(e => e != null && !UdonSharpEditorUtility.IsProxyBehaviour(e)).ToArray(), true, true, true);
                    EditorGUI.EndDisabledGroup();

                    return true;
                }
                EditorGUI.EndDisabledGroup();
            }

            return false;
        }

        /// <summary>
        /// Draws the default header for UdonSharpBehaviours, contains the script drawing, sync settings, interact settings, and utilities.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="skipLine"></param>
        /// <param name="drawScript"></param>
        /// <returns></returns>
        [PublicAPI]
        public static bool DrawDefaultUdonSharpBehaviourHeader(UnityEngine.Object target, bool skipLine = false , bool drawScript = true)
        {
            if (DrawConvertToUdonBehaviourButton(target)) return true;
            if (DrawProgramSource(target, drawScript)) return true;

            DrawSyncSettings(target);
            DrawInteractSettings(target);
            DrawUtilities(target);
            UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset((UdonSharpBehaviour)target);

            if (programAsset)
                DrawCompileErrorTextArea(programAsset);

            if (!skipLine)
                DrawUILine();

            return false;
        }
    }
}
