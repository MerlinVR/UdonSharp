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
        public TextAsset newScriptTemplateOverride = null;

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
        private static GUIContent templateOverrideLabel = new GUIContent("Script template override", "A custom override file to use as a template for newly created U# files. Put \"<TemplateClassName>\" in place of a class name for it to automatically populate with the file name.");

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            SettingsProvider provider = new SettingsProvider("Project/Udon Sharp", SettingsScope.Project)
            {
                label = "Udon Sharp",
                keywords = new HashSet<string>(new string[] { "Udon", "Sharp", "U#", "VRC", "VRChat" }),
                guiHandler = (searchContext) =>
                {
                    SerializedObject settings = UdonSharpSettingsObject.GetSerializedSettings();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(settings.FindProperty("autoCompileOnModify"), autoCompileLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("newScriptTemplateOverride"), templateOverrideLabel);

                    if (EditorGUI.EndChangeCheck())
                    {
                        settings.ApplyModifiedProperties();
                        EditorUtility.SetDirty(UdonSharpSettingsObject.GetOrCreateSettings());
                    }
                },
            };

            return provider;
        }


    }
}
