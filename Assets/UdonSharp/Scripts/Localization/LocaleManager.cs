
//#define UDONSHARP_LOC_DEBUG

using System.Collections;
using System.Collections.Generic;
using UdonSharp.Updater;
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

    }

    /// <summary>
    /// Instance of the currently selected locale, with lookups for appropriate messages and errors, falls back to en-US for missing strings
    /// </summary>
    internal class LocaleInstance
    {
        Dictionary<LocStr, string> localizedStringLookup;

        public LocaleInstance(string locale)
        {
            string udonSharpDir = UdonSharpLocator.GetInstallPath();


        }
    }

    public static class Loc
    {
        public static string Format(LocStr locKey, string format, params object[] parameters)
        {
            throw new System.NotImplementedException();
        }
    }
}
