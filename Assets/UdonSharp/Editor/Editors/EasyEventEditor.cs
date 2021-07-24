/**
 * MIT License
 * 
 * Copyright (c) 2019 Merlin
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

/**
 * Script to make working with objects that have Unity persistent events easier. 
 * 
 * Allows five things that the default Unity event editor does not:
 * 
 *  1. Allows reordering of events. If you want to reorder events in the default Unity editor, you need to delete events and recreate them in the desired order
 *  2. Gives easy access to private methods and properties on the target object. Usually you'd otherwise need to edit the event in debug view to add private references.
 *  3. Gives access to multiple components of the same type on the same object
 *  4. Gives an Invoke button to execute the event in editor for debugging and testing
 *  5. Adds hotkeys to event operations
 */

// Variant of EEE customized for UdonSharp to make editing events on UdonSharpBehaviours less cumbersome and confusing

#define UDONSHARP

#if UNITY_EDITOR

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

#if UDONSHARP
using VRC.Udon;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon.Graph;
using VRC.Udon.Editor;
#endif

namespace Merlin
{
    [InitializeOnLoad]
    internal class EasyEventEditorHandler
    {
        private const string eeeOverrideEventDrawerKey = "EEE.overrideEventDrawer";
        private const string eeeShowPrivateMembersKey = "EEE.showPrivateMembers";
        private const string eeeShowInvokeFieldKey = "EEE.showInvokeField";
        private const string eeeDisplayArgumentTypeKey = "EEE.displayArgumentType";
        private const string eeeGroupSameComponentTypeKey = "EEE.groupSameComponentType";
        private const string eeeUseHotkeys = "EEE.usehotkeys";
#if UDONSHARP
        private const string eeeHideOriginalUdonBehaviour = "EEE.hideOriginalUdonBehaviour";
#endif

        private static bool patchApplied = false;
        private static FieldInfo internalDrawerTypeMap = null;
        private static System.Type attributeUtilityType = null;

        public class EEESettings
        {
            public bool overrideEventDrawer;
            public bool showPrivateMembers;
            public bool showInvokeField;
            public bool displayArgumentType;
            public bool groupSameComponentType;
            public bool useHotkeys;
#if UDONSHARP
            public bool hideOriginalUdonBehaviour;
#endif
        }

        // https://stackoverflow.com/questions/12898282/type-gettype-not-working 
        public static System.Type FindTypeInAllAssemblies(string qualifiedTypeName)
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

        static EasyEventEditorHandler()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        static void OnEditorUpdate()
        {
            ApplyEventPropertyDrawerPatch();
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            ApplyEventPropertyDrawerPatch();
        }

        internal static FieldInfo GetDrawerTypeMap()
        {
            // We already have the map so skip all the reflection
            if (internalDrawerTypeMap != null)
            {
                return internalDrawerTypeMap;
            }

            System.Type scriptAttributeUtilityType = FindTypeInAllAssemblies("UnityEditor.ScriptAttributeUtility");

            if (scriptAttributeUtilityType == null)
            {
                Debug.LogError("Could not find ScriptAttributeUtility in assemblies!");
                return null;
            }

            // Save for later in case we need to lookup the function to populate the attributes
            attributeUtilityType = scriptAttributeUtilityType;

            FieldInfo info = scriptAttributeUtilityType.GetField("s_DrawerTypeForType", BindingFlags.NonPublic | BindingFlags.Static);

            if (info == null)
            {
                Debug.LogError("Could not find drawer type map!");
                return null;
            }

            internalDrawerTypeMap = info;

            return internalDrawerTypeMap;
        }

        private static void ClearPropertyCaches()
        {
            if (attributeUtilityType == null)
            {
                Debug.LogError("UnityEditor.ScriptAttributeUtility type is null! Make sure you have called GetDrawerTypeMap() to ensure this is cached!");
                return;
            }

            // Nuke handle caches so they can find our modified drawer
            MethodInfo clearCacheFunc = attributeUtilityType.GetMethod("ClearGlobalCache", BindingFlags.NonPublic | BindingFlags.Static);

            if (clearCacheFunc == null)
            {
                Debug.LogError("Could not find cache clear method!");
                return;
            }

            clearCacheFunc.Invoke(null, new object[] { });

            FieldInfo currentCacheField = attributeUtilityType.GetField("s_CurrentCache", BindingFlags.NonPublic | BindingFlags.Static);

            if (currentCacheField == null)
            {
                Debug.LogError("Could not find CurrentCache field!");
                return;
            }

            object currentCacheValue = currentCacheField.GetValue(null);

            if (currentCacheValue != null)
            {
                MethodInfo clearMethod = currentCacheValue.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);

                if (clearMethod == null)
                {
                    Debug.LogError("Could not find clear function for current cache!");
                    return;
                }

                clearMethod.Invoke(currentCacheValue, new object[] { });
            }

            System.Type inspectorWindowType = FindTypeInAllAssemblies("UnityEditor.InspectorWindow");

            if (inspectorWindowType == null)
            {
                Debug.LogError("Could not find inspector window type!");
                return;
            }

            FieldInfo trackerField = inspectorWindowType.GetField("m_Tracker", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo propertyHandleCacheField = typeof(Editor).GetField("m_PropertyHandlerCache", BindingFlags.NonPublic | BindingFlags.Instance);

            if (trackerField == null || propertyHandleCacheField == null)
            {
                Debug.LogError("Could not find tracker field!");
                return;
            }

            //FieldInfo trackerEditorsField = trackerField.GetType().GetField("")

            System.Type propertyHandlerCacheType = FindTypeInAllAssemblies("UnityEditor.PropertyHandlerCache");

            if (propertyHandlerCacheType == null)
            {
                Debug.LogError("Could not find type of PropertyHandlerCache");
                return;
            }

            // Secondary nuke because Unity is great and keeps a cached copy of the events for every Editor in addition to a global cache we cleared earlier.
            EditorWindow[] editorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();

            foreach (EditorWindow editor in editorWindows)
            {
                if (editor.GetType() == inspectorWindowType || editor.GetType().IsSubclassOf(inspectorWindowType))
                {
                    ActiveEditorTracker activeEditorTracker = trackerField.GetValue(editor) as ActiveEditorTracker;

                    if (activeEditorTracker != null)
                    {
                        foreach (Editor activeEditor in activeEditorTracker.activeEditors)
                        {
                            if (activeEditor != null)
                            {
                                propertyHandleCacheField.SetValue(activeEditor, System.Activator.CreateInstance(propertyHandlerCacheType));
                                activeEditor.Repaint(); // Force repaint to get updated drawing of property
                            }
                        }
                    }
                }
            }
        }

