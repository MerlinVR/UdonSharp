
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UdonSharp.Updater;
using UnityEditor;
using UnityEngine;

namespace UdonSharpEditor
{
    internal class UdonSharpSettings : ScriptableObject
    {
        public enum LogWatcherMode
        {
            Disabled,
            AllLogs,
            Prefix,
        }

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

        private static readonly string[] BuiltinScanningBlacklist = {
            "Assets/Udon/Editor/",
            "Assets/Udon/Serialization/",
            "Assets/Udon/ProgramSources/",
            "Assets/Udon/WrapperModules/",
            "Assets/VRChat Examples/",
            "Assets/VRCSDK/Dependencies/",
            "Assets/VRCSDK/SDK3/",
            "Assets/VRCSDK/Sample Assets/",
            "Assets/UdonSharp/Editor/",
            // Common 3rd party editor assets
            "Assets/AmplifyShaderEditor/",
            "Assets/AmplifyImpostors/",
            "Assets/Bakery/",
            "Assets/Editor/x64/Bakery/",
            "Assets/Procedural Worlds/", // Gaia
            "Assets/Pavo Studio/", // Muscle editor
            "Assets/Plugins/RootMotion/", // FinalIK
            "Assets/CyanEmu/", // References VRC stuff that's excluded
            "Assets/PolyFew/", // References Tuple type redundantly incorrectly and is not in an asmdef
        };

        // Compiler settings
        public bool autoCompileOnModify = true;
        public bool waitForFocus = false;
        public bool disableUploadCompile = false;
        public TextAsset newScriptTemplateOverride = null;

        public string[] scanningDirectoryBlacklist = Array.Empty<string>();

        // Interface settings
        public string defaultBehaviourInterfaceType = "";

        // Debug settings
        public bool buildDebugInfo = true;
        public bool includeInlineCode = true;
        public bool listenForVRCExceptions = true;

        public bool shouldForceCompile = false;

        // Log watcher
        public LogWatcherMode watcherMode = LogWatcherMode.Disabled;
        public string[] logWatcherMatchStrings = Array.Empty<string>();

        private static UdonSharpSettings _settings;
        
        public static UdonSharpSettings GetSettings()
        {
            if (_settings)
                return _settings;
            
            UdonSharpSettings settings = AssetDatabase.LoadAssetAtPath<UdonSharpSettings>(UdonSharpLocator.SettingsPath);

            if (settings == null)
                _settings = settings = CreateInstance<UdonSharpSettings>();
            
            return settings;
        }

