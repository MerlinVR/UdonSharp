using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UdonSharp
{

    /// <summary>
    /// "Why not use an AssetPostprocessor?" You may wonder. 
    /// This class is used in lieu of the asset post processor because this method will still work if users turn off the auto refresh on their preferences.
    /// Turning off the auto asset refresh will prevent Unity from recompiling and reloading assemblies every time a UdonSharp script is edited.
    /// This has the downside that we expect the user to know what they're doing and have valid syntax that's getting fed into the compiler since there is no "real" compilation happening on the C#
    /// But the benefit we get is that UdonSharp scripts compile nearly instantly.
    /// So this whole class just exists to give people that option.
    /// 
    /// I may want to rewrite this eventually because the FileSystemWatcher polls updates too frequently and burns CPU for no reason. There is no way to slow down its internal polling as far as I know.
    /// </summary>
    [InitializeOnLoad]
    public class UdonSharpAssetCompileWatcher
    {
        static FileSystemWatcher[] fileSystemWatchers;
        static readonly object modifiedFileLock = new object();

        static HashSet<string> modifiedFilePaths = new HashSet<string>();
        static HashSet<MonoScript> modifiedScripts = new HashSet<MonoScript>();

        static bool lastEnabledState = false;

        static UdonSharpAssetCompileWatcher()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += PlayModeErrorCheck;
        }

        static void SetupWatchers() 
        {
            if (fileSystemWatchers != null)
            {
                UdonSharpSettings settings = UdonSharpSettings.GetSettings();

                bool watcherEnabled = settings == null || settings.autoCompileOnModify;

                if (watcherEnabled != lastEnabledState)
                {
                    lastEnabledState = watcherEnabled;
                    foreach (FileSystemWatcher watcher in fileSystemWatchers)
                    {
                        if (watcher != null)
                            watcher.EnableRaisingEvents = watcherEnabled;
                    }
                }

                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload += CleanupWatchers;

            string[] blacklistedDirectories = UdonSharpSettings.GetScannerBlacklist();

            string[] directories = Directory.GetDirectories("Assets/", "*", SearchOption.AllDirectories).Append("Assets/")
                .Select(e => e.Replace('\\', '/'))
                .Where(e => !blacklistedDirectories.Any(name => name.TrimEnd('/') == e.TrimEnd('/') || e.StartsWith(name)))
                .ToArray();

            List<string> sourceDirectories = new List<string>();

            foreach (string directory in directories)
            {
                if (Directory.GetFiles(directory, "*.cs").Length > 0)
                    sourceDirectories.Add(directory);
            }

            fileSystemWatchers = new FileSystemWatcher[sourceDirectories.Count];
            
            for (int i = 0; i < sourceDirectories.Count; ++i)
            {
                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(sourceDirectories[i], "*.cs");
                fileSystemWatcher.IncludeSubdirectories = false;
                fileSystemWatcher.InternalBufferSize = 1024; // Someone would need to modify 64 files in a single directory at once to hit this

                fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileSystemWatcher.Changed += OnSourceFileChanged;

                fileSystemWatchers[i] = fileSystemWatcher;
            }
        }

        static void CleanupWatchers()
        {
            if (fileSystemWatchers != null)
            {
                foreach (FileSystemWatcher fileSystemWatcher in fileSystemWatchers)
                {
                    if (fileSystemWatcher != null)
                    {
                        fileSystemWatcher.EnableRaisingEvents = false;
                        fileSystemWatcher.Changed -= OnSourceFileChanged;
                        fileSystemWatcher.Dispose();
                    }
                }
            }

            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupWatchers;
        }

        static void HandleScriptModifications()
        {
            UdonSharpSettings settings = UdonSharpSettings.GetSettings();

            if (settings != null)
            {
                if (!settings.autoCompileOnModify)
                {
                    modifiedScripts.Clear();
                    return;
                }

                if (settings.waitForFocus && !UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                    return;
            }

            if (modifiedScripts.Count == 0)
                return;

            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            List<UdonSharpProgramAsset> udonSharpPrograms = new List<UdonSharpProgramAsset>();

            foreach (string dataGuid in udonSharpDataAssets)
            {
                udonSharpPrograms.Add(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid)));
            }

            HashSet<UdonSharpProgramAsset> assetsToUpdate = new HashSet<UdonSharpProgramAsset>();

            foreach (MonoScript script in modifiedScripts)
            {
                foreach (UdonSharpProgramAsset programAsset in udonSharpPrograms)
                {
                    if (programAsset.sourceCsScript == script)
                        assetsToUpdate.Add(programAsset);
                }
            }

            try
            {
                if (assetsToUpdate.Count > 0)
                {
                    if (settings == null || settings.compileAllScripts)
                    {
                        UdonSharpProgramAsset.CompileAllCsPrograms();
                    }
                    else
                    {
                        UdonSharpCompiler compiler = new UdonSharpCompiler(assetsToUpdate.ToArray());
                        compiler.Compile();
                    }
                }
            }
            finally
            {
                modifiedScripts.Clear();
            }

            modifiedScripts.Clear();
        }

        static void OnEditorUpdate()
        {
            SetupWatchers();
            
            lock (modifiedFileLock)
            {
                if (modifiedFilePaths.Count > 0)
                {
                    foreach (string filePath in modifiedFilePaths)
                    {
                        MonoScript asset = AssetDatabase.LoadAssetAtPath<MonoScript>(filePath.Replace(Application.dataPath.Replace("/", "\\"), "Assets"));
                        modifiedScripts.Add(asset);
                    }

                    modifiedFilePaths.Clear();
                }
            }

            HandleScriptModifications();
        }

        static void PlayModeErrorCheck(PlayModeStateChange state)
        {
            // Prevent people from entering play mode when there are compile errors, like normal Unity C#
            // READ ME
            // --------
            // If you think you know better and are about to edit this out, be aware that you gain nothing by doing so. 
            // If a script hits a compile error, it will not update until the compile errors are resolved.
            // You will just be left wondering "why aren't my scripts changing when I edit them?" since the old copy of the script will be used until the compile errors are resolved.
            // --------
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

                bool foundCompileErrors = false;

                foreach (string dataGuid in udonSharpDataAssets)
                {
                    UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid));

                    if (programAsset.sourceCsScript != null && programAsset.compileErrors.Count > 0)
                    {
                        foundCompileErrors = true;
                        break;
                    }
                }

                if (foundCompileErrors)
                {
                    EditorApplication.isPlaying = false;

                    typeof(SceneView).GetMethod("ShowNotification", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { "All U# compile errors have to be fixed before you can enter playmode!" });
                }
            }
        }

        static void OnSourceFileChanged(object source, FileSystemEventArgs args)
        {
            lock (modifiedFileLock) // The watcher runs on a different thread, and I don't feel like using a concurrent list.
            {
                modifiedFilePaths.Add(args.FullPath);
            }
        }
    }

}