        // Applies patch to Unity's builtin tracking for Drawers to redirect any drawers for Unity Events to our EasyEventDrawer instead.
        private static void ApplyEventDrawerPatch(bool enableOverride)
        {
            // Call here to find the scriptAttributeUtilityType in case it's needed for when overrides are disabled
            FieldInfo drawerTypeMap = GetDrawerTypeMap();

            if (enableOverride)
            {
                System.Type[] mapArgs = drawerTypeMap.FieldType.GetGenericArguments();

                System.Type keyType = mapArgs[0];
                System.Type valType = mapArgs[1];

                if (keyType == null || valType == null)
                {
                    Debug.LogError("Could not retrieve dictionary types!");
                    return;
                }

                FieldInfo drawerField = valType.GetField("drawer", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo typeField = valType.GetField("type", BindingFlags.Public | BindingFlags.Instance);

                if (drawerField == null || typeField == null)
                {
                    Debug.LogError("Could not retrieve dictionary value fields!");
                    return;
                }

                IDictionary drawerTypeMapDict = drawerTypeMap.GetValue(null) as IDictionary;

                if (drawerTypeMapDict == null)
                {
                    MethodInfo popAttributesFunc = attributeUtilityType.GetMethod("BuildDrawerTypeForTypeDictionary", BindingFlags.NonPublic | BindingFlags.Static);

                    if (popAttributesFunc == null)
                    {
                        Debug.LogError("Could not populate attributes for override!");
                        return;
                    }

                    popAttributesFunc.Invoke(null, new object[] { });
                
                    // Try again now that this should be populated
                    drawerTypeMapDict = drawerTypeMap.GetValue(null) as IDictionary;
                    if (drawerTypeMapDict == null)
                    {
                        Debug.LogError("Could not get dictionary for drawer types!");
                        return;
                    }
                }

                // Replace EventDrawer handles with our custom drawer
                List<object> keysToRecreate = new List<object>();

                foreach (DictionaryEntry entry in drawerTypeMapDict)
                {
                    System.Type drawerType = (System.Type)drawerField.GetValue(entry.Value);
                
                    if (drawerType.Name == "UnityEventDrawer")
                    {
                        keysToRecreate.Add(entry.Key);
                    }
                }

                foreach (object keyToKill in keysToRecreate)
                {
                    drawerTypeMapDict.Remove(keyToKill); 
                }

                // Recreate these key-value pairs since they are structs
                foreach (object keyToRecreate in keysToRecreate)
                {
                    object newValMapping = System.Activator.CreateInstance(valType);
                    typeField.SetValue(newValMapping, (System.Type)keyToRecreate);
                    drawerField.SetValue(newValMapping, typeof(EasyEventEditorDrawer));

                    drawerTypeMapDict.Add(keyToRecreate, newValMapping);
                }
            }
            else 
            {
                MethodInfo popAttributesFunc = attributeUtilityType.GetMethod("BuildDrawerTypeForTypeDictionary", BindingFlags.NonPublic | BindingFlags.Static);

                if (popAttributesFunc == null)
                {
                    Debug.LogError("Could not populate attributes for override!");
                    return;
                }

                // Just force the editor to repopulate the drawers without nuking afterwards.
                popAttributesFunc.Invoke(null, new object[] { });
            }
        
            // Clear caches to force event drawers to refresh immediately.
            ClearPropertyCaches();
        }

        public static void ApplyEventPropertyDrawerPatch(bool forceApply = false)
        {
            EEESettings settings = GetEditorSettings();

            if (!patchApplied || forceApply)
            {
                ApplyEventDrawerPatch(settings.overrideEventDrawer);
                patchApplied = true;
            }
        }

        public static EEESettings GetEditorSettings()
        {
            EEESettings settings = new EEESettings
            {
                overrideEventDrawer = EditorPrefs.GetBool(eeeOverrideEventDrawerKey, true),
                showPrivateMembers = EditorPrefs.GetBool(eeeShowPrivateMembersKey, false),
                showInvokeField = EditorPrefs.GetBool(eeeShowInvokeFieldKey, true),
                displayArgumentType = EditorPrefs.GetBool(eeeDisplayArgumentTypeKey, true),
                groupSameComponentType = EditorPrefs.GetBool(eeeGroupSameComponentTypeKey, false),
                useHotkeys = EditorPrefs.GetBool(eeeUseHotkeys, true),
#if UDONSHARP
                hideOriginalUdonBehaviour = EditorPrefs.GetBool(eeeHideOriginalUdonBehaviour, false),
#endif
            };

            return settings;
        }

        public static void SetEditorSettings(EEESettings settings)
        {
            EditorPrefs.SetBool(eeeOverrideEventDrawerKey, settings.overrideEventDrawer);
            EditorPrefs.SetBool(eeeShowPrivateMembersKey, settings.showPrivateMembers);
            EditorPrefs.SetBool(eeeShowInvokeFieldKey, settings.showInvokeField);
            EditorPrefs.SetBool(eeeDisplayArgumentTypeKey, settings.displayArgumentType);
            EditorPrefs.SetBool(eeeGroupSameComponentTypeKey, settings.groupSameComponentType);
            EditorPrefs.SetBool(eeeUseHotkeys, settings.useHotkeys);
#if UDONSHARP
            EditorPrefs.SetBool(eeeHideOriginalUdonBehaviour, settings.hideOriginalUdonBehaviour);
#endif
        }
    }

    internal class SettingsGUIContent
    {
        private static GUIContent enableToggleGuiContent = new GUIContent("Enable Easy Event Editor", "Replaces the default Unity event editing context with EEE");
        private static GUIContent enablePrivateMembersGuiContent = new GUIContent("Show private properties and methods", "Exposes private/internal/obsolete properties and methods to the function list on events");
        private static GUIContent showInvokeFieldGuiContent = new GUIContent("Show invoke button on events", "Gives you a button on events that can be clicked to execute all functions on a given event");
        private static GUIContent displayArgumentTypeContent = new GUIContent("Display argument type on function name", "Shows the argument that a function takes on the function header");
        private static GUIContent groupSameComponentTypeContent = new GUIContent("Do not group components of the same type", "If you have multiple components of the same type on one object, show all components. Unity hides duplicate components by default.");
        private static GUIContent useHotkeys = new GUIContent("Use hotkeys", "Adds common Unity hotkeys to event editor that operate on the currently selected event. The commands are Add (CTRL+A), Copy, Paste, Cut, Delete, and Duplicate");
#if UDONSHARP
        private static GUIContent hideOriginalUdonBehaviourContent = new GUIContent("Hide original UdonBehaviour", "Hides the original UdonBehaviour from the event interface and only shows the C# version of it.");
#endif

        public static void DrawSettingsButtons(EasyEventEditorHandler.EEESettings settings)
        {
            EditorGUI.indentLevel += 1;

            settings.overrideEventDrawer = EditorGUILayout.ToggleLeft(enableToggleGuiContent, settings.overrideEventDrawer);

            EditorGUI.BeginDisabledGroup(!settings.overrideEventDrawer);

            settings.showPrivateMembers = EditorGUILayout.ToggleLeft(enablePrivateMembersGuiContent, settings.showPrivateMembers);
            settings.showInvokeField = EditorGUILayout.ToggleLeft(showInvokeFieldGuiContent, settings.showInvokeField);
            settings.displayArgumentType = EditorGUILayout.ToggleLeft(displayArgumentTypeContent, settings.displayArgumentType);
            settings.groupSameComponentType = !EditorGUILayout.ToggleLeft(groupSameComponentTypeContent, !settings.groupSameComponentType);
            settings.useHotkeys = EditorGUILayout.ToggleLeft(useHotkeys, settings.useHotkeys);

#if UDONSHARP
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Udon Sharp settings", EditorStyles.boldLabel);
            settings.hideOriginalUdonBehaviour = EditorGUILayout.ToggleLeft(hideOriginalUdonBehaviourContent, settings.hideOriginalUdonBehaviour);
#endif

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel -= 1;
        }
    }

    #if UNITY_2018_3_OR_NEWER
    // Use the new settings provider class instead so we don't need to add extra stuff to the Edit menu
    // Using the IMGUI method
    static class EasyEventEditorSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/Easy Event Editor", SettingsScope.User)
            {
                label = "Easy Event Editor",

                guiHandler = (searchContext) =>
                {
                    EasyEventEditorHandler.EEESettings settings = EasyEventEditorHandler.GetEditorSettings();

                    EditorGUI.BeginChangeCheck();
                    SettingsGUIContent.DrawSettingsButtons(settings);

                    if (EditorGUI.EndChangeCheck())
                    {
                        EasyEventEditorHandler.SetEditorSettings(settings);
                        EasyEventEditorHandler.ApplyEventPropertyDrawerPatch(true);
                    }

                },

                keywords = new HashSet<string>(new[] { "Easy", "Event", "Editor", "Delegate", "VRChat", "EEE" })
            };

