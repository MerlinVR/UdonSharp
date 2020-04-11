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
        static HashSet<string> renamedFilePaths = new HashSet<string>();

        static bool lastEnabledState = false;

        static UdonSharpAssetCompileWatcher()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        static void SetupWatchers() 
        {
            if (fileSystemWatchers != null)
                return;

            AssemblyReloadEvents.beforeAssemblyReload += CleanupWatchers;

            string[] directories = Directory.GetDirectories("Assets/", "*", SearchOption.AllDirectories);

            List<string> sourceDirectories = new List<string>();

            foreach (string directory in directories)
            {
                if (Directory.GetFiles(directory, "*.cs").Length > 0)
                    sourceDirectories.Add(directory.Replace('\\', '/'));
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
        }

        static void HandleScriptModifications(List<MonoScript> scripts)
        {
            UdonSharpSettings settings = UdonSharpSettings.GetSettings();

            if (settings != null && !settings.autoCompileOnModify)
                return;

            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            List<UdonSharpProgramAsset> udonSharpPrograms = new List<UdonSharpProgramAsset>();

            foreach (string dataGuid in udonSharpDataAssets)
            {
                udonSharpPrograms.Add(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid)));
            }

            HashSet<UdonSharpProgramAsset> assetsToUpdate = new HashSet<UdonSharpProgramAsset>();

            foreach (MonoScript script in scripts)
            {
                foreach (UdonSharpProgramAsset programAsset in udonSharpPrograms)
                {
                    if (programAsset.sourceCsScript == script)
                        assetsToUpdate.Add(programAsset);
                }
            }

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

        static void OnEditorUpdate()
        {
            SetupWatchers();

            UdonSharpSettings settings = UdonSharpSettings.GetSettings();

            bool watcherEnabled = settings == null || settings.autoCompileOnModify;

            if (watcherEnabled != lastEnabledState && fileSystemWatchers != null)
            {
                lastEnabledState = watcherEnabled;
                foreach (FileSystemWatcher watcher in fileSystemWatchers)
                {
                    if (watcher != null)
                        watcher.EnableRaisingEvents = watcherEnabled;
                }
            }

            List<MonoScript> modifiedScripts = null;

            lock (modifiedFileLock)
            {
                if (modifiedFilePaths.Count > 0)
                {
                    modifiedScripts = new List<MonoScript>();

                    foreach (string filePath in modifiedFilePaths)
                    {
                        MonoScript asset = AssetDatabase.LoadAssetAtPath<MonoScript>(filePath.Replace(Application.dataPath.Replace("/", "\\"), "Assets"));
                        modifiedScripts.Add(asset);
                    }

                    modifiedFilePaths.Clear();
                }
            }

            if (modifiedScripts != null && modifiedScripts.Count > 0)
                HandleScriptModifications(modifiedScripts);
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
