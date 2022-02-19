
using UdonSharp;
using UnityEditor;

namespace UdonSharpEditor
{
    internal class UdonSharpProgramAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // bool importedUdonSharpAsset = false;
            
            foreach (string importedAssetPath in importedAssets)
            {
                UdonSharpProgramAsset importedAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(importedAssetPath);

                if (importedAsset != null)
                {
                    // importedUdonSharpAsset = true;
                    UdonSharpUpgrader.QueueUpgrade(importedAsset);
                    
                    UdonSharpEditorCache.Instance.QueueUpgradePass();
                }
            }

            UdonSharpProgramAsset.ClearProgramAssetCache();

            // if (importedUdonSharpAsset)
            //     UdonSharpEditorManager.QueueScriptCompile();
        }
    }
}
