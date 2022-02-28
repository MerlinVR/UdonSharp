
using System.Collections.Generic;
using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UdonSharp.Updater
{
    /// <summary>
    /// This scriptable object doesn't store anything, it just acts as an asset that marks the install location of U# similar to how Odin Inspector locates itself
    /// This is included along with the updater to allow it to compile independently from the other UdonSharp scripts which are liable to fail to compile if the user has messed up the installation.
    /// </summary>
    // [CreateAssetMenu(menuName = "U# Locator", fileName = "UdonSharpLocator")]
    public class UdonSharpLocator : ScriptableObject
    {
        private static string _cachedInstallLocation;

        /// <summary>
        /// Gets the install path for the root of UdonSharp, with a standard install this will be "Assets/UdonSharp"
        /// </summary>
        /// <returns></returns>
        public static string InstallPath 
        {
            get
            {
            #if UNITY_EDITOR
                if (_cachedInstallLocation != null)
                    return _cachedInstallLocation;

                string[] foundLocatorGuids = AssetDatabase.FindAssets($"t:{nameof(UdonSharpLocator)}");
                List<UdonSharpLocator> foundLocators = new List<UdonSharpLocator>();

                foreach (string locatorGuid in foundLocatorGuids)
                {
                    UdonSharpLocator locator =
                        AssetDatabase.LoadAssetAtPath<UdonSharpLocator>(AssetDatabase.GUIDToAssetPath(locatorGuid));

                    if (locator)
                        foundLocators.Add(locator);
                }

                if (foundLocators.Count == 0)
                {
                    throw new System.Exception(
                        "Could not find UdonSharp locator, make sure you have installed U# following the install instructions.");
                }

                if (foundLocators.Count > 1)
                {
                    throw new System.Exception(
                        "Multiple UdonSharp locators found, make sure you do not have multiple installations of UdonSharp.");
                }

                _cachedInstallLocation = Path.GetDirectoryName(AssetDatabase.GetAssetPath(foundLocators[0]));
                return _cachedInstallLocation;
            #else
                throw new System.PlatformNotSupportedException("Cannot get UdonSharp installation path outside of the Editor runtime");
            #endif
            }
        }

    /// <summary>
        /// Gets the resources path for U#
        /// </summary>
        /// <returns></returns>
        public static string ResourcesPath => Path.Combine(InstallPath, "Editor", "Resources");

        public static string LocalizationPath => Path.Combine(ResourcesPath, "Localization");

        public static string SettingsPath => Path.Combine(UdonSharpDataLocator.DataPath, "Settings", "UdonSharpSettings.asset");

        public static string SamplesPath => Path.Combine(InstallPath, "Samples~");
        
        public static string IntermediatePrefabPath => Path.Combine(UdonSharpDataLocator.DataPath, "PrefabBuild");
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UdonSharpLocator))]
    internal class UdonSharpLocatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Do not delete or move this file! This is used by UdonSharp to locate its installation directory, if you delete it U# will break!", MessageType.Error);
            EditorGUILayout.HelpBox($"Path: {UdonSharpLocator.InstallPath}\n" +
                $"Resources Path: {UdonSharpLocator.ResourcesPath}\n" +
                $"Localization Path: {UdonSharpLocator.LocalizationPath}\n" +
                $"Settings Path: {UdonSharpLocator.SettingsPath}", MessageType.Info);
        }
    }
#endif
}
