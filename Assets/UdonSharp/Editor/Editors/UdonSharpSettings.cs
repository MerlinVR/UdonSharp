using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UdonSharp
{
    public class UdonSharpSettingsObject : ScriptableObject
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

        internal static UdonSharpSettingsObject GetOrCreateSettings()
        {
            UdonSharpSettingsObject settings = AssetDatabase.LoadAssetAtPath<UdonSharpSettingsObject>(SettingsSavePath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<UdonSharpSettingsObject>();
                AssetDatabase.CreateAsset(settings, SettingsSavePath);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        internal static string GetProgramTemplateString()
        {
            UdonSharpSettingsObject settings = GetOrCreateSettings();

            if (settings.newScriptTemplateOverride != null)
                return settings.newScriptTemplateOverride.ToString();

            return DefaultProgramTemplate;
        }
    }

    public class UdonSharpSettingsProvider
    {
        private static GUIContent autoCompileLabel = new GUIContent("Auto compile on modify", "Trigger a compile whenever a U# source file is modified.");
        private static GUIContent compileAllLabel = new GUIContent("Compile all scripts", "Compile all scripts when a script is modified. This prevents some potential for weird issues where classes don't match");
        private static GUIContent templateOverrideLabel = new GUIContent("Script template override", "A custom override file to use as a template for newly created U# files. Put \"<TemplateClassName>\" in place of a class name for it to automatically populate with the file name.");
        private static GUIContent includeDebugInfo = new GUIContent("Debug build", "Include debug info in build");
        private static GUIContent includeInlineCode = new GUIContent("Inline code", "Include C# inline in generated assembly");

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            SettingsProvider provider = new SettingsProvider("Project/Udon Sharp", SettingsScope.Project)
            {
                label = "Udon Sharp",
                keywords = new HashSet<string>(new string[] { "Udon", "Sharp", "U#", "VRC", "VRChat" }),
                guiHandler = (searchContext) =>
                {
                    UdonSharpSettingsObject settings = UdonSharpSettingsObject.GetOrCreateSettings();
                    SerializedObject settingsObject = UdonSharpSettingsObject.GetSerializedSettings();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettingsObject.autoCompileOnModify)), autoCompileLabel);
                    
                    if (settings.autoCompileOnModify)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettingsObject.compileAllScripts)), compileAllLabel);
                        if (!settings.compileAllScripts)
                            EditorGUILayout.HelpBox("Only compiling the script that has been modified can cause issues if you have multiple scripts communicating via methods.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(settingsObject.FindProperty("newScriptTemplateOverride"), templateOverrideLabel);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Debugging", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettingsObject.buildDebugInfo)), includeDebugInfo);

                    if (settings.buildDebugInfo)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettingsObject.includeInlineCode)), includeInlineCode);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        settingsObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(UdonSharpSettingsObject.GetOrCreateSettings());
                    }
                },
            };

            return provider;
        }


    }
}
