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
    /// </summary>
    [InitializeOnLoad]
    public class UdonSharpAssetCompileWatcher
    {
        static FileSystemWatcher fileSystemWatcher;
        static readonly object modifiedFileLock = new object();

        static HashSet<string> modifiedFilePaths = new HashSet<string>();
        static HashSet<string> renamedFilePaths = new HashSet<string>();

        static UdonSharpAssetCompileWatcher()
        {
            EditorApplication.update += OnEditorUpdate;

            AssemblyReloadEvents.beforeAssemblyReload += CleanupWatcher;

            fileSystemWatcher = new FileSystemWatcher("Assets/", "*.cs");
            fileSystemWatcher.IncludeSubdirectories = true;

            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fileSystemWatcher.Changed += OnSourceFileChanged;
            fileSystemWatcher.Renamed += OnSourceFileRenamed;
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        static void CleanupWatcher()
        {
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.Dispose();
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
            List<MonoScript> modifiedScripts = new List<MonoScript>();

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

            if (modifiedScripts.Count > 0)
                HandleScriptModifications(modifiedScripts);

            //lock (modifiedFileLock)
            //{
            //    foreach (string renamedFile in renamedFilePaths)
            //    {
            //        Debug.Log(renamedFile);
            //    }

            //    renamedFilePaths.Clear();
            //}
        }

        static void OnSourceFileChanged(object source, FileSystemEventArgs args)
        {
            lock (modifiedFileLock) // The watcher runs on a different thread, and I don't feel like using a concurrent list.
            {
                modifiedFilePaths.Add(args.FullPath);
            }
        }

        static void OnSourceFileRenamed(object source, RenamedEventArgs args)
        {
            //lock (modifiedFileLock)
            //{
            //    renamedFilePaths.Add(args.Name);
            //}
        }
    }

}
