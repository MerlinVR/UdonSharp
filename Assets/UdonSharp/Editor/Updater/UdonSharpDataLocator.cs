
using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Codice.Client.BaseCommands.Fileinfo;
using FileInfo = System.IO.FileInfo;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UdonSharp.Updater
{
    internal class UdonSharpDataLocator : ScriptableObject
    {
        private const string DEFAULT_DATA_PATH = "Assets/UdonSharp/UdonSharpDataLocator.asset";
        
        private static string _cachedLocation;

        public static string GetDataPath()
        {
#if UNITY_EDITOR
            if (_cachedLocation != null)
                return _cachedLocation;
            
            string[] foundLocatorGuids = AssetDatabase.FindAssets($"t:{nameof(UdonSharpDataLocator)}");
            List<UdonSharpDataLocator> foundLocators = new List<UdonSharpDataLocator>();

            foreach (string locatorGuid in foundLocatorGuids)
            {
                UdonSharpDataLocator locator = AssetDatabase.LoadAssetAtPath<UdonSharpDataLocator>(AssetDatabase.GUIDToAssetPath(locatorGuid));

                if (locator)
                    foundLocators.Add(locator);
            }
            
            if (foundLocators.Count > 1)
                throw new System.Exception("Multiple UdonSharp data locators found, make sure you do not have multiple installations of UdonSharp and have not duplicated any UdonSharp directories");

            if (foundLocators.Count == 0)
                foundLocators.Add(InitializeUdonSharpData());
            
            _cachedLocation = Path.GetDirectoryName(AssetDatabase.GetAssetPath(foundLocators[0]));
            return _cachedLocation;
#else
            throw new System.PlatformNotSupportedException("Cannot get UdonSharp data path outside of the Editor runtime");
#endif
        }

        private static string GetUtilitiesPath(UdonSharpDataLocator locator)
        {
            string locatorPath = AssetDatabase.GetAssetPath(locator);

            return Path.Combine(Path.GetDirectoryName(locatorPath), "UtilityScripts");
        }

        private static UdonSharpDataLocator InitializeUdonSharpData()
        {
            if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(DEFAULT_DATA_PATH)))
                AssetDatabase.CreateFolder("Assets", "UdonSharp");
            
            var locator = CreateInstance<UdonSharpDataLocator>();
            AssetDatabase.CreateAsset(locator, DEFAULT_DATA_PATH);

            string utilsTargetPath = GetUtilitiesPath(locator);

            string utilsSourcePath = Path.Combine(UdonSharpLocator.GetSamplesPath(), "Utilities");
            
            DeepCopyDirectory(utilsSourcePath, utilsTargetPath);
            
            Debug.Log("Created UdonSharp data directory", locator);

            AssetDatabase.Refresh();

            return locator;
        }

        private static void DeepCopyDirectory(string sourcePath, string destinationPath)
        {
            string[] sourceDirs = Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories);

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(sourcePath, destinationPath));
            }

            string[] sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);

            foreach (string sourceFile in sourceFiles)
            {
                string targetFilePath = sourceFile.Replace(sourcePath, destinationPath);
                File.Copy(sourceFile, targetFilePath);
                new FileInfo(targetFilePath).IsReadOnly = false;
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UdonSharpDataLocator))]
    internal class UdonSharpDataLocatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Do not delete this file! This is used by UdonSharp to locate its data directory.", MessageType.Error);
        }
    }

    internal class InitUSharpDataOnImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length <= 0) return;
            
            try
            {
                UdonSharpDataLocator.GetDataPath(); // Implicitly initializes the data asset if it doesn't exist
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
#endif
}
