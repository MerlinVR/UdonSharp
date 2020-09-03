
using UdonSharpEditor;
using UnityEditor;

namespace UdonSharp
{
    public class UdonSharpProgramAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool importedUdonSharpAsset = false;

            foreach (string importedAssetPath in importedAssets)
            {
                UdonSharpProgramAsset importedAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(importedAssetPath);

                if (importedAsset != null)
                {
                    importedUdonSharpAsset = true;
                    break;
                }
            }

            UdonSharpProgramAsset.ClearProgramAssetCache();

            if (importedUdonSharpAsset)
                UdonSharpEditorManager.QueueScriptCompile();
        }
    }
}
