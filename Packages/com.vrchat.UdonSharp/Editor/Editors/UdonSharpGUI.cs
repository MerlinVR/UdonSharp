
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharp.Localization;
using UdonSharp.Updater;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Object = UnityEngine.Object;

namespace UdonSharpEditor
{
#region Sync mode menu editor
    internal class SyncModeMenu : EditorWindow
    {
        private static SyncModeMenu _menu;

        private UdonBehaviour[] udonBehaviours;
        private int selectedIdx = -1;

        private static GUIStyle _selectionStyle;
        private static GUIStyle _descriptionStyle;

        private static readonly List<(GUIContent, GUIContent)> _labels = new List<(GUIContent, GUIContent)>(new[] {
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

            for (int i = 0; i < _labels.Count; ++i)
            {
                DrawSelectionOption(_labels[i].Item1, _labels[i].Item2, i);
            }

            GUILayout.EndArea();
        }

        private void DrawSelectionOption(GUIContent title, GUIContent descriptor, int index)
        {
            bool elementsMatch = UdonSharpUtils.AllElementsMatch(udonBehaviours.Select(e => e.SyncMethod));
            
            EditorGUILayout.BeginHorizontal();

            GUIStyle checkboxStyle = new GUIStyle();
            checkboxStyle.padding.top = 5;
            checkboxStyle.padding.right = 0;
            checkboxStyle.margin.right = 0;

            if (elementsMatch)
                EditorGUILayout.LabelField(udonBehaviours[0].SyncMethod == (Networking.SyncType)(index + 1) ? "✔" : "", checkboxStyle, GUILayout.Width(10f));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(descriptor, _descriptionStyle);
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

            foreach (UdonBehaviour udonBehaviour in udonBehaviours)
            {
                if (udonBehaviour.SyncMethod != (Networking.SyncType)(selectedIdx + 1))
                {
                    Undo.RecordObject(udonBehaviour, "Change sync mode");
                    udonBehaviour.SyncMethod = (Networking.SyncType)(selectedIdx + 1);

                    PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                }
            }

            Close();
            GUIUtility.ExitGUI();
        }

        private static GUIStyle _outlineStyle;

        private static void DrawSelectionOutline(Rect rect)
        {
            if (_outlineStyle == null)
            {
                Texture2D clearColorDarkTex = new Texture2D(1, 1);
                clearColorDarkTex.SetPixel(0, 0, new Color32(64, 128, 223, 255));
                clearColorDarkTex.Apply();

                _outlineStyle = new GUIStyle();
                _outlineStyle.normal.background = clearColorDarkTex;
            }

            const float outlineWidth = 2f;

            GUI.Box(new Rect(rect.x, rect.y, rect.width, outlineWidth), GUIContent.none, _outlineStyle);
            GUI.Box(new Rect(rect.x - outlineWidth, rect.y, outlineWidth, rect.height + outlineWidth + 1f), GUIContent.none, _outlineStyle);
            GUI.Box(new Rect(rect.x + rect.width, rect.y, outlineWidth, rect.height + outlineWidth + 1f), GUIContent.none, _outlineStyle);
            GUI.Box(new Rect(rect.x - outlineWidth, rect.y + rect.height + outlineWidth + 1f, rect.width + outlineWidth * 2f, outlineWidth), GUIContent.none, _outlineStyle);
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
            if (_selectionStyle == null || _descriptionStyle == null)
            {
                _selectionStyle = new GUIStyle(EditorStyles.helpBox);
                _selectionStyle.font = EditorStyles.label.font;
                _selectionStyle.fontSize = EditorStyles.label.fontSize;

                _descriptionStyle = new GUIStyle(EditorStyles.label);
                _descriptionStyle.wordWrap = true;
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

            _menu = CreateInstance<SyncModeMenu>();
            _menu.udonBehaviours = behaviours;
            _menu.wantsMouseMove = true;
            //menu.ShowAsDropDown(controlRect, dropdownSize);

            _menu.ShowDropDown(controlRect, dropdownSize);
        }

        private static Vector2 CalculateDropdownSize(Rect controlRect)
        {
            Rect areaRect = GetAreaRect(controlRect);
            areaRect.width -= 30f; // Checkbox width

            float totalHeight = 0f;

            for (int i = 0; i < _labels.Count; ++i)
            {
                totalHeight += EditorStyles.boldLabel.CalcHeight(_labels[i].Item1, areaRect.width);
                totalHeight += 13f; // Space()
                totalHeight += _descriptionStyle.CalcHeight(_labels[i].Item2, areaRect.width);
                totalHeight += _selectionStyle.margin.vertical;
                totalHeight += _selectionStyle.padding.vertical;
            }
            
            totalHeight += EditorStyles.textArea.margin.vertical;

            return new Vector2(controlRect.width, totalHeight);
        }

        private static Array _popupLocationArray;

        private void ShowDropDown(Rect controlRect, Vector2 size)
        {
            if (_popupLocationArray == null)
            {
                Type popupLocationType = typeof(Editor).Assembly.GetType("UnityEditor.PopupLocation");

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
        private static GUIStyle _errorTextStyle;
        private static GUIStyle _undoLabelStyle;
        private static GUIContent _undoArrowLight;
        private static GUIContent _undoArrowDark;
        private static GUIContent _undoArrowContent;
        private static Texture2D _clearColorLight;
        private static Texture2D _clearColorDark;
        private static GUIStyle _clearColorStyle;

        /// <summary>
        /// Draws compile errors if there are any, shows nothing if there are no compile errors
        /// </summary>
        [PublicAPI]
        public static void DrawCompileErrorTextArea()
        {
            if (!UdonSharpEditorCache.Instance.HasUdonSharpCompileError()) 
                return;
            
            if (_errorTextStyle == null)
            {
                _errorTextStyle = new GUIStyle(EditorStyles.textArea);
                _errorTextStyle.normal.textColor = new Color32(211, 34, 34, 255);
                _errorTextStyle.focused.textColor = _errorTextStyle.normal.textColor;
            }
                
            StringBuilder errorStrBuilder = new StringBuilder();

            foreach (UdonSharpEditorCache.CompileDiagnostic diagnostic in UdonSharpEditorCache.Instance.LastCompileDiagnostics)
            {
                if (diagnostic.severity == DiagnosticSeverity.Error)
                {
                    if (diagnostic.file.IsNullOrWhitespace())
                        errorStrBuilder.Append($"{diagnostic.message}\n");
                    else
                        errorStrBuilder.Append($"{diagnostic.file}({diagnostic.line},{diagnostic.character}): {diagnostic.message}\n");
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"U# Compile Errors", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(errorStrBuilder.ToString().TrimEnd('\n'), _errorTextStyle);
        }

        private static void SetupGUI()
        {
            if (_undoLabelStyle == null ||
                _undoArrowLight == null ||
                _undoArrowDark == null ||
                _clearColorLight == null ||
                _clearColorDark == null ||
                _clearColorStyle == null)
            {
                _undoLabelStyle = new GUIStyle(EditorStyles.label);
                _undoLabelStyle.alignment = TextAnchor.MiddleCenter;
                _undoLabelStyle.padding = new RectOffset(0, 0, 1, 0);
                _undoLabelStyle.margin = new RectOffset(0, 0, 0, 0);
                _undoLabelStyle.border = new RectOffset(0, 0, 0, 0);
                _undoLabelStyle.stretchWidth = false;
                _undoLabelStyle.stretchHeight = false;

                _undoArrowLight = new GUIContent((Texture)EditorGUIUtility.Load(Path.Combine(UdonSharpLocator.ResourcesPath, "UndoArrowLight.png")), "Reset to default value");
                _undoArrowDark = new GUIContent((Texture)EditorGUIUtility.Load(Path.Combine(UdonSharpLocator.ResourcesPath, "UndoArrowBlack.png")), "Reset to default value");

                Texture2D clearColorLightTex = new Texture2D(1, 1);
                clearColorLightTex.SetPixel(0, 0, new Color32(194, 194, 194, 255));
                clearColorLightTex.Apply();

                _clearColorLight = clearColorLightTex;

                Texture2D clearColorDarkTex = new Texture2D(1, 1);
                clearColorDarkTex.SetPixel(0, 0, new Color32(56, 56, 56, 255));
                clearColorDarkTex.Apply();

                _clearColorDark = clearColorDarkTex;

                _clearColorStyle = new GUIStyle();
            }

            _undoArrowContent = EditorGUIUtility.isProSkin ? _undoArrowLight : _undoArrowDark;
            _clearColorStyle.normal.background = EditorGUIUtility.isProSkin ? _clearColorDark : _clearColorLight;
        }

        private class USharpEditorState
        {
            public bool ShowExtraOptions;
            public bool ShowProgramUasm;
            public bool ShowProgramDisassembly;
            public string CustomEventName = "";
        }

        private static Dictionary<UdonSharpProgramAsset, USharpEditorState> _editorStates = new Dictionary<UdonSharpProgramAsset, USharpEditorState>();
        private static USharpEditorState GetEditorState(UdonSharpProgramAsset programAsset)
        {
            if (_editorStates.TryGetValue(programAsset, out var editorState)) 
                return editorState;
            
            editorState = new USharpEditorState { ShowExtraOptions = programAsset.showUtilityDropdown };
            _editorStates.Add(programAsset, editorState);

            return editorState;
        }
        
        internal static void DrawUtilities(UdonBehaviour[] udonBehaviours)
        {
            if (!UdonSharpUtils.AllElementsMatch(udonBehaviours.Select(e => e.programSource)))
            {
                EditorGUILayout.LabelField("Cannot draw utilities, program sources for selected UdonBehaviours do not match");
                return;
            }
            
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)udonBehaviours[0].programSource;
            USharpEditorState editorState = GetEditorState(programAsset);

            editorState.ShowExtraOptions = programAsset.showUtilityDropdown = EditorGUILayout.Foldout(editorState.ShowExtraOptions, "Utilities", true);
            
            if (editorState.ShowExtraOptions)
            {
                if (GUILayout.Button(Loc.Get(LocStr.UI_CompileAllPrograms)))
                {
                    UdonSharpProgramAsset.CompileAllCsPrograms(true);
                }

                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

                if (GUILayout.Button(Loc.Get(LocStr.UI_SendCustomEvent)))
                {
                    foreach (UdonBehaviour behaviour in udonBehaviours)
                    {
                        UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);
                    
                        UdonSharpEditorUtility.CopyProxyToUdon(proxy, ProxySerializationPolicy.All);
                    
                        behaviour.SendCustomEvent(editorState.CustomEventName);

                        UdonSharpEditorUtility.CopyUdonToProxy(proxy, ProxySerializationPolicy.All);
                    }
                }

                editorState.CustomEventName = EditorGUILayout.TextField("Event Name:", editorState.CustomEventName);

                EditorGUI.EndDisabledGroup();
            }
        }
        
        internal static void DrawUtilities(UdonSharpProgramAsset programAsset)
        {
            USharpEditorState editorState = GetEditorState(programAsset);

            if (GUILayout.Button(Loc.Get(LocStr.UI_CompileAllPrograms)))
                UdonSharpProgramAsset.CompileAllCsPrograms(true);

            EditorGUILayout.Space();

            editorState.ShowProgramUasm = EditorGUILayout.Foldout(editorState.ShowProgramUasm, "Compiled C# Udon Assembly", true);
            if (editorState.ShowProgramUasm)
            {
                programAsset.DrawAssemblyText();
            }
            
            if (programAsset.GetRealProgram() == null)
                programAsset.UpdateProgram();

            if (programAsset.GetRealProgram() != null)
            {
                editorState.ShowProgramDisassembly = EditorGUILayout.Foldout(editorState.ShowProgramDisassembly, "Program Disassembly", true);
                if (editorState.ShowProgramDisassembly)
                    programAsset.DrawProgramDisassembly();
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
                DrawUtilities(new UdonBehaviour[] { backingBehaviour });
            }
        }
        
        [PublicAPI]
        public static void DrawUtilities(UnityEngine.Object[] targets)
        {
            DrawUtilities(targets.Select(e => UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)e)).ToArray());
        }

    #region Default UdonSharpBehaviour drawing
        internal static bool DrawCreateScriptButton(UdonSharpProgramAsset programAsset)
        {
            if (GUILayout.Button("Create Script"))
            {
                string thisPath = AssetDatabase.GetAssetPath(programAsset);
                string fileName = Path.GetFileNameWithoutExtension(thisPath).Replace(" Udon C# Program Asset", "").Replace(" ", "").Replace("#", "Sharp");
                string chosenFilePath = EditorUtility.SaveFilePanel("Save UdonSharp File", Path.GetDirectoryName(thisPath), fileName, "cs");

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

        // private static Type _currentUserScript;
        // private static UnityEngine.Object ValidateObjectReference(UnityEngine.Object[] references, System.Type objType, SerializedProperty property, Enum options = null)
        // {
        //     if (property != null)
        //         throw new System.ArgumentException("Serialized property on validate object reference should be null!");
        //
        //     if (_currentUserScript != null ||
        //         objType == typeof(UdonBehaviour) ||
        //         objType == typeof(UdonSharpBehaviour))
        //     {
        //         foreach (UnityEngine.Object reference in references)
        //         {
        //             GameObject referenceObject = reference as GameObject;
        //             UdonBehaviour referenceBehaviour = reference as UdonBehaviour;
        //
        //             if (referenceObject != null)
        //             {
        //                 UdonBehaviour[] components = referenceObject.GetComponents<UdonBehaviour>();
        //
        //                 foreach (UdonBehaviour component in components)
        //                 {
        //                     var foundComponent = ValidateObjectReference(new UnityEngine.Object[] { component }, objType, null, UdonSyncMode.NotSynced /* just any enum, we don't care */) as UdonBehaviour;
        //
        //                     if (foundComponent != null)
        //                     {
        //                         return foundComponent;
        //                     }
        //                 }
        //             }
        //             else if (referenceBehaviour != null)
        //             {
        //                 if (referenceBehaviour.programSource != null &&
        //                     referenceBehaviour.programSource is UdonSharpProgramAsset udonSharpProgram &&
        //                     udonSharpProgram.sourceCsScript != null)
        //                 {
        //                     if (_currentUserScript == null || // If this is null, the field is referencing a generic UdonBehaviour or UdonSharpBehaviour instead of a behaviour of a certain type that inherits from UdonSharpBehaviour.
        //                         udonSharpProgram.sourceCsScript.GetClass() == _currentUserScript)
        //                         return referenceBehaviour;
        //                 }
        //             }
        //         }
        //     }
        //     else
        //     {
        //         // Fallback to default handling if the user has not compiled with the new info
        //         if (references[0] != null && references[0] is GameObject && typeof(Component).IsAssignableFrom(objType))
        //         {
        //             GameObject gameObject = (GameObject)references[0];
        //             references = gameObject.GetComponents(typeof(Component));
        //         }
        //         foreach (UnityEngine.Object component in references)
        //         {
        //             if (component != null && objType.IsInstanceOfType(component))
        //             {
        //                 return component;
        //             }
        //         }
        //     }
        //
        //     return null;
        // }
        
        private static Dictionary<Object, Dictionary<string, bool>> _foldoutStates = new Dictionary<Object, Dictionary<string, bool>>();

        private static bool GetFoldoutState(Object behaviour, string foldoutIdentifier)
        {
            if (behaviour == null)
                return false;

            if (!_foldoutStates.TryGetValue(behaviour, out Dictionary<string, bool> foldoutDict))
                return false;

            if (!foldoutDict.TryGetValue(foldoutIdentifier, out bool foldoutState))
                return false;

            return foldoutState;
        }

        private static void SetFoldoutState(Object behaviour, string foldoutIdentifier, bool value)
        {
            if (behaviour == null)
                return;

            if (!_foldoutStates.TryGetValue(behaviour, out var foldoutDict))
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

        internal static object DrawFieldForType(Object sourceBehaviour, string fieldName, string symbol, object value, Type type, FieldInfo symbolField)
        {
            if (fieldName == null)
                fieldName = ObjectNames.NicifyVariableName(symbol);

            TooltipAttribute tooltip = symbolField.GetAttribute<TooltipAttribute>();

            GUIContent fieldLabel = tooltip != null ? new GUIContent(fieldName, tooltip.tooltip) : new GUIContent(fieldName);

            if (type.IsArray)
            {
                bool foldoutEnabled = GetFoldoutState(sourceBehaviour, symbol);

                // Event tempEvent = new Event(Event.current);

                Rect foldoutRect = EditorGUILayout.GetControlRect();
                foldoutEnabled = EditorGUI.Foldout(foldoutRect, foldoutEnabled, fieldLabel, true);

                SetFoldoutState(sourceBehaviour, symbol, foldoutEnabled);

                // switch (tempEvent.type)
                // {
                //     case EventType.DragExited:
                //         if (GUI.enabled)
                //             HandleUtility.Repaint();
                //         break;
                //     case EventType.DragUpdated:
                //     case EventType.DragPerform:
                //         if (foldoutRect.Contains(tempEvent.mousePosition) && GUI.enabled)
                //         {
                //             int foldoutId = (int)typeof(EditorGUIUtility).GetField("s_LastControlID", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                //
                //             UnityEngine.Object[] references = DragAndDrop.objectReferences;
                //             UnityEngine.Object[] objArray = new UnityEngine.Object[1];
                //
                //             bool acceptedDrag = false;
                //
                //             List<UnityEngine.Object> draggedReferences = new List<UnityEngine.Object>();
                //
                //             _currentUserScript = symbolField.FieldType;
                //             foreach (UnityEngine.Object obj in references)
                //             {
                //                 objArray[0] = obj;
                //                 UnityEngine.Object validatedObject = ValidateObjectReference(objArray, type.GetElementType(), null);
                //
                //                 if (validatedObject != null)
                //                 {
                //                     DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                //
                //                     if (tempEvent.type == EventType.DragPerform)
                //                     {
                //                         draggedReferences.Add(validatedObject);
                //                         acceptedDrag = true;
                //                         DragAndDrop.activeControlID = 0;
                //                     }
                //                     else
                //                     {
                //                         DragAndDrop.activeControlID = foldoutId;
                //                     }
                //                 }
                //             }
                //             _currentUserScript = null;
                //
                //             if (acceptedDrag)
                //             {
                //                 Array oldArray = (Array)value;
                //
                //                 Array newArray = Activator.CreateInstance(type, oldArray.Length + draggedReferences.Count) as Array;
                //                 Array.Copy(oldArray, newArray, oldArray.Length);
                //                 Array.Copy(draggedReferences.ToArray(), 0, newArray, oldArray.Length, draggedReferences.Count);
                //
                //                 GUI.changed = true;
                //                 Event.current.Use();
                //                 DragAndDrop.AcceptDrag();
                //
                //                 return newArray;
                //             }
                //         }
                //
                //         break;
                // }

                if (foldoutEnabled)
                {
                    Type elementType = type.GetElementType();

                    if (value == null)
                    {
                        GUI.changed = true;
                        return Activator.CreateInstance(type, 0);
                    }

                    EditorGUI.indentLevel++;

                    Array valueArray = value as Array;

                    using (new EditorGUILayout.VerticalScope())
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
                            Array newArray = Activator.CreateInstance(type, newLength) as Array;

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
                            EditorGUI.BeginChangeCheck();
                            object newArrayVal = DrawFieldForType(sourceBehaviour, $"Element {i}", $"{symbol}_element{i}", valueArray.GetValue(i), elementType, symbolField);

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
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return EditorGUILayout.ObjectField(fieldLabel, (Object)value, type, true);
            }
            else if (type == typeof(string))
            {
                TextAreaAttribute textArea = symbolField?.GetAttribute<TextAreaAttribute>();

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
            else if (type == typeof(float))
            {
                RangeAttribute range = symbolField?.GetAttribute<RangeAttribute>();

                if (range != null)
                    return EditorGUILayout.Slider(fieldLabel, (float?)value ?? default, range.min, range.max);
                
                return EditorGUILayout.FloatField(fieldLabel, (float?)value ?? default);
            }
            else if (type == typeof(double))
            {
                RangeAttribute range = symbolField?.GetAttribute<RangeAttribute>();

                if (range != null)
                    return EditorGUILayout.Slider(fieldLabel, (float)((double?)value ?? default), range.min, range.max);
                    
                return EditorGUILayout.DoubleField(fieldLabel, (double?)value ?? default);
            }
            else if (type == typeof(int))
            {
                RangeAttribute range = symbolField?.GetAttribute<RangeAttribute>();

                if (range != null)
                    return EditorGUILayout.IntSlider(fieldLabel, (int?)value ?? default, (int)range.min, (int)range.max);
                    
                return EditorGUILayout.IntField(fieldLabel, (int?)value ?? default);
            }
            else if (type == typeof(bool))
            {
                return EditorGUILayout.Toggle(fieldLabel, (bool?)value ?? default);
            }
            else if (type == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(fieldLabel, (Vector2?)value ?? default);
            }
            else if (type == typeof(Vector2Int))
            {
                return EditorGUILayout.Vector2IntField(fieldLabel, (Vector2Int?)value ?? default);
            }
            else if (type == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(fieldLabel, (Vector3?)value ?? default);
            }
            else if (type == typeof(Vector3Int))
            {
                return EditorGUILayout.Vector3IntField(fieldLabel, (Vector3Int?)value ?? default);
            }
            else if (type == typeof(Vector4))
            {
                return EditorGUILayout.Vector4Field(fieldLabel, (Vector4?)value ?? default);
            }
            else if (type == typeof(Color))
            {
                ColorUsageAttribute colorUsage = symbolField?.GetAttribute<ColorUsageAttribute>();

                if (colorUsage != null)
                    return EditorGUILayout.ColorField(fieldLabel, (Color?)value ?? default, false, colorUsage.showAlpha, colorUsage.hdr);
                
                return EditorGUILayout.ColorField(fieldLabel, (Color?)value ?? default);
            }
            else if (type == typeof(Color32))
            {
                ColorUsageAttribute colorUsage = symbolField?.GetAttribute<ColorUsageAttribute>();

                if (colorUsage != null)
                    return (Color32)EditorGUILayout.ColorField(fieldLabel, (Color32?)value ?? default, false, colorUsage.showAlpha, false);
                
                return (Color32)EditorGUILayout.ColorField(fieldLabel, (Color32?)value ?? default);
            }
            else if (type == typeof(Quaternion))
            {
                Quaternion quatVal = (Quaternion?)value ?? default;
                Vector4 newQuat = EditorGUILayout.Vector4Field(fieldLabel, new Vector4(quatVal.x, quatVal.y, quatVal.z, quatVal.w));
                return new Quaternion(newQuat.x, newQuat.y, newQuat.z, newQuat.w);
            }
            else if (type == typeof(Bounds))
            {
                return EditorGUILayout.BoundsField(fieldLabel, (Bounds?)value ?? default);
            }
            else if (type == typeof(BoundsInt))
            {
                return EditorGUILayout.BoundsIntField(fieldLabel, (BoundsInt?)value ?? default);
            }
            else if (type == typeof(ParticleSystem.MinMaxCurve))
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
            else if (type == typeof(LayerMask))
            {
                return (LayerMask)EditorGUILayout.MaskField(fieldLabel, (LayerMask?)value ?? default, Enumerable.Range(0, 32).Select(e => LayerMask.LayerToName(e).Length > 0 ? e + ": " + LayerMask.LayerToName(e) : "").ToArray());
            }
            else if (type.IsEnum)
            {
                return EditorGUILayout.EnumPopup(fieldLabel, (Enum)(value ?? Activator.CreateInstance(type)));
            }
            else if (type == typeof(System.Type))
            {
                string typeName = value != null ? ((Type)value).FullName : "null";
                EditorGUILayout.LabelField(fieldLabel, typeName);
            }
            else if (type == typeof(Gradient))
            {
                GradientUsageAttribute gradientUsage = symbolField?.GetAttribute<GradientUsageAttribute>();

                if (value == null)
                {
                    value = new Gradient();
                    GUI.changed = true;
                }

                if (gradientUsage != null)
                    return EditorGUILayout.GradientField(fieldLabel, (Gradient)value, gradientUsage.hdr);
                
                return EditorGUILayout.GradientField(fieldLabel, (Gradient)value);
            }
            else if (type == typeof(AnimationCurve))
            {
                return EditorGUILayout.CurveField(fieldLabel, (AnimationCurve)value ?? new AnimationCurve());
            }
            else if (type == typeof(char))
            {
                string stringVal = EditorGUILayout.TextField(fieldLabel, (((char?)value) ?? default).ToString());
                if (stringVal.Length > 0)
                    return stringVal[0];
                
                return (char?)value ?? default;
            }
            else if (type == typeof(uint))
            {
                return (uint)Math.Min(Math.Max(EditorGUILayout.LongField(fieldLabel, (uint?)value ?? default), uint.MinValue), uint.MaxValue);
            }
            else if (type == typeof(long))
            {
                return EditorGUILayout.LongField(fieldLabel, (long?)value ?? default);
            }
            else if (type == typeof(byte))
            {
                return (byte)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (byte?)value ?? default), byte.MinValue, byte.MaxValue);
            }
            else if (type == typeof(sbyte))
            {
                return (sbyte)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (sbyte?)value ?? default), sbyte.MinValue, sbyte.MaxValue);
            }
            else if (type == typeof(short))
            {
                return (short)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (short?)value ?? default), short.MinValue, short.MaxValue);
            }
            else if (type == typeof(ushort))
            {
                return (ushort)Mathf.Clamp(EditorGUILayout.IntField(fieldLabel, (ushort?)value ?? default), ushort.MinValue, ushort.MaxValue);
            }
            else if (type == typeof(Rect))
            {
                return EditorGUILayout.RectField(fieldLabel, (Rect?)value ?? default);
            }
            else if (type == typeof(RectInt))
            {
                return EditorGUILayout.RectIntField(fieldLabel, (RectInt?)value ?? default);
            }
            else if (type == typeof(VRC.SDKBase.VRCUrl))
            {
                VRC.SDKBase.VRCUrl url = (VRC.SDKBase.VRCUrl)value ?? new VRC.SDKBase.VRCUrl("");
                url = new VRC.SDKBase.VRCUrl(EditorGUILayout.TextField(fieldLabel, url.Get()));
                return url;
            }
            else
            {
                EditorGUILayout.LabelField($"{fieldName}: no drawer for type {type}");

                return value;
            }

            return value;
        }
    #endregion

        /// <summary>
        /// The default drawing for UdonSharpBehaviour public variables
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="programAsset"></param>
        /// <param name="dirty"></param>
        [Obsolete("DrawPublicVariables using UdonBehaviour is obsolete in U# >=1.0, use DrawVariables instead", true)]
        public static void DrawPublicVariables(UdonBehaviour behaviour, UdonSharpProgramAsset programAsset, ref bool dirty) 
        { }

        /// <summary>
        /// Draws serialized variables on a behaviour
        /// </summary>
        /// <param name="behaviour">Any MonoBehaviour if a UdonBehaviour is passed, will try and get the UdonSharpBehaviour proxy to draw.</param>
        [PublicAPI]
        public static void DrawVariables(Object behaviour)
        {
            DrawVariables(new [] { behaviour });
        }
        
        /// <summary>
        /// Draws serialized variables on a behaviour
        /// </summary>
        /// <param name="behaviours">Any MonoBehaviour if a UdonBehaviour is passed, will try and get the UdonSharpBehaviour proxy to draw.</param>
        [PublicAPI]
        public static void DrawVariables(Object[] behaviours)
        {
            Object[] behaviourArrCopy = new Object[behaviours.Length];

            for (int i = 0; i < behaviours.Length; ++i)
            {
                Object behaviour = behaviours[i];
                behaviourArrCopy[i] = behaviours[i];
                
                if (!(behaviours[i] is MonoBehaviour))
                    throw new ArgumentException(nameof(behaviours));
                
                if (behaviour is UdonBehaviour udonBehaviour)
                {
                    if (UdonSharpEditorUtility.IsUdonSharpBehaviour(udonBehaviour))
                    {
                        behaviourArrCopy[i] = UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour);
                    }
                    else
                    {
                        throw new NotSupportedException("Cannot call DrawVariables on a non-UdonSharpBehaviour");
                    }
                }
            }

            SerializedObject serializedObject = new SerializedObject(behaviourArrCopy);
            
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

            serializedObject.ApplyModifiedProperties();
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

        private static FieldInfo _serializedAssetField;
        
        internal static bool DrawProgramSource(UdonBehaviour behaviour, bool drawScript = true)
        {
            if (_serializedAssetField == null)
                _serializedAssetField = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);

            // Program source
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.BeginChangeCheck();
            AbstractUdonProgramSource newProgramSource = (AbstractUdonProgramSource)EditorGUILayout.ObjectField(Loc.Get(LocStr.UI_ProgramSource), behaviour.programSource, typeof(AbstractUdonProgramSource), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(behaviour, "Change program source");
                behaviour.programSource = newProgramSource;
                _serializedAssetField.SetValue(behaviour, newProgramSource != null ? newProgramSource.SerializedProgramAsset : null);
            }
            EditorGUI.EndDisabledGroup();

            // if (((UdonSharpProgramAsset)behaviour.programSource)?.sourceCsScript == null)
            // {
            //     if (UdonSharpGUI.DrawCreateScriptButton((UdonSharpProgramAsset)behaviour.programSource))
            //     {
            //         EditorUtility.SetDirty(behaviour.programSource);
            //     }
            //     return true;
            // }
            
            if (drawScript)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField(Loc.Get(LocStr.UI_ProgramScript), ((UdonSharpProgramAsset)behaviour.programSource)?.sourceCsScript, typeof(MonoScript), false);
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
            }

