
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UdonSharp
{
    public class UdonSharpSettings : ScriptableObject
    {
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

        public bool autoCompileOnModify = true;
        public bool compileAllScripts = true;
        public bool waitForFocus = false;
        public TextAsset newScriptTemplateOverride = null;

        public string[] scanningDirectoryBlacklist = new string[0];

        public bool buildDebugInfo = true;
        public bool includeInlineCode = true;
        public bool listenForVRCExceptions = false;

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
                Debug.LogWarning("Settings null!");

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

        public static string GetProgramTemplateString(string scriptName)
        {
            scriptName = scriptName.Replace(" ", "")
                                   .Replace("#", "Sharp")
                                   .Replace("(", "")
                                   .Replace(")", "")
                                   .Replace("*", "")
                                   .Replace("<", "")
                                   .Replace(">", "")
                                   .Replace("-", "_");

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
        private static readonly GUIContent templateOverrideLabel = new GUIContent("Script template override", "A custom override file to use as a template for newly created U# files. Put \"<TemplateClassName>\" in place of a class name for it to automatically populate with the file name.");
        private static readonly GUIContent includeDebugInfoLabel = new GUIContent("Debug build", "Include debug info in build");
        private static readonly GUIContent includeInlineCodeLabel = new GUIContent("Inline code", "Include C# inline in generated assembly");
        private static readonly GUIContent listenForVRCExceptionsLabel = new GUIContent("Listen for client exceptions", "Listens for exceptions from Udon and tries to match them to scripts in the project");
        private static readonly GUIContent scanningBlackListLabel = new GUIContent("Scanning blacklist", "Directories to not watch for source code changes and not include in class lookups");

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

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.autoCompileOnModify)), autoCompileLabel);
                    
                    if (settings.autoCompileOnModify)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.compileAllScripts)), compileAllLabel);
                        if (!settings.compileAllScripts)
                            EditorGUILayout.HelpBox("Only compiling the script that has been modified can cause issues if you have multiple scripts communicating via methods.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.waitForFocus)), waitForFocusLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.newScriptTemplateOverride)), templateOverrideLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.scanningDirectoryBlacklist)), scanningBlackListLabel, true);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Debugging", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.buildDebugInfo)), includeDebugInfoLabel);

                    if (settings.buildDebugInfo)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.includeInlineCode)), includeInlineCodeLabel);
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.listenForVRCExceptions)), listenForVRCExceptionsLabel);
                    }

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