            return provider;
        }
    }
    #else
    public class EasyEventEditorSettings : EditorWindow
    {
        [MenuItem("Edit/Easy Event Editor Settings")]
        static void Init()
        {
            EasyEventEditorSettings window = GetWindow<EasyEventEditorSettings>(false, "EEE Settings");
            window.minSize = new Vector2(350, 150);
            window.maxSize = new Vector2(350, 150);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Easy Event Editor Settings", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EasyEventEditorHandler.EEESettings settings = EasyEventEditorHandler.GetEditorSettings();

            EditorGUI.BeginChangeCheck();
            SettingsGUIContent.DrawSettingsButtons(settings);

            if (EditorGUI.EndChangeCheck())
            {
                EasyEventEditorHandler.SetEditorSettings(settings);
                EasyEventEditorHandler.ApplyEventPropertyDrawerPatch(true);
            }
        }
    }
    #endif

    // Drawer that gets patched in over Unity's default event drawer
    internal class EasyEventEditorDrawer : PropertyDrawer
    {
        class DrawerState
        {
            public ReorderableList reorderableList;
            public int lastSelectedIndex;

            // Invoke field tracking
            public string currentInvokeStrArg = "";
            public int currentInvokeIntArg = 0;
            public float currentInvokeFloatArg = 0f;
            public bool currentInvokeBoolArg = false;
            public Object currentInvokeObjectArg = null;
        }

        class FunctionData
        {
            public FunctionData(SerializedProperty listener, Object target = null, MethodInfo method = null, PersistentListenerMode mode = PersistentListenerMode.EventDefined)
            {
                listenerElement = listener;
                targetObject = target;
                targetMethod = method;
                listenerMode = mode;
            }

            public SerializedProperty listenerElement;
            public Object targetObject;
            public MethodInfo targetMethod;
            public PersistentListenerMode listenerMode;
        }

        Dictionary<string, DrawerState> drawerStates = new Dictionary<string, DrawerState>();

        DrawerState currentState;
        string currentLabelText;
        SerializedProperty currentProperty;
        SerializedProperty listenerArray;

        UnityEventBase dummyEvent;
        MethodInfo cachedFindMethodInfo = null;
        static EasyEventEditorHandler.EEESettings cachedSettings;

    #if UNITY_2018_4_OR_NEWER
        private static UnityEventBase GetDummyEventStep(string propertyPath, System.Type propertyType, BindingFlags bindingFlags)
        {
            UnityEventBase dummyEvent = null;
        
            while (propertyPath.Length > 0)
            {
                if (propertyPath.StartsWith("."))
                    propertyPath = propertyPath.Substring(1);

                string[] splitPath = propertyPath.Split(new char[] { '.' }, 2);

                FieldInfo newField = propertyType.GetField(splitPath[0], bindingFlags);

                if (newField == null)
                    break;

                propertyType = newField.FieldType;
                if (propertyType.IsArray)
                {
                    propertyType = propertyType.GetElementType();
                }
                else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                if (splitPath.Length == 1)
                    break;

                propertyPath = splitPath[1];
                if (propertyPath.StartsWith("Array.data["))
                    propertyPath = propertyPath.Split(new char[] { ']' }, 2)[1];
            }

            if (propertyType.IsSubclassOf(typeof(UnityEventBase)))
                dummyEvent = System.Activator.CreateInstance(propertyType) as UnityEventBase;

            return dummyEvent;
        }

        private static UnityEventBase GetDummyEvent(SerializedProperty property)
        {
            Object targetObject = property.serializedObject.targetObject;
            if (targetObject == null)
                return new UnityEvent();

            UnityEventBase dummyEvent = null;
            System.Type targetType = targetObject.GetType();
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            do
            {
                dummyEvent = GetDummyEventStep(property.propertyPath, targetType, bindingFlags);
                bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                targetType = targetType.BaseType;
            } while (dummyEvent == null && targetType != null);

            return dummyEvent ?? new UnityEvent();
        }
    #endif

        private void PrepareState(SerializedProperty propertyForState)
        {
            DrawerState state;

            if (!drawerStates.TryGetValue(propertyForState.propertyPath, out state))
            {
                state = new DrawerState();

                SerializedProperty persistentListeners = propertyForState.FindPropertyRelative("m_PersistentCalls.m_Calls");

                // The fun thing is that if Unity just made the first bool arg true internally, this whole thing would be unnecessary.
                state.reorderableList = new ReorderableList(propertyForState.serializedObject, persistentListeners, true, true, true, true);
                state.reorderableList.elementHeight = 43; // todo: actually find proper constant for this. 
                state.reorderableList.drawHeaderCallback += DrawHeaderCallback;
                state.reorderableList.drawElementCallback += DrawElementCallback;
                state.reorderableList.onSelectCallback += SelectCallback;
                state.reorderableList.onRemoveCallback += ReorderCallback;
                state.reorderableList.onAddCallback += AddEventListener;
                state.reorderableList.onRemoveCallback += RemoveCallback;

                state.lastSelectedIndex = 0;

                drawerStates.Add(propertyForState.propertyPath, state);
            }

            currentProperty = propertyForState;

            currentState = state;
            currentState.reorderableList.index = currentState.lastSelectedIndex;
            listenerArray = state.reorderableList.serializedProperty;

            // Setup dummy event
    #if UNITY_2018_4_OR_NEWER
            dummyEvent = GetDummyEvent(propertyForState);
    #else
            string eventTypeName = currentProperty.FindPropertyRelative("m_TypeName").stringValue;
            System.Type eventType = EasyEventEditorHandler.FindTypeInAllAssemblies(eventTypeName);
            if (eventType == null)
                dummyEvent = new UnityEvent();
            else
                dummyEvent = System.Activator.CreateInstance(eventType) as UnityEventBase;
    #endif

            cachedSettings = EasyEventEditorHandler.GetEditorSettings();
        }

        private void HandleKeyboardShortcuts()
        {
            if (!cachedSettings.useHotkeys)
                return;

            Event currentEvent = Event.current;

            if (!currentState.reorderableList.HasKeyboardControl())
                return;

            if (currentEvent.type == EventType.ValidateCommand)
            {
                if (currentEvent.commandName == "Copy" ||
                    currentEvent.commandName == "Paste" ||
                    currentEvent.commandName == "Cut" ||
                    currentEvent.commandName == "Duplicate" ||
                    currentEvent.commandName == "Delete" ||
                    currentEvent.commandName == "SoftDelete" ||
                    currentEvent.commandName == "SelectAll")
                {
                    currentEvent.Use();
                }
            }
            else if (currentEvent.type == EventType.ExecuteCommand) 
            {
                if (currentEvent.commandName == "Copy")
                {
                    HandleCopy();
                    currentEvent.Use();
                }
                else if (currentEvent.commandName == "Paste")
                {
                    HandlePaste();
                    currentEvent.Use();
                }
                else if (currentEvent.commandName == "Cut")
                {
                    HandleCut();
                    currentEvent.Use();
                }
                else if (currentEvent.commandName == "Duplicate")
                {
                    HandleDuplicate();
                    currentEvent.Use();
                }
                else if (currentEvent.commandName == "Delete" || currentEvent.commandName == "SoftDelete")
                {
                    RemoveCallback(currentState.reorderableList);
                    currentEvent.Use();
                }
                else if (currentEvent.commandName == "SelectAll") // Use Ctrl+A for add, since Ctrl+N isn't usable using command names
                {
                    HandleAdd();
                    currentEvent.Use();
                }
            }
        }

        private class EventClipboardStorage
        {
            public static SerializedObject CopiedEventProperty;
            public static int CopiedEventIndex;
        }

        private void HandleCopy()
        {
            SerializedObject serializedEvent = new SerializedObject(listenerArray.GetArrayElementAtIndex(currentState.reorderableList.index).serializedObject.targetObject);

            EventClipboardStorage.CopiedEventProperty = serializedEvent;
            EventClipboardStorage.CopiedEventIndex = currentState.reorderableList.index;
        }

        private void HandlePaste()
        {
            if (EventClipboardStorage.CopiedEventProperty == null)
                return;

            SerializedProperty iterator = EventClipboardStorage.CopiedEventProperty.GetIterator();

            if (iterator == null)
                return;

            while (iterator.NextVisible(true))
            {
                if (iterator != null && iterator.name == "m_PersistentCalls")
                {
                    iterator = iterator.FindPropertyRelative("m_Calls");
                    break;
                }
            }

            if (iterator.arraySize < (EventClipboardStorage.CopiedEventIndex + 1))
                return;

            SerializedProperty sourceProperty = iterator.GetArrayElementAtIndex(EventClipboardStorage.CopiedEventIndex);

            if (sourceProperty == null)
                return;

            int targetArrayIdx = currentState.reorderableList.count > 0 ? currentState.reorderableList.index : 0;
            currentState.reorderableList.serializedProperty.InsertArrayElementAtIndex(targetArrayIdx);

            SerializedProperty targetProperty = currentState.reorderableList.serializedProperty.GetArrayElementAtIndex((currentState.reorderableList.count > 0 ? currentState.reorderableList.index : 0) + 1);
            ResetEventState(targetProperty);

            targetProperty.FindPropertyRelative("m_CallState").enumValueIndex = sourceProperty.FindPropertyRelative("m_CallState").enumValueIndex;
            targetProperty.FindPropertyRelative("m_Target").objectReferenceValue = sourceProperty.FindPropertyRelative("m_Target").objectReferenceValue;
            targetProperty.FindPropertyRelative("m_MethodName").stringValue = sourceProperty.FindPropertyRelative("m_MethodName").stringValue;
            targetProperty.FindPropertyRelative("m_Mode").enumValueIndex = sourceProperty.FindPropertyRelative("m_Mode").enumValueIndex;

            SerializedProperty targetArgs = targetProperty.FindPropertyRelative("m_Arguments");
            SerializedProperty sourceArgs = sourceProperty.FindPropertyRelative("m_Arguments");

            targetArgs.FindPropertyRelative("m_IntArgument").intValue = sourceArgs.FindPropertyRelative("m_IntArgument").intValue;
            targetArgs.FindPropertyRelative("m_FloatArgument").floatValue = sourceArgs.FindPropertyRelative("m_FloatArgument").floatValue;
            targetArgs.FindPropertyRelative("m_BoolArgument").boolValue = sourceArgs.FindPropertyRelative("m_BoolArgument").boolValue;
            targetArgs.FindPropertyRelative("m_StringArgument").stringValue = sourceArgs.FindPropertyRelative("m_StringArgument").stringValue;
            targetArgs.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = sourceArgs.FindPropertyRelative("m_ObjectArgument").objectReferenceValue;
            targetArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = sourceArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue;

            currentState.reorderableList.index++;
            currentState.lastSelectedIndex++;

            targetProperty.serializedObject.ApplyModifiedProperties();
        }

        private void HandleCut()
        {
            HandleCopy();
            RemoveCallback(currentState.reorderableList);
        }

        private void HandleDuplicate()
        {
            if (currentState.reorderableList.count == 0)
                return;

            SerializedProperty listProperty = currentState.reorderableList.serializedProperty;

            SerializedProperty eventProperty = listProperty.GetArrayElementAtIndex(currentState.reorderableList.index);

            eventProperty.DuplicateCommand();

            currentState.reorderableList.index++;
            currentState.lastSelectedIndex++;
        }

        private void HandleAdd()
        {
            int targetIdx = currentState.reorderableList.count > 0 ? currentState.reorderableList.index : 0;
            currentState.reorderableList.serializedProperty.InsertArrayElementAtIndex(targetIdx);

            SerializedProperty eventProperty = currentState.reorderableList.serializedProperty.GetArrayElementAtIndex(currentState.reorderableList.index + 1);
            ResetEventState(eventProperty);

            currentState.reorderableList.index++;
            currentState.lastSelectedIndex++;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            currentLabelText = label.text;
            PrepareState(property);

            HandleKeyboardShortcuts();

            if (dummyEvent == null)
                return;

            if (currentState.reorderableList != null)
            {
                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                currentState.reorderableList.DoList(position);
                EditorGUI.indentLevel = oldIndent;
            }
        }

        static void InvokeOnTargetEvents(MethodInfo method, object[] targets, object argValue)
        {
            foreach (object target in targets)
            {
                if (argValue != null)
                    method.Invoke(target, new object[] { argValue });
                else
                    method.Invoke(target, new object[] { });
            }
        }

        void DrawInvokeField(Rect position, float headerStartOffset)
        {
            Rect buttonPos = position;
            buttonPos.height *= 0.9f;
            buttonPos.width = 51;
            buttonPos.x += headerStartOffset + 2;

            Rect textPos = buttonPos;
            textPos.x += 6;
            textPos.width -= 12;

            Rect inputFieldPos = position;
            inputFieldPos.height = buttonPos.height;
            inputFieldPos.width = position.width - buttonPos.width - 3 - headerStartOffset;
            inputFieldPos.x = buttonPos.x + buttonPos.width + 2;
            inputFieldPos.y += 1;

            Rect inputFieldTextPlaceholder = inputFieldPos;

            System.Type[] eventInvokeArgs = GetEventParams(dummyEvent);

            GUIStyle textStyle = EditorStyles.miniLabel;
            textStyle.alignment = TextAnchor.MiddleLeft;

            MethodInfo invokeMethod = InvokeFindMethod("Invoke", dummyEvent, dummyEvent, PersistentListenerMode.EventDefined);
            FieldInfo serializedField = currentProperty.serializedObject.targetObject.GetType().GetField(currentProperty.name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            object[] invokeTargets = currentProperty.serializedObject.targetObjects.Select(target => target == null || serializedField == null ? null : serializedField.GetValue(target)).Where(f => f != null).ToArray();
        
            EditorGUI.BeginDisabledGroup(invokeTargets.Length == 0 || invokeMethod == null);

            bool executeInvoke = GUI.Button(buttonPos, "", EditorStyles.miniButton);
            GUI.Label(textPos, "Invoke"/* + " (" + string.Join(", ", eventInvokeArgs.Select(e => e.Name).ToArray()) + ")"*/, textStyle);

            if (eventInvokeArgs.Length > 0)
            {
                System.Type argType = eventInvokeArgs[0];

                if (argType == typeof(string))
                {
                    currentState.currentInvokeStrArg = EditorGUI.TextField(inputFieldPos, currentState.currentInvokeStrArg);

                    // Draw placeholder text
                    if (currentState.currentInvokeStrArg.Length == 0)
                    {
                        GUIStyle placeholderLabelStyle = EditorStyles.centeredGreyMiniLabel;
                        placeholderLabelStyle.alignment = TextAnchor.UpperLeft;

                        GUI.Label(inputFieldTextPlaceholder, "String argument...", placeholderLabelStyle);
                    }

                    if (executeInvoke)
                        InvokeOnTargetEvents(invokeMethod, invokeTargets, currentState.currentInvokeStrArg);
                }
                else if (argType == typeof(int))
                {
                    currentState.currentInvokeIntArg = EditorGUI.IntField(inputFieldPos, currentState.currentInvokeIntArg);

                    if (executeInvoke)
                        InvokeOnTargetEvents(invokeMethod, invokeTargets, currentState.currentInvokeIntArg);
                }
                else if (argType == typeof(float))
                {
                    currentState.currentInvokeFloatArg = EditorGUI.FloatField(inputFieldPos, currentState.currentInvokeFloatArg);

                    if (executeInvoke)
                        InvokeOnTargetEvents(invokeMethod, invokeTargets, currentState.currentInvokeFloatArg);
                }
                else if (argType == typeof(bool))
                {
                    currentState.currentInvokeBoolArg = EditorGUI.Toggle(inputFieldPos, currentState.currentInvokeBoolArg);

                    if (executeInvoke)
                        InvokeOnTargetEvents(invokeMethod, invokeTargets, currentState.currentInvokeBoolArg);
                }
                else if (argType == typeof(Object))
                {
                    currentState.currentInvokeObjectArg = EditorGUI.ObjectField(inputFieldPos, currentState.currentInvokeObjectArg, argType, true);

                    if (executeInvoke)
                        invokeMethod.Invoke(currentProperty.serializedObject.targetObject, new object[] { currentState.currentInvokeObjectArg });
                }
            }
            else if (executeInvoke) // No input arg
            {
                InvokeOnTargetEvents(invokeMethod, invokeTargets, null);
            }

            EditorGUI.EndDisabledGroup();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            PrepareState(property);

            float height = 0f;
            if (currentState.reorderableList != null)
                height = currentState.reorderableList.GetHeight();

            return height;
        }

        MethodInfo InvokeFindMethod(string functionName, object targetObject, UnityEventBase eventObject, PersistentListenerMode listenerMode, System.Type argType = null)
        {
            MethodInfo findMethod = cachedFindMethodInfo;

            if (findMethod == null)
            {
                // Rather not reinvent the wheel considering this function calls different functions depending on the number of args the event has...
                findMethod = eventObject.GetType().GetMethod("FindMethod", BindingFlags.NonPublic | BindingFlags.Instance, null,
                        new System.Type[] {
                        typeof(string),
                        typeof(object),
                        typeof(PersistentListenerMode),
                        typeof(System.Type)
                        },
                    null);

                cachedFindMethodInfo = findMethod;
            }

            if (findMethod == null)
            {
                Debug.LogError("Could not find FindMethod function!");
                return null;
            }

            return findMethod.Invoke(eventObject, new object[] { functionName, targetObject, listenerMode, argType }) as MethodInfo;
        }

        System.Type[] GetEventParams(UnityEventBase eventIn)
        {
            MethodInfo methodInfo = InvokeFindMethod("Invoke", eventIn, eventIn, PersistentListenerMode.EventDefined);
            return methodInfo.GetParameters().Select(x => x.ParameterType).ToArray();
        }

        string GetEventParamsStr(UnityEventBase eventIn)
        {
            StringBuilder builder = new StringBuilder();
            System.Type[] methodTypes = GetEventParams(eventIn);

            builder.Append("(");
            builder.Append(string.Join(", ", methodTypes.Select(val => val.Name).ToArray()));
            builder.Append(")");

            return builder.ToString();
        }

        string GetFunctionArgStr(string functionName, object targetObject, PersistentListenerMode listenerMode, System.Type argType = null)
        {
            MethodInfo methodInfo = InvokeFindMethod(functionName, targetObject, dummyEvent, listenerMode, argType);

            if (methodInfo == null)
                return "";

            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            if (parameterInfos.Length == 0)
                return "";

            return GetTypeName(parameterInfos[0].ParameterType);
        }

        void DrawHeaderCallback(Rect headerRect)
        {
            // We need to know where to position the invoke field based on the length of the title in the UI
            GUIContent headerTitle = new GUIContent(string.IsNullOrEmpty(currentLabelText) ? "Event" : currentLabelText + " " + GetEventParamsStr(dummyEvent));
            float headerStartOffset = EditorStyles.label.CalcSize(headerTitle).x;
        
            GUI.Label(headerRect, headerTitle);
        
            if (cachedSettings.showInvokeField)
                DrawInvokeField(headerRect, headerStartOffset);
        }

        Rect[] GetElementRects(Rect rect)
        {
            Rect[] rects = new Rect[4];

            rect.height = EditorGUIUtility.singleLineHeight;
            rect.y += 2;

            // enabled field
            rects[0] = rect;
            rects[0].width *= 0.3f;

            // game object field
            rects[1] = rects[0];
            rects[1].x += 1;
            rects[1].width -= 2;
            rects[1].y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // function field
            rects[2] = rect;
            rects[2].xMin = rects[1].xMax + 5;

            // argument field
            rects[3] = rects[2];
            rects[3].y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            return rects;
        }

        string GetFunctionDisplayName(SerializedProperty objectProperty, SerializedProperty methodProperty, PersistentListenerMode listenerMode, System.Type argType, bool showArg)
        {
            string methodNameOut = "No Function";

            if (objectProperty.objectReferenceValue == null || methodProperty.stringValue == "")
                return methodNameOut;

            MethodInfo methodInfo = InvokeFindMethod(methodProperty.stringValue, objectProperty.objectReferenceValue, dummyEvent, listenerMode, argType);
            string funcName = methodProperty.stringValue.StartsWith("set_") ? methodProperty.stringValue.Substring(4) : methodProperty.stringValue;

            if (methodInfo == null)
            {
                methodNameOut = string.Format("<Missing {0}.{1}>", objectProperty.objectReferenceValue.GetType().Name.ToString(), funcName);
                return methodNameOut;
            }

            string objectTypeName = objectProperty.objectReferenceValue.GetType().Name;
            Component objectComponent = objectProperty.objectReferenceValue as Component;

            if (!cachedSettings.groupSameComponentType && objectComponent != null)
            {
                System.Type objectType = objectProperty.objectReferenceValue.GetType();

                Component[] components = objectComponent.GetComponents(objectType);

                if (components.Length > 1)
                {
                    int componentID = 0;
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] == objectComponent)
                        {
                            componentID = i + 1;
                            break;
                        }
                    }
                
                    objectTypeName += string.Format("({0})", componentID);
                }
            }

            if (showArg)
            {
                string functionArgStr = GetFunctionArgStr(methodProperty.stringValue, objectProperty.objectReferenceValue, listenerMode, argType);
                methodNameOut = string.Format("{0}.{1} ({2})", objectTypeName, funcName, functionArgStr);
            }
            else
            {
                methodNameOut = string.Format("{0}.{1}", objectTypeName, funcName);
            }


            return methodNameOut;
        }

        System.Type[] GetTypeForListenerMode(PersistentListenerMode listenerMode)
        {
            switch (listenerMode)
            {
                case PersistentListenerMode.EventDefined:
                case PersistentListenerMode.Void:
                    return new System.Type[] { };
                case PersistentListenerMode.Object:
                    return new System.Type[] { typeof(Object) };
                case PersistentListenerMode.Int:
                    return new System.Type[] { typeof(int) };
                case PersistentListenerMode.Float:
                    return new System.Type[] { typeof(float) };
                case PersistentListenerMode.String:
                    return new System.Type[] { typeof(string) };
                case PersistentListenerMode.Bool:
                    return new System.Type[] { typeof(bool) };
            }

            return new System.Type[] { };
        }

        void FindValidMethods(Object targetObject, PersistentListenerMode listenerMode, List<FunctionData> methodInfos, System.Type[] customArgTypes = null)
        {
            System.Type objectType = targetObject.GetType();

            System.Type[] argTypes;

            if (listenerMode == PersistentListenerMode.EventDefined && customArgTypes != null)
                argTypes = customArgTypes;
            else
                argTypes = GetTypeForListenerMode(listenerMode);

            List<MethodInfo> foundMethods = new List<MethodInfo>();

            // For some reason BindingFlags.FlattenHierarchy does not seem to work, so we manually traverse the base types instead
            while (objectType != null)
            {
                MethodInfo[] foundMethodsOnType = objectType.GetMethods(BindingFlags.Public | (cachedSettings.showPrivateMembers ? BindingFlags.NonPublic : BindingFlags.Default) | BindingFlags.Instance);

                foundMethods.AddRange(foundMethodsOnType);

                objectType = objectType.BaseType; 
            }

            foreach (MethodInfo methodInfo in foundMethods)
            {
                // Sadly we can only use functions with void return type since C# throws an error
                if (methodInfo.ReturnType != typeof(void))
                    continue;

                ParameterInfo[] methodParams = methodInfo.GetParameters();
                if (methodParams.Length != argTypes.Length)
                    continue;

                bool isValidParamMatch = true;
                for (int i = 0; i < methodParams.Length; i++)
                {
                    if (!methodParams[i].ParameterType.IsAssignableFrom(argTypes[i])/* && (argTypes[i] != typeof(int) || !methodParams[i].ParameterType.IsEnum)*/)
                    {
                        isValidParamMatch = false;
                    }
                    if (listenerMode == PersistentListenerMode.Object && argTypes[i].IsAssignableFrom(methodParams[i].ParameterType))
                    {
                        isValidParamMatch = true;
                    }
                }

                if (!isValidParamMatch)
                    continue;

                if (!cachedSettings.showPrivateMembers && methodInfo.GetCustomAttributes(typeof(System.ObsoleteAttribute), true).Length > 0)
                    continue;


                FunctionData foundMethodData = new FunctionData(null, targetObject, methodInfo, listenerMode);

                methodInfos.Add(foundMethodData);
            }
        }

        string GetTypeName(System.Type typeToName)
        {
            if (typeToName == typeof(float))
                return "float";
            if (typeToName == typeof(bool))
                return "bool";
            if (typeToName == typeof(int))
                return "int";
            if (typeToName == typeof(string))
                return "string";

            return typeToName.Name;
        }

        void AddFunctionToMenu(string contentPath, SerializedProperty elementProperty, FunctionData methodData, GenericMenu menu, int componentCount, bool dynamicCall = false)
        {
            string functionName = (methodData.targetMethod.Name.StartsWith("set_") ? methodData.targetMethod.Name.Substring(4) : methodData.targetMethod.Name);
            string argStr = string.Join(", ", methodData.targetMethod.GetParameters().Select(param => GetTypeName(param.ParameterType)).ToArray());

            if (dynamicCall) // Cut out the args from the dynamic variation to match Unity, and the menu item won't be created if it's not unique.
            {
                contentPath += functionName;
            }
            else
            {
                if (methodData.targetMethod.Name.StartsWith("set_")) // If it's a property add the arg before the name
                {
                    contentPath += argStr + " " + functionName;
                }
                else
                {
                    contentPath += functionName + " (" + argStr + ")"; // Add arguments
                }
            }

            if (!methodData.targetMethod.IsPublic)
                contentPath += " " + (methodData.targetMethod.IsPrivate ? "<private>" : "<internal>");

            if (methodData.targetMethod.GetCustomAttributes(typeof(System.ObsoleteAttribute), true).Length > 0)
                contentPath += " <obsolete>";

            methodData.listenerElement = elementProperty;

            SerializedProperty serializedTargetObject = elementProperty.FindPropertyRelative("m_Target");
            SerializedProperty serializedMethodName = elementProperty.FindPropertyRelative("m_MethodName");
            SerializedProperty serializedMode = elementProperty.FindPropertyRelative("m_Mode");

            bool itemOn = serializedTargetObject.objectReferenceValue == methodData.targetObject &&
                          serializedMethodName.stringValue == methodData.targetMethod.Name &&
                          serializedMode.enumValueIndex == (int)methodData.listenerMode;

            menu.AddItem(new GUIContent(contentPath), itemOn, SetEventFunctionCallback, methodData);
        }

        void BuildMenuForObject(Object targetObject, SerializedProperty elementProperty, GenericMenu menu, int componentCount = 0)
        {
#if UDONSHARP
            bool isUdonSharpBehaviour = targetObject is UdonBehaviour udonBehaviour && UdonSharpEditorUtility.IsUdonSharpBehaviour(udonBehaviour);
#endif

            List<FunctionData> methodInfos = new List<FunctionData>();
            string contentPath = targetObject.GetType().Name + (componentCount > 0 ? string.Format("({0})", componentCount) : "")
#if UDONSHARP
                + (isUdonSharpBehaviour ? $" ({UdonSharpEditorUtility.GetUdonSharpBehaviourType((UdonBehaviour)targetObject)})" : "")
#endif
                + "/";

            FindValidMethods(targetObject, PersistentListenerMode.Void, methodInfos);
            FindValidMethods(targetObject, PersistentListenerMode.Int, methodInfos);
            FindValidMethods(targetObject, PersistentListenerMode.Float, methodInfos);
            FindValidMethods(targetObject, PersistentListenerMode.String, methodInfos);
            FindValidMethods(targetObject, PersistentListenerMode.Bool, methodInfos);
            FindValidMethods(targetObject, PersistentListenerMode.Object, methodInfos);

            methodInfos = methodInfos.OrderBy(method1 => method1.targetMethod.Name.StartsWith("set_") ? 0 : 1).ThenBy((method1) => method1.targetMethod.Name).ToList();

            // Get event args to determine if we can do a pass through of the arg to the parameter
            System.Type[] eventArgs = dummyEvent.GetType().GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();

            bool dynamicBinding = false;
            
#if UDONSHARP
            if (isUdonSharpBehaviour)
                menu.AddSeparator("");
#endif

            if (eventArgs.Length > 0)
            {
                List<FunctionData> dynamicMethodInfos = new List<FunctionData>();
                FindValidMethods(targetObject, PersistentListenerMode.EventDefined, dynamicMethodInfos, eventArgs);

                if (dynamicMethodInfos.Count > 0)
                {
                    dynamicMethodInfos = dynamicMethodInfos.OrderBy(m => m.targetMethod.Name.StartsWith("set") ? 0 : 1).ThenBy(m => m.targetMethod.Name).ToList();

                    dynamicBinding = true;

                    // Add dynamic header
                    menu.AddDisabledItem(new GUIContent(contentPath + string.Format("Dynamic {0}", GetTypeName(eventArgs[0]))));
                    menu.AddSeparator(contentPath);

                    foreach (FunctionData dynamicMethod in dynamicMethodInfos)
                    {
                        AddFunctionToMenu(contentPath, elementProperty, dynamicMethod, menu, 0, true);
                    }
                }
            }

            // Add static header if we have dynamic bindings
            if (dynamicBinding)
            {
                menu.AddDisabledItem(new GUIContent(contentPath + "Static Parameters"));
                menu.AddSeparator(contentPath);
            }

            foreach (FunctionData method in methodInfos)
            {
                AddFunctionToMenu(contentPath, elementProperty, method, menu, componentCount);
            }

#if UDONSHARP
            // Push SendCustomEvent up to the top level menu and create a separator since it is a very commonly used method on UdonBehaviours
            if (isUdonSharpBehaviour)
            {
                FunctionData sendCustomEventMethod = methodInfos.First(e => e.targetMethod.Name == "SendCustomEvent");
                AddFunctionToMenu($"UdonBehaviour{(componentCount > 0 ? string.Format("({0}) ", componentCount) : " ")}", elementProperty, sendCustomEventMethod, menu, componentCount);
            }
#endif
        }

        class ComponentTypeCount
        {
            public int TotalCount = 0;
            public int CurrentCount = 1;
        }

        GenericMenu BuildPopupMenu(Object targetObj, SerializedProperty elementProperty, System.Type objectArgType)
        {
            GenericMenu menu = new GenericMenu();

            string currentMethodName = elementProperty.FindPropertyRelative("m_MethodName").stringValue;

            menu.AddItem(new GUIContent("No Function"), string.IsNullOrEmpty(currentMethodName), ClearEventFunctionCallback, new FunctionData(elementProperty));
            menu.AddSeparator("");
        
            if (targetObj is Component)
            {
                targetObj = (targetObj as Component).gameObject;
            }
            else if (!(targetObj is GameObject))
            {
                // Function menu for asset objects and such
                BuildMenuForObject(targetObj, elementProperty, menu);
                return menu;
            }

            // GameObject menu
            BuildMenuForObject(targetObj, elementProperty, menu);

            Component[] components = (targetObj as GameObject).GetComponents<Component>();
            Dictionary<System.Type, ComponentTypeCount> componentTypeCounts = new Dictionary<System.Type, ComponentTypeCount>();

            // Only get the first instance of each component type
            if (cachedSettings.groupSameComponentType)
            {
                components = components.GroupBy(comp => comp.GetType()).Select(group => group.First()).ToArray();
            }
            else // Otherwise we need to know if there are multiple components of a given type before we start going through the components since we only need numbers on component types with multiple instances.
            {
                foreach (Component component in components)
                {
                    ComponentTypeCount typeCount;
                    if (!componentTypeCounts.TryGetValue(component.GetType(), out typeCount))
                    {
                        typeCount = new ComponentTypeCount();
                        componentTypeCounts.Add(component.GetType(), typeCount);
                    }

                    typeCount.TotalCount++;
                }

            }

            foreach (Component component in components)
            {
#if UDONSHARP
                if (cachedSettings.hideOriginalUdonBehaviour &&
                    component is UdonBehaviour udonBehaviour &&
                    udonBehaviour.programSource != null && udonBehaviour.programSource is UdonSharpProgramAsset &&
                    UdonSharpEditorUtility.FindProxyBehaviour(udonBehaviour, ProxySerializationPolicy.NoSerialization) != null)
                    continue;

                if (!cachedSettings.hideOriginalUdonBehaviour &&
                    component is UdonSharpBehaviour proxyBehaviour &&
                    UdonSharpEditorUtility.IsProxyBehaviour(proxyBehaviour))
                    continue;
#endif

                int componentCount = 0;

                if (!cachedSettings.groupSameComponentType)
                {
                    ComponentTypeCount typeCount = componentTypeCounts[component.GetType()];
                    if (typeCount.TotalCount > 1)
                        componentCount = typeCount.CurrentCount++;
                }

                BuildMenuForObject(component, elementProperty, menu, componentCount);
            }

            return menu;
        }

