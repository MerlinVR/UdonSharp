using System.Collections.Generic;
using System.IO;
using System.Linq;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;

namespace UdonSharpEditor
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
    internal class UdonSharpAssetCompileWatcher
    {
        private static FileSystemWatcher[] _fileSystemWatchers;
        private static readonly object _modifiedFileLock = new object();

        private static HashSet<string> _modifiedFilePaths = new HashSet<string>();
        private static HashSet<MonoScript> _modifiedScripts = new HashSet<MonoScript>();

        private static bool _lastEnabledState;

        static UdonSharpAssetCompileWatcher()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void SetupWatchers() 
        {
            if (_fileSystemWatchers != null)
            {
                UdonSharpSettings settings = UdonSharpSettings.GetSettings();

                bool watcherEnabled = settings.autoCompileOnModify;

                if (watcherEnabled == _lastEnabledState) 
                    return;
                
                _lastEnabledState = watcherEnabled;
                foreach (FileSystemWatcher watcher in _fileSystemWatchers)
                {
                    if (watcher != null)
                        watcher.EnableRaisingEvents = watcherEnabled;
                }

                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload += CleanupWatchers;

            // string[] blacklistedDirectories = UdonSharpSettings.GetScannerBlacklist();
            //
            // string[] directories = Directory.GetDirectories("Assets/", "*", SearchOption.AllDirectories).Append("Assets/")
            //     .Select(e => e.Replace('\\', '/'))
            //     .Where(e => !blacklistedDirectories.Any(name => name.TrimEnd('/') == e.TrimEnd('/') || e.StartsWith(name)))
            //     .ToArray();
            //
            // List<string> sourceDirectories = new List<string>();
            //
            // foreach (string directory in directories)
            // {
            //     if (Directory.GetFiles(directory, "*.cs").Length > 0)
            //         sourceDirectories.Add(directory);
            // }

            IEnumerable<string> sourcePaths = CompilationContext.GetAllFilteredSourcePaths(true);

            HashSet<string> sourceDirectoriesSet = new HashSet<string>();

            foreach (string sourcePath in sourcePaths)
            {
                sourceDirectoriesSet.Add(Path.GetDirectoryName(sourcePath));
            }

            string[] sourceDirectories = sourceDirectoriesSet.ToArray();

            _fileSystemWatchers = new FileSystemWatcher[sourceDirectories.Length];
            
            for (int i = 0; i < sourceDirectories.Length; ++i)
            {
                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(sourceDirectories[i], "*.cs");
                fileSystemWatcher.IncludeSubdirectories = false;
                fileSystemWatcher.InternalBufferSize = 512; // Someone would need to modify 32 files in a single directory at once to hit this

                fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileSystemWatcher.Changed += OnSourceFileChanged;

                _fileSystemWatchers[i] = fileSystemWatcher;
            }
        }

        private static void CleanupWatchers()
        {
            if (_fileSystemWatchers != null)
            {
                foreach (FileSystemWatcher fileSystemWatcher in _fileSystemWatchers)
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

        private static void HandleScriptModifications()
        {
            UdonSharpSettings settings = UdonSharpSettings.GetSettings();

            if (!settings.autoCompileOnModify)
            {
                _modifiedScripts.Clear();
                return;
            }

            if (settings.waitForFocus && !UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (_modifiedScripts.Count == 0)
                return;

            try
            {
                UdonSharpProgramAsset.CompileAllCsPrograms();
            }
            finally
            {
                _modifiedScripts.Clear();
            }
        }

        private static void OnEditorUpdate()
        {
            SetupWatchers();
            
            lock (_modifiedFileLock)
            {
                if (_modifiedFilePaths.Count > 0)
                {
                    foreach (string filePath in _modifiedFilePaths)
                    {
                        string path = filePath.Substring(Application.dataPath.Length - "Assets".Length);
                        MonoScript asset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                        
                        if (asset)
                            _modifiedScripts.Add(asset);
                    }

                    _modifiedFilePaths.Clear();
                }
            }

            HandleScriptModifications();
        }

        private static void OnSourceFileChanged(object source, FileSystemEventArgs args)
        {
            lock (_modifiedFileLock) // The watcher runs on a different thread, and I don't feel like using a concurrent list.
            {
                // There's some platform args.FullPath may be a relative path.
                _modifiedFilePaths.Add(Path.GetFullPath(args.FullPath));
            }
        }
    }

}
