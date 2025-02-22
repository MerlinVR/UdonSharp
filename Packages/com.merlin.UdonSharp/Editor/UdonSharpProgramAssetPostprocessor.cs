
using System;
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

    internal class UdonSharpProgramAssetModificationPreProcessor : UnityEditor.AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) == null)
            {
                return AssetDeleteResult.DidNotDelete;
            }
            
            UdonSharpProgramAsset.ClearProgramAssetCache();

            return AssetDeleteResult.DidNotDelete;
        }
    }
}
