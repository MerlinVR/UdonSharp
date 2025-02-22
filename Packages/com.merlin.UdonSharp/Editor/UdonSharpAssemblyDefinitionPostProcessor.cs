
using System.Collections.Generic;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;

namespace UdonSharpEditor
{
    internal class UdonSharpAssemblyDefinitionPostProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            List<string> allPaths = new List<string>();
            allPaths.AddRange(importedAssets);
            allPaths.AddRange(deletedAssets);
            
            foreach (string asset in allPaths)
            {
                UdonSharpAssemblyDefinition asmDef = AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(asset);

                if (asmDef != null)
                {
                    CompilationContext.ResetAssemblyCaches();
                }
            }
        }
    }
}