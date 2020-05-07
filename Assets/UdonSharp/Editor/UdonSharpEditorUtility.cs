
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;

namespace UdonSharp
{
    public static class UdonSharpEditorUtility
    {
        /// <summary>
        /// Creates a new UdonAssemblyProgramAsset from an UdonSharpProgramAsset for the sake of portability. Most info used for the inspector gets stripped so this isn't a great solution for remotely complex assets.
        /// </summary>
        /// <param name="udonSharpProgramAsset">The source program asset</param>
        /// <param name="savePath">The save path for the asset file. Save path is only needed here because Udon needs a GUID for saving the serialized program asset and it'd be a pain to break that requirement at the moment</param>
        /// <returns>The exported UdonAssemblyProgramAsset</returns>
        public static UdonAssemblyProgramAsset UdonSharpProgramToAssemblyProgram(UdonSharpProgramAsset udonSharpProgramAsset, string savePath)
        {
            if (EditorApplication.isPlaying)
                throw new System.NotSupportedException("UdonSharpEditorUtility.UdonSharpProgramToAssemblyProgram() cannot be called in play mode");

            UdonAssemblyProgramAsset newProgramAsset = ScriptableObject.CreateInstance<UdonAssemblyProgramAsset>();
            AssetDatabase.CreateAsset(newProgramAsset, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            newProgramAsset = AssetDatabase.LoadAssetAtPath<UdonAssemblyProgramAsset>(savePath);

            FieldInfo assemblyField = typeof(UdonAssemblyProgramAsset).GetField("udonAssembly", BindingFlags.NonPublic | BindingFlags.Instance);
            udonSharpProgramAsset.CompileCsProgram();

            assemblyField.SetValue(newProgramAsset, assemblyField.GetValue(udonSharpProgramAsset));

            MethodInfo assembleMethod = typeof(UdonAssemblyProgramAsset).GetMethod("AssembleProgram", BindingFlags.NonPublic | BindingFlags.Instance);
            assembleMethod.Invoke(newProgramAsset, new object[] { });

            IUdonProgram uSharpProgram = udonSharpProgramAsset.GetRealProgram();
            FieldInfo assemblyProgramGetter = typeof(UdonProgramAsset).GetField("program", BindingFlags.NonPublic | BindingFlags.Instance);
            IUdonProgram assemblyProgram = (IUdonProgram)assemblyProgramGetter.GetValue(newProgramAsset);

            if (uSharpProgram == null || assemblyProgram == null)
                return null;

            string[] symbols = uSharpProgram.SymbolTable.GetSymbols();

            foreach (string symbol in symbols)
            {
                uint symbolAddress = uSharpProgram.SymbolTable.GetAddressFromSymbol(symbol);
                System.Type symbolType = uSharpProgram.Heap.GetHeapVariableType(symbolAddress);
                object symbolValue = uSharpProgram.Heap.GetHeapVariable(symbolAddress);
                
                assemblyProgram.Heap.SetHeapVariable(assemblyProgram.SymbolTable.GetAddressFromSymbol(symbol), symbolValue, symbolType);
            }

            EditorUtility.SetDirty(newProgramAsset);

            newProgramAsset.SerializedProgramAsset.StoreProgram(assemblyProgram);
            EditorUtility.SetDirty(newProgramAsset.SerializedProgramAsset);

            AssetDatabase.SaveAssets();

            // This doesn't work unfortunately due to how Udon tries to locate the serialized asset when importing an assembly
            //string serializedAssetPath = $"{Path.GetDirectoryName(savePath)}/{Path.GetFileNameWithoutExtension(savePath)}_serialized.asset";
            
            //AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(newProgramAsset.SerializedProgramAsset), serializedAssetPath);
            //AssetDatabase.SaveAssets();

            return newProgramAsset;
        }
    }
}
