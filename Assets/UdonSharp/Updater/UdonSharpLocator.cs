
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
    //[CreateAssetMenu(menuName = "U# Locator", fileName = "UdonSharpLocator")]
    public class UdonSharpLocator : ScriptableObject
    {
        private static string cachedLocation = null;

        /// <summary>
        /// Gets the install path for the root of UdonSharp, with a standard install this will be "Assets/UdonSharp"
        /// </summary>
        /// <returns></returns>
        public static string GetInstallPath()
        {
#if UNITY_EDITOR
            if (cachedLocation != null)
                return cachedLocation;
            
            string[] foundLocatorGuids = AssetDatabase.FindAssets($"t:{typeof(UdonSharpLocator).Name}");
            List<UdonSharpLocator> foundLocators = new List<UdonSharpLocator>();

            foreach (string locatorGuid in foundLocatorGuids)
            {
                UdonSharpLocator locator = AssetDatabase.LoadAssetAtPath<UdonSharpLocator>(AssetDatabase.GUIDToAssetPath(locatorGuid));

                if (locator)
                    foundLocators.Add(locator);
            }

            if (foundLocators.Count == 0)
            {
                throw new System.Exception("Could not find UdonSharp locator, make sure you have installed U# following the install instructions.");
            }
            else if (foundLocators.Count > 1)
            {
                throw new System.Exception("Multiple UdonSharp locators found, make sure you do not have multiple installations of UdonSharp.");
            }
            else
            {
                cachedLocation = Path.GetDirectoryName(AssetDatabase.GetAssetPath(foundLocators[0]));

                return cachedLocation;
            }
#else
            throw new System.PlatformNotSupportedException("Cannot get UdonSharp installation path outside of the Editor runtime");
#endif
        }

        /// <summary>
        /// Gets the resources path for U#
        /// </summary>
        /// <returns></returns>
        public static string GetResourcesPath()
        {
            return Path.Combine(GetInstallPath(), "Editor", "Resources");
        }

        public static string GetLocalizationPath()
        {
            return Path.Combine(GetResourcesPath(), "Localization");
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UdonSharpLocator))]
    public class UdonSharpLocatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Do not delete or move this file! This is used by UdonSharp to locate its installation directory, if you delete it U# will break!", MessageType.Error);
            EditorGUILayout.HelpBox($"Path: {UdonSharpLocator.GetInstallPath()}\n" +
                $"Resources Path: {UdonSharpLocator.GetResourcesPath()}\n" +
                $"Localization Path: {UdonSharpLocator.GetLocalizationPath()}", MessageType.Info);
        }
    }
#endif
}