#if UDONSHARP
        private static Dictionary<string, string> builtinEventLookup;
#endif

        // Where the event data actually gets added when you choose a function
        static void SetEventFunctionCallback(object functionUserData)
        {
            FunctionData functionData = functionUserData as FunctionData;

#if UDONSHARP
            if (functionData.targetObject is UdonSharpBehaviour udonSharpBehaviour &&
                UdonSharpEditorUtility.IsProxyBehaviour(udonSharpBehaviour))
            {
                MethodInfo originalTargetMethod = functionData.targetMethod;
                functionData.targetObject = UdonSharpEditorUtility.GetBackingUdonBehaviour(udonSharpBehaviour);
                functionData.targetMethod = typeof(UdonBehaviour).GetMethod("SendCustomEvent");

                if (originalTargetMethod.Name != "SendCustomEvent" && 
                    originalTargetMethod.Name != "SendCustomNetworkEvent")
                {
                    functionData.listenerMode = PersistentListenerMode.String;

                    SerializedProperty serializedArgsFixer = functionData.listenerElement.FindPropertyRelative("m_Arguments");

                    UdonSharpProgramAsset programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(udonSharpBehaviour);

                    // Stolen from the resolver context
                    if (builtinEventLookup == null)
                    {
                        builtinEventLookup = new Dictionary<string, string>();

                        foreach (UdonNodeDefinition nodeDefinition in UdonEditorManager.Instance.GetNodeDefinitions("Event_"))
                        {
                            if (nodeDefinition.fullName == "Event_Custom")
                                continue;

                            string eventNameStr = nodeDefinition.fullName.Substring(6);
                            char[] eventName = eventNameStr.ToCharArray();
                            eventName[0] = char.ToLowerInvariant(eventName[0]);

                            builtinEventLookup.Add(eventNameStr, "_" + new string(eventName));
                        }
                    }

                    string targetMethodName = originalTargetMethod.Name;

                    bool isBuiltin = false;
                    if (builtinEventLookup.ContainsKey(targetMethodName))
                    {
                        targetMethodName = builtinEventLookup[targetMethodName];
                        isBuiltin = true;
                    }

                    SerializedProperty targetMethodProperty = serializedArgsFixer.FindPropertyRelative("m_StringArgument");

                    targetMethodProperty.stringValue = targetMethodName;

                    if (!isBuiltin && !originalTargetMethod.IsPublic)
                        targetMethodProperty.stringValue = "<Custom events called via UI must be public>";
                }
            }
#endif

            SerializedProperty serializedElement = functionData.listenerElement;

            SerializedProperty serializedTarget = serializedElement.FindPropertyRelative("m_Target");
            SerializedProperty serializedMethodName = serializedElement.FindPropertyRelative("m_MethodName");
            SerializedProperty serializedArgs = serializedElement.FindPropertyRelative("m_Arguments");
            SerializedProperty serializedMode = serializedElement.FindPropertyRelative("m_Mode");

            SerializedProperty serializedArgAssembly = serializedArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName");
            SerializedProperty serializedArgObjectValue = serializedArgs.FindPropertyRelative("m_ObjectArgument");

            serializedTarget.objectReferenceValue = functionData.targetObject;
            serializedMethodName.stringValue = functionData.targetMethod.Name;
            serializedMode.enumValueIndex = (int)functionData.listenerMode;

            if (functionData.listenerMode == PersistentListenerMode.Object)
            {
                ParameterInfo[] methodParams = functionData.targetMethod.GetParameters();
                if (methodParams.Length == 1 && typeof(Object).IsAssignableFrom(methodParams[0].ParameterType))
                    serializedArgAssembly.stringValue = methodParams[0].ParameterType.AssemblyQualifiedName;
                else
                    serializedArgAssembly.stringValue = typeof(Object).AssemblyQualifiedName;
            }
            else
            {
                serializedArgAssembly.stringValue = typeof(Object).AssemblyQualifiedName;
                serializedArgObjectValue.objectReferenceValue = null;
            }

            System.Type argType = EasyEventEditorHandler.FindTypeInAllAssemblies(serializedArgAssembly.stringValue);
            if (!typeof(Object).IsAssignableFrom(argType) || !argType.IsInstanceOfType(serializedArgObjectValue.objectReferenceValue))
                serializedArgObjectValue.objectReferenceValue = null;

            functionData.listenerElement.serializedObject.ApplyModifiedProperties();
        }

        static void ClearEventFunctionCallback(object functionUserData)
        {
            FunctionData functionData = functionUserData as FunctionData;

            functionData.listenerElement.FindPropertyRelative("m_Mode").enumValueIndex = (int)PersistentListenerMode.Void;
            functionData.listenerElement.FindPropertyRelative("m_MethodName").stringValue = null;
            functionData.listenerElement.serializedObject.ApplyModifiedProperties();
        }

        void DrawElementCallback(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty element = listenerArray.GetArrayElementAtIndex(index);

            rect.y++;
            Rect[] rects = GetElementRects(rect);

            Rect enabledRect = rects[0];
            Rect gameObjectRect = rects[1];
            Rect functionRect = rects[2];
            Rect argRect = rects[3];

            SerializedProperty serializedCallState = element.FindPropertyRelative("m_CallState");
            SerializedProperty serializedMode = element.FindPropertyRelative("m_Mode");
            SerializedProperty serializedArgs = element.FindPropertyRelative("m_Arguments");
            SerializedProperty serializedTarget = element.FindPropertyRelative("m_Target");
            SerializedProperty serializedMethod = element.FindPropertyRelative("m_MethodName");

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            EditorGUI.PropertyField(enabledRect, serializedCallState, GUIContent.none);

            EditorGUI.BeginChangeCheck();

            Object oldTargetObject = serializedTarget.objectReferenceValue;

            GUI.Box(gameObjectRect, GUIContent.none);
            EditorGUI.PropertyField(gameObjectRect, serializedTarget, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                Object newTargetObject = serializedTarget.objectReferenceValue;

                // Attempt to maintain the function pointer and component pointer if someone changes the target object and it has the correct component type on it.
                if (oldTargetObject != null && newTargetObject != null)
                {
                    if (oldTargetObject.GetType() != newTargetObject.GetType()) // If not an asset, if it is an asset and the same type we don't do anything
                    {
                        // If these are Unity components then the game object that they are attached to may have multiple copies of the same component type so attempt to match the count
                        if (typeof(Component).IsAssignableFrom(oldTargetObject.GetType()) && newTargetObject.GetType() == typeof(GameObject))
                        {
                            GameObject oldParentObject = ((Component)oldTargetObject).gameObject;
                            GameObject newParentObject = (GameObject)newTargetObject;

                            Component[] oldComponentList = oldParentObject.GetComponents(oldTargetObject.GetType());

                            int componentLocationOffset = 0;
                            for (int i = 0; i < oldComponentList.Length; ++i)
                            {
                                if (oldComponentList[i] == oldTargetObject)
                                    break;

                                if (oldComponentList[i].GetType() == oldTargetObject.GetType()) // Only take exact matches for component type since I don't want to do redo the reflection to find the methods at the moment.
                                    componentLocationOffset++;
                            }

                            Component[] newComponentList = newParentObject.GetComponents(oldTargetObject.GetType());

                            int newComponentIndex = 0;
                            int componentCount = -1;
                            for (int i = 0; i < newComponentList.Length; ++i)
                            {
                                if (componentCount == componentLocationOffset)
                                    break;

                                if (newComponentList[i].GetType() == oldTargetObject.GetType())
                                {
                                    newComponentIndex = i;
                                    componentCount++;
                                }
                            }
                        
                            if (newComponentList.Length > 0 && newComponentList[newComponentIndex].GetType() == oldTargetObject.GetType())
                            {
                                serializedTarget.objectReferenceValue = newComponentList[newComponentIndex];
                            }
                            else
                            {
                                serializedMethod.stringValue = null;
                            }
                        }
                        else
                        {
                            serializedMethod.stringValue = null;
                        }
                    }
                }
                else
                {
                    serializedMethod.stringValue = null;
                }
            }

            PersistentListenerMode mode = (PersistentListenerMode)serializedMode.enumValueIndex;

            SerializedProperty argument;
            if (serializedTarget.objectReferenceValue == null || string.IsNullOrEmpty(serializedMethod.stringValue))
                mode = PersistentListenerMode.Void;
        
            switch (mode)
            {
                case PersistentListenerMode.Object:
                case PersistentListenerMode.String:
                case PersistentListenerMode.Bool:
                case PersistentListenerMode.Float:
                    argument = serializedArgs.FindPropertyRelative("m_" + System.Enum.GetName(typeof(PersistentListenerMode), mode) + "Argument");
                    break;
                default:
                    argument = serializedArgs.FindPropertyRelative("m_IntArgument");
                    break;
            }

            string argTypeName = serializedArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue;
            System.Type argType = typeof(Object);
            if (!string.IsNullOrEmpty(argTypeName))
                argType = EasyEventEditorHandler.FindTypeInAllAssemblies(argTypeName) ?? typeof (Object);

            if (mode == PersistentListenerMode.Object)
            {
                EditorGUI.BeginChangeCheck();
                Object result = EditorGUI.ObjectField(argRect, GUIContent.none, argument.objectReferenceValue, argType, true);
                if (EditorGUI.EndChangeCheck())
                    argument.objectReferenceValue = result;
            }
            else if (mode != PersistentListenerMode.Void && mode != PersistentListenerMode.EventDefined)
                EditorGUI.PropertyField(argRect, argument, GUIContent.none);

            EditorGUI.BeginDisabledGroup(serializedTarget.objectReferenceValue == null);
            {
                EditorGUI.BeginProperty(functionRect, GUIContent.none, serializedMethod);

                GUIContent buttonContent;

                if (EditorGUI.showMixedValue)
                {
                    buttonContent = new GUIContent("\u2014", "Mixed Values");
                }
                else
                {
                    if (serializedTarget.objectReferenceValue == null || string.IsNullOrEmpty(serializedMethod.stringValue))
                    {
                        buttonContent = new GUIContent("No Function");
                    }
                    else
                    {
                        buttonContent = new GUIContent(GetFunctionDisplayName(serializedTarget, serializedMethod, mode, argType, cachedSettings.displayArgumentType));
                    }
                }

                if (GUI.Button(functionRect, buttonContent, EditorStyles.popup))
                {
                    BuildPopupMenu(serializedTarget.objectReferenceValue, element, argType).DropDown(functionRect);
                }

                EditorGUI.EndProperty();
            }
            EditorGUI.EndDisabledGroup();
        }

        void SelectCallback(ReorderableList list)
        {
            currentState.lastSelectedIndex = list.index;
        }

        void ReorderCallback(ReorderableList list)
        {
            currentState.lastSelectedIndex = list.index;
        }

        void AddEventListener(ReorderableList list)
        {
            if (listenerArray.hasMultipleDifferentValues)
            {
                foreach (Object targetObj in listenerArray.serializedObject.targetObjects)
                {
                    SerializedObject tempSerializedObject = new SerializedObject(targetObj);
                    SerializedProperty listenerArrayProperty = tempSerializedObject.FindProperty(listenerArray.propertyPath);
                    listenerArrayProperty.arraySize += 1;
                    tempSerializedObject.ApplyModifiedProperties();
                }

                listenerArray.serializedObject.SetIsDifferentCacheDirty();
                listenerArray.serializedObject.Update();
                list.index = list.serializedProperty.arraySize - 1;
            }
            else
            {
                ReorderableList.defaultBehaviours.DoAddButton(list);
            }

            currentState.lastSelectedIndex = list.index;

            // Init default state
            SerializedProperty serialiedListener = listenerArray.GetArrayElementAtIndex(list.index);
            ResetEventState(serialiedListener);
        }

        void ResetEventState(SerializedProperty serialiedListener)
        {
            SerializedProperty serializedCallState = serialiedListener.FindPropertyRelative("m_CallState");
            SerializedProperty serializedTarget = serialiedListener.FindPropertyRelative("m_Target");
            SerializedProperty serializedMethodName = serialiedListener.FindPropertyRelative("m_MethodName");
            SerializedProperty serializedMode = serialiedListener.FindPropertyRelative("m_Mode");
            SerializedProperty serializedArgs = serialiedListener.FindPropertyRelative("m_Arguments");

            serializedCallState.enumValueIndex = (int)UnityEventCallState.RuntimeOnly;
            serializedTarget.objectReferenceValue = null;
            serializedMethodName.stringValue = null;
            serializedMode.enumValueIndex = (int)PersistentListenerMode.Void;

            serializedArgs.FindPropertyRelative("m_IntArgument").intValue = 0;
            serializedArgs.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
            serializedArgs.FindPropertyRelative("m_BoolArgument").boolValue = false;
            serializedArgs.FindPropertyRelative("m_StringArgument").stringValue = null;
            serializedArgs.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
            serializedArgs.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = null;
        }

        void RemoveCallback(ReorderableList list)
        {
            if (currentState.reorderableList.count > 0)
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                currentState.lastSelectedIndex = list.index;
            }
        }
    }

} // namespace Merlin

#endif
