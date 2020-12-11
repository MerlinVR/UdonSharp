
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace UdonSharp
{
    public class UdonSharpSettings : ScriptableObject
    {
        public enum LogWatcherMode
        {
            Disabled,
            AllLogs,
            Prefix,
        }

        private const string SettingsSavePath = "Assets/UdonSharp/UdonSharpSettings.asset";

        private const string DefaultProgramTemplate = @"
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class <TemplateClassName> : UdonSharpBehaviour
{
    void Start()
    {
        
    }
}
";

        private static readonly string[] BuiltinScanningBlacklist = new string[]
        {
            "Assets/Udon/Editor/",
            "Assets/Udon/Serialization/",
            "Assets/VRChat Examples/",
            "Assets/VRCSDK/Dependencies/",
            "Assets/UdonSharp/Editor/",
            // Common 3rd party editor assets
            "Assets/AmplifyShaderEditor/",
            "Assets/AmplifyImpostors/",
            "Assets/Bakery/",
            "Assets/Editor/x64/Bakery/",
            "Assets/Procedural Worlds/", // Gaia
            "Assets/Pavo Studio/", // Muscle editor
            "Assets/Plugins/RootMotion/", // FinalIK
        };

        // Compiler settings
        public bool autoCompileOnModify = true;
        public bool compileAllScripts = true;
        public bool waitForFocus = false;
        public bool disableUploadCompile = false;
        public TextAsset newScriptTemplateOverride = null;

        public string[] scanningDirectoryBlacklist = new string[0];

        // Interface settings
        public string defaultBehaviourInterfaceType = "";

        // Debug settings
        public bool buildDebugInfo = true;
        public bool includeInlineCode = true;
        public bool listenForVRCExceptions = true;

        public bool shouldForceCompile = false;

        // Log watcher
        public LogWatcherMode watcherMode = LogWatcherMode.Disabled;
        public string[] logWatcherMatchStrings = new string[0];

        public static UdonSharpSettings GetSettings()
        {
            UdonSharpSettings settings = AssetDatabase.LoadAssetAtPath<UdonSharpSettings>(SettingsSavePath);
            
            return settings;
        }

        internal static UdonSharpSettings GetOrCreateSettings()
        {
            UdonSharpSettings settings = AssetDatabase.LoadAssetAtPath<UdonSharpSettings>(SettingsSavePath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<UdonSharpSettings>();
                AssetDatabase.CreateAsset(settings, SettingsSavePath);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        static string SanitizeName(string name)
        {
            return name.Replace(" ", "")
                        .Replace("#", "Sharp")
                        .Replace("(", "")
                        .Replace(")", "")
                        .Replace("*", "")
                        .Replace("<", "")
                        .Replace(">", "")
                        .Replace("-", "_")
                        .Replace("!", "")
                        .Replace("$", "")
                        .Replace("@", "")
                        .Replace("+", "");
        }

        // Unity does not like having scripts with different names from their classes and will start breaking things weirdly, so enforce it by default. 
        // If people really want to rename the asset afterwards they can, but there will be a compile warning that they can't get rid of without fixing the names.
        internal static string SanitizeScriptFilePath(string file)
        {
            string fileName = SanitizeName(Path.GetFileNameWithoutExtension(file));

            string filePath = Path.GetDirectoryName(file);

            return $"{filePath}/{fileName}{Path.GetExtension(file)}";
        }

        public static string GetProgramTemplateString(string scriptName)
        {
            scriptName = SanitizeName(scriptName);

            UdonSharpSettings settings = GetSettings();

            string templateStr;

            if (settings != null && settings.newScriptTemplateOverride != null)
                templateStr = settings.newScriptTemplateOverride.ToString();
            else
                templateStr = DefaultProgramTemplate;

            templateStr = templateStr.Replace("<TemplateClassName>", scriptName);

            return templateStr;
        }

        public static string[] GetScannerBlacklist()
        {
            UdonSharpSettings settings = GetSettings();

            if (settings != null)
                return BuiltinScanningBlacklist.Concat(settings.scanningDirectoryBlacklist).ToArray();

            return BuiltinScanningBlacklist;
        }
    }
    
    public class UdonSharpSettingsProvider
    {
        private static readonly GUIContent autoCompileLabel = new GUIContent("Auto compile on modify", "Trigger a compile whenever a U# source file is modified.");
        private static readonly GUIContent compileAllLabel = new GUIContent("Compile all scripts", "Compile all scripts when a script is modified. This prevents some potential for weird issues where classes don't match");
        private static readonly GUIContent waitForFocusLabel = new GUIContent("Compile on focus", "Waits for application focus to compile any changed U# scripts");
        private static readonly GUIContent disableUploadCompileLabel = new GUIContent("Disable compile on upload", "Disables U# compile step on upload. This is not recommended unless you absolutely cannot deal with the compile on upload step.");
        private static readonly GUIContent templateOverrideLabel = new GUIContent("Script template override", "A custom override file to use as a template for newly created U# files. Put \"<TemplateClassName>\" in place of a class name for it to automatically populate with the file name.");
        private static readonly GUIContent includeDebugInfoLabel = new GUIContent("Debug build", "Include debug info in build");
        private static readonly GUIContent includeInlineCodeLabel = new GUIContent("Inline code", "Include C# inline in generated assembly");
        private static readonly GUIContent listenForVRCExceptionsLabel = new GUIContent("Listen for client exceptions", "Listens for exceptions from Udon and tries to match them to scripts in the project");
        private static readonly GUIContent scanningBlackListLabel = new GUIContent("Scanning blacklist", "Directories to not watch for source code changes and not include in class lookups");
        private static readonly GUIContent forceCompileLabel = new GUIContent("Force compile on upload", "Forces Unity to synchronously compile scripts when a world build is started. Unity will complain and throw errors, but it seems to work. This is a less intrusive way to prevent Unity from corrupting assemblies on upload.");
        private static readonly GUIContent outputLogWatcherModeLabel = new GUIContent("Output log watch mode", "The log watcher will read log messages from the VRC log and forward them to the editor's console. Prefix mode will only show messages with a given prefix string.");
        private static readonly GUIContent prefixArrayLabel = new GUIContent("Prefixes", "The list of prefixes that the log watcher will forward to the editor from in-game");
        private static readonly GUIContent defaultBehaviourEditorLabel = new GUIContent("Default Behaviour Editor", "The default editor for U# behaviours, this is what will handle inspector drawing by default.");

        static string DrawCustomEditorSelection(string currentSelection)
        {
            List<(string, string)> optionsList = new List<(string, string)>() { ("", "Default") };
            optionsList.AddRange(UdonSharpCustomEditorManager._defaultInspectorMap.Select(e => (e.Key, e.Value.Item1)));

            int[] values = Enumerable.Range(0, optionsList.Count).ToArray();

            int currentValue = 0;
            if (currentSelection == "")
            {
                currentValue = 0;
            }
            else
            {
                for (int i = 1; i < optionsList.Count; ++i)
                {
                    if (currentSelection == optionsList[i].Item1)
                    {
                        currentValue = i;
                        break;
                    }
                }
            }

            int newSelection = EditorGUILayout.IntPopup(defaultBehaviourEditorLabel, currentValue, optionsList.Select(e => new GUIContent(e.Item2)).ToArray(), values);

            string newSelectionStr = "";
            if (newSelection > 0)
                newSelectionStr = optionsList[newSelection].Item1;

            if (newSelection != 0)
                EditorGUILayout.HelpBox("Selecting an editor other than the default editor will require a C# script recompile to update the inspector with newly added/removed fields.", MessageType.Info);

            return newSelectionStr;
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            SettingsProvider provider = new SettingsProvider("Project/Udon Sharp", SettingsScope.Project)
            {
                label = "Udon Sharp",
                keywords = new HashSet<string>(new string[] { "Udon", "Sharp", "U#", "VRC", "VRChat" }),
                guiHandler = (searchContext) =>
                {
                    UdonSharpSettings settings = UdonSharpSettings.GetOrCreateSettings();
                    SerializedObject settingsObject = UdonSharpSettings.GetSerializedSettings();

                    // Compiler settings
                    EditorGUILayout.LabelField("Compiler", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.autoCompileOnModify)), autoCompileLabel);
                    
                    if (settings.autoCompileOnModify)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.compileAllScripts)), compileAllLabel);
                        if (!settings.compileAllScripts)
                            EditorGUILayout.HelpBox("Only compiling the script that has been modified can cause issues if you have multiple scripts communicating via methods.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.waitForFocus)), waitForFocusLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.disableUploadCompile)), disableUploadCompileLabel);

                    if (settings.disableUploadCompile)
                    {
                        EditorGUILayout.HelpBox(@"Do not disable this setting unless it is not viable to wait for the compile on upload process. 
Disabling this setting will make the UNITY_EDITOR define not work as expected and will break prefabs that depend on the define being accurate between game and editor builds.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.newScriptTemplateOverride)), templateOverrideLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.scanningDirectoryBlacklist)), scanningBlackListLabel, true);

                    EditorGUILayout.Space();

                    // Interface settings
                    EditorGUILayout.LabelField("Interface", EditorStyles.boldLabel);

                    SerializedProperty defaultDrawerProperty = settingsObject.FindProperty(nameof(UdonSharpSettings.defaultBehaviourInterfaceType));

                    defaultDrawerProperty.stringValue = DrawCustomEditorSelection(defaultDrawerProperty.stringValue);

                    EditorGUILayout.Space();

                    // Debugging settings
                    EditorGUILayout.LabelField("Debugging", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.buildDebugInfo)), includeDebugInfoLabel);

                    if (settings.buildDebugInfo)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.includeInlineCode)), includeInlineCodeLabel);
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.listenForVRCExceptions)), listenForVRCExceptionsLabel);
                    }

                    EditorGUILayout.Space();
                    SerializedProperty watcherModeProperty = settingsObject.FindProperty(nameof(UdonSharpSettings.watcherMode));
                    EditorGUILayout.PropertyField(watcherModeProperty, outputLogWatcherModeLabel);
                    
                    if (watcherModeProperty.enumValueIndex == (int)UdonSharpSettings.LogWatcherMode.Prefix)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.logWatcherMatchStrings)), prefixArrayLabel, true);
                    }

                    EditorGUILayout.Space();

                    // Experimental settings
                    EditorGUILayout.LabelField("Experimental", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.shouldForceCompile)), forceCompileLabel);

                    if (EditorGUI.EndChangeCheck())
                    {
                        settingsObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(UdonSharpSettings.GetSettings());
                    }
                },
            };

            return provider;
        }


    }
}