            return false;
        }

        /// <summary>
        /// Draws the program asset field, and program source field optionally. Also handles drawing the create script button when the script on the program asset is null.
        /// Returns true if the rest of the inspector drawing should early out due to an empty program script
        /// </summary>
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

        [PublicAPI]
        public static bool DrawProgramSource(UnityEngine.Object[] targets, bool drawScript = true)
        {
            if (targets.Length == 0)
                throw new ArgumentException("Targets array cannot be empty");

            UdonSharpProgramAsset currentSource = null;
            
            foreach (Object target in targets)
            {
                if (target == null)
                    throw new ArgumentException("Target cannot be null");
                
                if (!(target is UdonSharpBehaviour udonSharpBehaviour))
                    throw new ArgumentException($"Target {target} is not an UdonSharpBehaviour");

                UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(udonSharpBehaviour);
                UdonSharpProgramAsset behaviourSource = (UdonSharpProgramAsset)backingBehaviour.programSource;
                
                if (currentSource != null && currentSource != behaviourSource)
                {
                    EditorGUILayout.LabelField("Program source references do not match between selected behaviours");
                    return false;
                }

                currentSource = behaviourSource;
            }

            return DrawProgramSource( UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)targets[0]), drawScript);
        }

        private static MethodInfo _dropdownButtonMethod;

        private static void DrawSyncSettings(UdonBehaviour[] behaviours)
        {
            if (!UdonSharpUtils.AllElementsMatch(behaviours.Select(e => e.programSource)))
            {
                EditorGUILayout.LabelField("Cannot draw sync settings, program sources for selected UdonBehaviours do not match");
                return;
            }
            
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)behaviours[0].programSource;

            bool hasConflictingSyncError = false;
            bool hasPosSyncError = false;
            bool hasObjSyncError = false;

            foreach (UdonBehaviour behaviour in behaviours)
            {
                UdonBehaviour[] behavioursOnObject = behaviour.GetComponents<UdonBehaviour>();

                // Sanity checking for mixed sync modes
                if (behavioursOnObject.Length <= 1) 
                    continue;
                
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
                    if (programAsset.behaviourSyncMode != BehaviourSyncMode.NoVariableSync)
                    {
                        hasConflictingSyncError = true;
                    }
                }
                
            #pragma warning disable 618
                // Validate that we don't have a VRC Object Sync on continuous synced objects since it is not valid
                if (!behaviour.Reliable) 
                    continue;
                
                VRCObjectSync objSync = behaviour.GetComponent<VRCObjectSync>();

                if (behaviour.SynchronizePosition)
                    hasPosSyncError = true;
                else if (objSync)
                    hasObjSyncError = true;
            #pragma warning restore 618
            }
            
            if (hasConflictingSyncError)
                EditorGUILayout.HelpBox("You are mixing sync methods between UdonBehaviours on the same game object, this will cause all behaviours to use the sync method of the last component on the game object.", MessageType.Error);
            
            if (hasPosSyncError)
                EditorGUILayout.HelpBox("Manual sync cannot be used on GameObjects with Position Sync", MessageType.Error);
            
            if (hasObjSyncError)
                EditorGUILayout.HelpBox("Manual sync cannot be used on GameObjects with VRC Object Sync", MessageType.Error);

            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            // Dropdown for the sync settings
            if (programAsset.behaviourSyncMode != BehaviourSyncMode.NoVariableSync && programAsset.behaviourSyncMode != BehaviourSyncMode.None)
            {
                bool allowsSyncConfig = programAsset.behaviourSyncMode == BehaviourSyncMode.Any;

                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying || !allowsSyncConfig);

                Rect syncMethodRect = EditorGUILayout.GetControlRect();
                int id = GUIUtility.GetControlID("DropdownButton".GetHashCode(), FocusType.Keyboard, syncMethodRect);
                GUIContent dropdownContent = allowsSyncConfig ? new GUIContent("Synchronization Method") : new GUIContent("Synchronization Method", "This sync mode is currently set by the UdonBehaviourSyncMode attribute on the script");

                Rect dropdownRect = EditorGUI.PrefixLabel(syncMethodRect, id, dropdownContent);

                if (_dropdownButtonMethod == null)
                    _dropdownButtonMethod = typeof(EditorGUI).GetMethod("DropdownButton", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(int), typeof(Rect), typeof(GUIContent), typeof(GUIStyle) }, null);

                string dropdownText = null;

                foreach (UdonBehaviour behaviour in behaviours)
                {
                    string newText;
                    
                    switch (behaviour.SyncMethod)
                    {
                        case Networking.SyncType.Continuous:
                            newText = "Continuous";
                            break;
                        case Networking.SyncType.Manual:
                            newText = "Manual";
                            break;
                        default:
                            newText = "None";
                            break;
                    }
                    
                    // Different selected values, just use dashes like Unity's multi edit
                    if (dropdownText != null && newText != dropdownText)
                    {
                        dropdownText = "-";
                        break;
                    }

                    dropdownText = newText;
                }
                
                if ((bool)_dropdownButtonMethod.Invoke(null, new object[] { id, dropdownRect, new GUIContent(dropdownText), EditorStyles.miniPullDown }))
                {
                    SyncModeMenu.Show(syncMethodRect, behaviours);

                    GUIUtility.ExitGUI();
                }

                EditorGUI.EndDisabledGroup();
                
                // Handle auto setting of sync mode if the component has just been created
                foreach (UdonBehaviour behaviour in behaviours)
                {
                    bool newReliableState = behaviour.SyncMethod == Networking.SyncType.Manual;
                    
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
            }

            EditorGUI.BeginChangeCheck();

            // Position sync upgrade warnings & collision transfer handling
        #pragma warning disable CS0618 // Type or member is obsolete
            // Force collision ownership transfer off on UdonBehaviours since it is no longer respected when used on UdonBehaviours.
            foreach (UdonBehaviour behaviour in behaviours)
            {
                if (behaviour.AllowCollisionOwnershipTransfer)
                {
                    behaviour.AllowCollisionOwnershipTransfer = false;
                    GUI.changed = true;
                }
            }

            // For now we'll do a warning, later on we may add a validation pass that just converts everything automatically
            bool needsObjectSyncConversion = false;
            foreach (UdonBehaviour behaviour in behaviours)
            {
                if (behaviour.SynchronizePosition && 
                    behaviour.GetComponent<VRCObjectSync>() == null)
                {
                    needsObjectSyncConversion = true;
                    break;
                }
            }

            bool userRequestedConversion = false;
            
            if (needsObjectSyncConversion)
            {
                EditorGUILayout.HelpBox("This behaviour has sync position enabled on it, sync position is deprecated and you should now use the VRC Object Sync script.", MessageType.Warning);
                if (GUILayout.Button("Switch to VRC Object Sync"))
                {
                    userRequestedConversion = true;
                }
            }

            if (userRequestedConversion)
            {
                foreach (UdonBehaviour behaviour in behaviours)
                {
                    if (behaviour.GetComponent<VRCObjectSync>())
                        continue;
                    
                    VRCObjectSync newObjSync = Undo.AddComponent<VRCObjectSync>(behaviour.gameObject);
                    while (UnityEditorInternal.ComponentUtility.MoveComponentUp(newObjSync)) { }

                    UdonBehaviour[] otherBehaviours = behaviour.GetComponents<UdonBehaviour>();
                    
                    foreach (UdonBehaviour otherBehaviour in otherBehaviours)
                    {
                        Undo.RecordObject(behaviour, "Convert to VRC Object Sync");
                        otherBehaviour.SynchronizePosition = false;
                        otherBehaviour.AllowCollisionOwnershipTransfer = false;
                    }

                    Undo.RecordObject(newObjSync, "Object sync collision transfer");
                    newObjSync.AllowCollisionOwnershipTransfer = false;
                }
            }
            
        #pragma warning restore CS0618 // Type or member is obsolete

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UdonBehaviour behaviour in behaviours)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Draws the syncing settings for a behaviour
        /// </summary>
        /// <param name="target"></param>
        [PublicAPI]
        public static void DrawSyncSettings(UnityEngine.Object target)
        {
            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)target);

            if (backingBehaviour)
                DrawSyncSettings(new UdonBehaviour[] { backingBehaviour });
        }
        
        /// <summary>
        /// Draws the syncing settings for multiple behaviours
        /// </summary>
        [PublicAPI]
        public static void DrawSyncSettings(UnityEngine.Object[] targets)
        {
            DrawSyncSettings(targets.Select(e => UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)e)).ToArray());
        }

        /// <summary>
        /// Draws the interact settings for UdonBehaviours, this is the interact text and proximity settings. These settings will only show if the script has an Interact() event.
        /// </summary>
        [PublicAPI]
        public static void DrawInteractSettings(UdonBehaviour[] behaviours)
        {
            if (behaviours.Length == 0)
                throw new ArgumentException("Behaviour array cannot be empty");

            bool hasInteractEvent = (behaviours.First().programSource as UdonSharpProgramAsset)?.hasInteractEvent ?? false;
            
            if (behaviours.Any(e => ((e.programSource as UdonSharpProgramAsset)?.hasInteractEvent ?? false) != hasInteractEvent))
            {
                EditorGUILayout.LabelField("Selected behaviours don't have matching interact event handing.");
                return;
            }
            
            if (!hasInteractEvent)
                return;

            string currentText = behaviours[0].interactText;

            // This should be placeholder text that gets cleared out when selected, but for now it's good enough
            if (!UdonSharpUtils.AllElementsMatch(behaviours.Select(e => e.interactText)))
                currentText = "-";

            EditorGUI.BeginChangeCheck();
            
            string newInteractText = EditorGUILayout.TextField("Interaction Text", currentText);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (UdonBehaviour behaviour in behaviours)
                {
                    Undo.RecordObject(behaviour, "Change interact text");
                    behaviour.interactText = newInteractText;
                    
                    if (PrefabUtility.IsPartOfPrefabInstance(behaviour))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }
            }

            float currentProximity = behaviours[0].proximity;

            if (!UdonSharpUtils.AllElementsMatch(behaviours.Select(e => e.proximity)))
                currentProximity = -1f;

            EditorGUI.BeginChangeCheck();
            float newProximity = EditorGUILayout.Slider("Proximity", currentProximity, 0f, 100f);

            if (EditorGUI.EndChangeCheck())
            {
                if (newProximity >= 0f)
                {
                    foreach (UdonBehaviour behaviour in behaviours)
                    {
                        Undo.RecordObject(behaviour, "Change interact distance");
                        behaviour.proximity = newProximity;
                    
                        if (PrefabUtility.IsPartOfPrefabInstance(behaviour))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                    }
                }
            }
            
            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
            if (GUILayout.Button(Loc.Get(LocStr.UI_TriggerInteract)))
            {
                foreach (UdonBehaviour behaviour in behaviours)
                {
                    UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);
                    
                    UdonSharpEditorUtility.CopyProxyToUdon(proxy, ProxySerializationPolicy.All);
                    
                    behaviour.SendCustomEvent("_interact");

                    UdonSharpEditorUtility.CopyUdonToProxy(proxy, ProxySerializationPolicy.All);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Draws the interact settings for UdonBehaviours, this is the interact text and proximity settings. These settings will only show if the script has an Interact() event.
        /// </summary>
        [PublicAPI]
        public static void DrawInteractSettings(UnityEngine.Object target)
        {
            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)target);

            if (backingBehaviour)
                DrawInteractSettings(new UdonBehaviour[] { backingBehaviour });
        }
        
        /// <summary>
        /// Draws the interact settings for UdonBehaviours, this is the interact text and proximity settings. These settings will only show if the script has an Interact() event.
        /// </summary>
        [PublicAPI]
        public static void DrawInteractSettings(UnityEngine.Object[] targets)
        {
            DrawInteractSettings(targets.Select(e => UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)e)).ToArray());
        }

        [Obsolete("DrawConvertToUdonBehaviourButton is no longer used. UdonSharpBehaviours as of 1.0 are how you interact with UdonBehaviours using U# scripts")]
        public static bool DrawConvertToUdonBehaviourButton(UnityEngine.Object target) => false;

        [Obsolete("DrawConvertToUdonBehaviourButton is no longer used. UdonSharpBehaviours as of 1.0 are how you interact with UdonBehaviours using U# scripts")]
        public static bool DrawConvertToUdonBehaviourButton(UnityEngine.Object[] targets) => false;

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
            if (DrawProgramSource(target, drawScript)) return true;

            DrawSyncSettings(target);
            DrawInteractSettings(target);
            DrawUtilities(target);
            
            DrawCompileErrorTextArea();

            if (!skipLine)
                DrawUILine();

            return false;
        }
        
        [PublicAPI]
        public static bool DrawDefaultUdonSharpBehaviourHeader(UnityEngine.Object[] targets, bool skipLine = false , bool drawScript = true)
        {
            if (DrawProgramSource(targets, drawScript)) return true;

            DrawSyncSettings(targets);
            DrawInteractSettings(targets);
            DrawUtilities(targets);
            
            DrawCompileErrorTextArea();

            if (!skipLine)
                DrawUILine();

            return false;
        }
    }
}
