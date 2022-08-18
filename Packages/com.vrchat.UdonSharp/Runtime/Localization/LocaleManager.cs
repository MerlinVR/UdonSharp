
#define UDONSHARP_LOC_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UdonSharp.Updater;
using UnityEditor;
using UnityEngine;

namespace UdonSharp.Localization
{
    /// <summary>
    /// Locale string ID list used to get a specified localized string
    /// 
    /// Used for UI, compiler messages, warnings, and errors
    /// The int ID of a given LocStr is not reliable and is liable to change so do not depend on the integer value of it.
    /// 
    /// Enums are grouped and prefixed by the type of string they represent:
    /// `UI_`: UI related strings for UI elements, for instance the Compile All UdonSharp Programs button
    /// `CI_`: Compiler info messages, ex. Compile of X scripts finished in Y
    /// `CW_`: Compiler warning messages, ex. Multiple UdonSharpProgramAssets referencing the same script
    /// `CE_`: Compiler error messages, ex. Most compile errors
    /// </summary>
    public enum LocStr
    {
        UI_TestLocStr,
        UI_CompileProgram,
        UI_CompileAllPrograms,
        UI_SendCustomEvent,
        UI_TriggerInteract,
        UI_ProgramSource,
        UI_ProgramScript,
        UI_SourceScript,
        UI_SerializedUdonProgramAsset,
        CE_UdonSharpBehaviourConstructorsNotSupported,
        CE_PartialMethodsNotSupported,
        CE_UdonSharpBehaviourGenericMethodsNotSupported,
        CE_LocalMethodsNotSupported,
        CE_NodeNotSupported,
        CE_UdonMethodNotExposed,
        CE_InitializerListsNotSupported,

        Length,
    }

    /// <summary>
    /// Instance of the currently selected locale, with lookups for appropriate messages and errors, falls back to en-US for missing strings
    /// </summary>
    internal class LocaleInstance
    {
        Dictionary<LocStr, string> localizedStringLookup = new Dictionary<LocStr, string>();

        public LocaleInstance(string locale)
        {
            string localeDir = UdonSharpLocator.LocalizationPath;
            string fileContents = "";

            try
            {
                fileContents = LoadLocale(Path.Combine(localeDir, locale.ToLowerInvariant() + ".resx"));
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load locale {locale}\nException: {e}");
            }

            XName nameAttributeID = XName.Get("name");
            XName valueAttributeID = XName.Get("value");

            using (StringReader strReader = new StringReader(fileContents))
            {
                XElement xml = XElement.Load(strReader, LoadOptions.PreserveWhitespace);

                foreach (var element in xml.Elements())
                {
                    if (element.Name != "data")
                        continue;

                    string elementName = element.Attribute(nameAttributeID).Value;
                    string elementValue = element.Element(valueAttributeID).Value;

                    if (Enum.TryParse(elementName, out LocStr elementResult))
                    {
                        localizedStringLookup.Add(elementResult, elementValue);
                    }
#if UDONSHARP_LOC_DEBUG
                    else
                    {
                        Debug.LogWarning($"Could not find corresponding enum for key '{elementName}'");
                    }
#endif
                }
            }

#if UDONSHARP_LOC_DEBUG
            for (int i = 0; i < (int)LocStr.Length; ++i)
            {
                if (!localizedStringLookup.ContainsKey((LocStr)i))
                    Debug.LogWarning($"Did not find string for key '{(LocStr)i}'");
            }
#endif
        }

        static string LoadLocale(string path)
        {
            if (!File.Exists(path))
                throw new System.IO.FileNotFoundException($"Could not find locale file at {path}, make sure you have installed UdonSharp following the installation instructions.");

            return ReadFileTextSync(path, 1f);
        }

        // Stolen from UdonSharpUtils because it's in a different assembly, todo: move it somewhere more accessible
        static string ReadFileTextSync(string filePath, float timeoutSeconds)
        {
            bool sourceLoaded = false;

            string fileText = "";

            System.DateTime startTime = System.DateTime.Now;

            while (true)
            {
                System.IO.IOException exception = null;

                try
                {
                    fileText = System.IO.File.ReadAllText(filePath);
                    sourceLoaded = true;
                }
                catch (System.IO.IOException e)
                {
                    exception = e;

                    if (e is System.IO.FileNotFoundException ||
                        e is System.IO.DirectoryNotFoundException)
                        throw e;
                }

                if (sourceLoaded)
                    break;
                else
                    System.Threading.Thread.Sleep(20);

                System.TimeSpan timeFromStart = System.DateTime.Now - startTime;

                if (timeFromStart.TotalSeconds > timeoutSeconds)
                {
                    UnityEngine.Debug.LogError($"Timeout when attempting to read file {filePath}");
                    if (exception != null)
                        throw exception;
                }
            }

            return fileText;
        }

        public string GetString(LocStr locStr)
        {
            if (localizedStringLookup.TryGetValue(locStr, out string foundStr))
            {
                return foundStr;
            }

            return "";
        }
    }

    /// <summary>
    /// Localization manager
    /// </summary>
    public static class Loc
    {
        private static LocaleInstance _instance;

        public static void InitLocalization()
        {
            if (_instance == null)
                ReloadLocalization();
        }

        internal static void ReloadLocalization()
        {
            _instance = new LocaleInstance("en-us");
        }

        public static string Get(LocStr locKey)
        {
            InitLocalization();
            return _instance.GetString(locKey);
        }

        public static string Format(LocStr locKey, params object[] parameters)
        {
            InitLocalization();
            return string.Format(_instance.GetString(locKey), parameters);
        }
    }

#if UDONSHARP_LOC_DEBUG && UNITY_EDITOR
    internal class LocalePostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (Path.GetExtension(str) == ".resx")
                {
                    Loc.ReloadLocalization();
                    break;
                }
            }
        }
    }
#endif
}
