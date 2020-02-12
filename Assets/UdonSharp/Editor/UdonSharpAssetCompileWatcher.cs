using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        static UdonSharpAssetCompileWatcher()
        {
            EditorApplication.update += OnEditorUpdate;

            AssemblyReloadEvents.beforeAssemblyReload += CleanupWatcher;

            FileSystemWatcher fileSystemWatcher;

            fileSystemWatcher = new FileSystemWatcher("Assets/", "*.cs");
            fileSystemWatcher.IncludeSubdirectories = true;

            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
            fileSystemWatcher.Changed += OnSourceFileChanged;
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
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            List<UdonSharpProgramAsset> udonSharpPrograms = new List<UdonSharpProgramAsset>();

            foreach (string dataGuid in udonSharpDataAssets)
            {
                //Debug.Log(AssetDatabase.GUIDToAssetPath(dataGuid));
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

            int assetCount = 0;

            foreach (UdonSharpProgramAsset programToUpdate in assetsToUpdate)
            {
                EditorUtility.DisplayProgressBar("UdonSharp Compile", 
                                                $"Compiling {AssetDatabase.GetAssetPath(programToUpdate.sourceCsScript)}...", 
                                                Mathf.Clamp01((assetCount++ / (float)assetsToUpdate.Count) + Random.Range(0.01f, 0.2f))); // Make it look like we're doing work :D

                programToUpdate.AssembleCsProgram();
            }

            EditorUtility.ClearProgressBar();
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