        internal static UdonSharpSettings GetOrCreateSettings()
        {
            string settingsPath = UdonSharpLocator.SettingsPath;
            UdonSharpSettings settings = AssetDatabase.LoadAssetAtPath<UdonSharpSettings>(settingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(settingsPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                
                _settings = settings = CreateInstance<UdonSharpSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        private static string SanitizeName(string name)
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

            string filePath = Path.GetDirectoryName(file).Replace('\\', '/');

            string projectRoot = Path.GetDirectoryName( Application.dataPath ).Replace('\\', '/');

            //make sure the path is relative to the project root
            if (filePath.StartsWith(projectRoot))
                filePath = filePath.Substring(projectRoot.Length + 1); //add 1 to remove trailing slash

            return $"{filePath}/{fileName}{Path.GetExtension(file)}";
        }

        public static string GetProgramTemplateString(string scriptName)
        {
            scriptName = SanitizeName(scriptName);

            UdonSharpSettings settings = GetSettings();

            string templateStr = settings.newScriptTemplateOverride != null ? settings.newScriptTemplateOverride.ToString() : DefaultProgramTemplate;

            templateStr = templateStr.Replace("<TemplateClassName>", scriptName);

            return templateStr;
        }

        private static string[] GetScannerBlacklist()
        {
            return BuiltinScanningBlacklist.Concat(GetSettings().scanningDirectoryBlacklist).ToArray();
        }

        public static bool IsBlacklistedPath(string path)
        {
            string[] blackList = GetScannerBlacklist();

            path = path.Replace('\\', '/');

            foreach (string blacklistPath in blackList)
            {
                if (path.StartsWith(blacklistPath.Replace('\\', '/')))
                    return true;
            }

            return false;
        }

        public static IEnumerable<string> FilterBlacklistedPaths(IEnumerable<string> paths)
        {
            // todo: use hashset instead of n*m comparisons
            List<string> filteredPaths = new List<string>();
            string[] blacklist = GetScannerBlacklist();
            for (int i = 0; i < blacklist.Length; ++i)
                blacklist[i] = blacklist[i].Replace('\\', '/');

            foreach (string originalPath in paths)
            {
                string replacedOriginal = originalPath.Replace('\\', '/');

                bool blackListed = false;
                foreach (string blacklistPath in blacklist)
                {
                    if (replacedOriginal.StartsWith(blacklistPath, StringComparison.OrdinalIgnoreCase))
                    {
                        blackListed = true;
                        break;
                    }
                }

                if (!blackListed)
                    filteredPaths.Add(replacedOriginal);
            }

            return filteredPaths;
        }
    }
    
    internal static class UdonSharpSettingsProvider
    {
        private static readonly GUIContent _autoCompileLabel = new GUIContent("Auto compile on modify", "Trigger a compile whenever a U# source file is modified.");
        private static readonly GUIContent _waitForFocusLabel = new GUIContent("Compile on focus", "Waits for application focus to compile any changed U# scripts");
        private static readonly GUIContent _disableUploadCompileLabel = new GUIContent("Disable compile on upload", "Disables U# compile step on upload. This is not recommended unless you absolutely cannot deal with the compile on upload step.");
        private static readonly GUIContent _templateOverrideLabel = new GUIContent("Script template override", "A custom override file to use as a template for newly created U# files. Put \"<TemplateClassName>\" in place of a class name for it to automatically populate with the file name.");
        private static readonly GUIContent _includeDebugInfoLabel = new GUIContent("Debug build", "Include debug info in build");
        private static readonly GUIContent _includeInlineCodeLabel = new GUIContent("Inline code", "Include C# inline in generated assembly");
        private static readonly GUIContent _listenForVrcExceptionsLabel = new GUIContent("Listen for client exceptions", "Listens for exceptions from Udon and tries to match them to scripts in the project");
        private static readonly GUIContent _scanningBlackListLabel = new GUIContent("Scanning blacklist", "Directories to not watch for source code changes and not include in class lookups");
        private static readonly GUIContent _forceCompileLabel = new GUIContent("Force compile on upload", "Forces Unity to synchronously compile scripts when a world build is started. Unity will complain and throw errors, but it seems to work. This is a less intrusive way to prevent Unity from corrupting assemblies on upload.");
        private static readonly GUIContent _outputLogWatcherModeLabel = new GUIContent("Output log watch mode", "The log watcher will read log messages from the VRC log and forward them to the editor's console. Prefix mode will only show messages with a given prefix string.");
        private static readonly GUIContent _prefixArrayLabel = new GUIContent("Prefixes", "The list of prefixes that the log watcher will forward to the editor from in-game");
        private static readonly GUIContent _defaultBehaviourEditorLabel = new GUIContent("Default Behaviour Editor", "The default editor for U# behaviours, this is what will handle inspector drawing by default.");

        private static string DrawCustomEditorSelection(string currentSelection)
        {
            List<(string, string)> optionsList = new List<(string, string)>() { ("", "Default") };
            optionsList.AddRange(UdonSharpCustomEditorManager.DefaultInspectorMap.Select(e => (e.Key, e.Value.Item1)));

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

            int newSelection = EditorGUILayout.IntPopup(_defaultBehaviourEditorLabel, currentValue, optionsList.Select(e => new GUIContent(e.Item2)).ToArray(), values);

            string newSelectionStr = "";
            if (newSelection > 0)
                newSelectionStr = optionsList[newSelection].Item1;

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
                    SerializedObject settingsObject = new SerializedObject(settings);

                    // Compiler settings
                    EditorGUILayout.LabelField("Compiler", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.autoCompileOnModify)), _autoCompileLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.waitForFocus)), _waitForFocusLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.disableUploadCompile)), _disableUploadCompileLabel);

                    if (settings.disableUploadCompile)
                    {
                        EditorGUILayout.HelpBox(@"Do not disable this setting unless it is not viable to wait for the compile on upload process. 
Disabling this setting will make the UNITY_EDITOR define not work as expected and will break prefabs that depend on the define being accurate between game and editor builds.", MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.newScriptTemplateOverride)), _templateOverrideLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.scanningDirectoryBlacklist)), _scanningBlackListLabel, true);

                    EditorGUILayout.Space();

                    // Interface settings
                    EditorGUILayout.LabelField("Interface", EditorStyles.boldLabel);

                    SerializedProperty defaultDrawerProperty = settingsObject.FindProperty(nameof(UdonSharpSettings.defaultBehaviourInterfaceType));

                    defaultDrawerProperty.stringValue = DrawCustomEditorSelection(defaultDrawerProperty.stringValue);

                    EditorGUILayout.Space();

                    // Debugging settings
                    EditorGUILayout.LabelField("Debugging", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.buildDebugInfo)), _includeDebugInfoLabel);

                    if (settings.buildDebugInfo)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.includeInlineCode)), _includeInlineCodeLabel);
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.listenForVRCExceptions)), _listenForVrcExceptionsLabel);
                    }

                    EditorGUILayout.Space();
                    SerializedProperty watcherModeProperty = settingsObject.FindProperty(nameof(UdonSharpSettings.watcherMode));
                    EditorGUILayout.PropertyField(watcherModeProperty, _outputLogWatcherModeLabel);
                    
                    if (watcherModeProperty.enumValueIndex == (int)UdonSharpSettings.LogWatcherMode.Prefix)
                    {
                        EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.logWatcherMatchStrings)), _prefixArrayLabel, true);
                    }

                    EditorGUILayout.Space();

                    // Experimental settings
                    EditorGUILayout.LabelField("Experimental", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(settingsObject.FindProperty(nameof(UdonSharpSettings.shouldForceCompile)), _forceCompileLabel);

                    if (EditorGUI.EndChangeCheck())
                    {
                        settingsObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                    }
                },
            };

            return provider;
        }
    }
}
