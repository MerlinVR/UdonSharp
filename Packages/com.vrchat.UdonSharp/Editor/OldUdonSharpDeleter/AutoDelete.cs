

using System.IO;
using UnityEngine;

namespace UdonSharpEditor
{
    /// <summary>
    /// Deletes the old UdonSharp files at the first opportunity, this is to prevent conflicts with the new UdonSharp package
    /// </summary>
    internal static class AutoDelete
    {
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (Directory.Exists("Packages/com.vrchat.worlds/Integrations/UdonSharp"))
            {
                // Delete the old UdonSharp files
                Directory.Delete("Packages/com.vrchat.worlds/Integrations/UdonSharp", true);
                
                Debug.Log("[<color=#0c824c>UdonSharp</color>] Found and deleted SDK copy of UdonSharp to prevent conflicts with the new UdonSharp package");
            }
        }
    }
}