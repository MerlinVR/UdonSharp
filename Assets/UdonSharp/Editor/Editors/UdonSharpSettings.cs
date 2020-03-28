using System.Collections;
using System.Collections.Generic;
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

        public bool autoCompileOnModify = true;
        public bool compileAllScripts = true;
        public TextAsset newScriptTemplateOverride = null;

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

        public static string GetProgramTemplateString()
        {
            UdonSharpSettings settings = GetSettings();

            if (settings != null && settings.newScriptTemplateOverride != null)
                return settings.newScriptTemplateOverride.ToString();

            return DefaultProgramTemplate;
        }
    }
    
    public class UdonSharpSettingsProvider
    {
        private static GUIContent autoCompileLabel = new GUIContent("Auto compile on modify", "Trigger a compile whenever a U# source file is modified.");
        private static GUIContent compileAllLabel = new GUIContent("Compile all scripts", "Compile all scripts when a script is modified. This prevents some potential for weird issues where classes don't match");
        private static GUIContent templateOverrideLabel = new GUIContent("Script template override", "A custom override file to use as a template for newly created U# files. Put \"<TemplateClassName>\" in place of a class name for it to automatically populate with the file name.");
        private static GUIContent includeDebugInfoLabel = new GUIContent("Debug build", "Include debug info in build");
        private static GUIContent includeInlineCodeLabel = new GUIContent("Inline code", "Include C# inline in generated assembly");
        private static GUIContent listenForVRCExceptionsLabel = new GUIContent("Listen for client exceptions", "Listens for exceptions from Udon and tries to match them to scripts in the project");

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

                    EditorGUILayout.PropertyField(settingsObject.FindProperty("newScriptTemplateOverride"), templateOverrideLabel);

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
