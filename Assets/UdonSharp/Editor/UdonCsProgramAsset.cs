using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[assembly: UdonProgramSourceNewMenu(typeof(UdonSharp.UdonCsProgramAsset), "Udon C# Program Asset")]

namespace UdonSharp
{

    public class UdonCsProgramAsset : UdonAssemblyProgramAsset
    {
        [SerializeField]
        protected MonoScript sourceCsScript;

        public override void RunProgramSourceEditor(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty)
        {
            sourceCsScript = (MonoScript)EditorGUILayout.ObjectField("Source Script", sourceCsScript, typeof(MonoScript), false);

            base.RunProgramSourceEditor(publicVariables, ref dirty);

            if (GUILayout.Button("Compile Script"))
            {
                AssembleCsProgram();
            }
        }

        protected override void DoRefreshProgramActions()
        {
            // Don't do this automatically yet
            //AssembleCsProgram();
        }

        protected void AssembleCsProgram()
        {
            UdonSharpCompiler compiler = new UdonSharpCompiler(sourceCsScript);
            udonAssembly = compiler.Compile();

            // After the assembly is compiled, we need to perform linking, for this we iterate all of the UdonAssemblyProgramAssets
            //  and find external functions that this behavior calls, 
            //  then we add calls to the appropriate functions with SendCustomEvent and SetProgramVariable for args

            // Call to base to assemble from the assembly
            AssembleProgram();

            // After the program is assembled, we need to resolve the constant symbols and assign their default values
            compiler.AssignHeapConstants(program);

        }
    }
    
    [CustomEditor(typeof(UdonCsProgramAsset))]
    public class UdonCsProgramAssetEditor : UdonAssemblyProgramAssetEditor
    { }
}