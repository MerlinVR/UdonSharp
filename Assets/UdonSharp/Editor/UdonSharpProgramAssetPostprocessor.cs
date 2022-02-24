
using UdonSharp;
using UnityEditor;

namespace UdonSharpEditor
{
    internal class UdonSharpProgramAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string importedAssetPath in importedAssets)
            {
                UdonSharpProgramAsset importedAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(importedAssetPath);

                if (importedAsset && (importedAsset.CompiledVersion < UdonSharpProgramVersion.CurrentVersion || importedAsset.ScriptVersion < UdonSharpProgramVersion.CurrentVersion))
                {
                    UdonSharpUpgrader.QueueUpgrade(importedAsset);
                    UdonSharpEditorCache.Instance.QueueUpgradePass();
                }

                if (importedAsset)
                {
                    UdonSharpProgramAsset.ClearProgramAssetCache();
                }
            }
        }
    }
}
