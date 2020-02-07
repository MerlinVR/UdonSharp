using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[assembly: UdonProgramSourceNewMenu(typeof(UdonSharp.UdonSharpProgramAsset), "Udon C# Program Asset")]

namespace UdonSharp
{

    public class UdonSharpProgramAsset : UdonAssemblyProgramAsset
    {
        [SerializeField]
        public MonoScript sourceCsScript;

        private static bool showProgramUasm = false;

        public override void RunProgramSourceEditor(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty)
        {
            EditorGUI.BeginChangeCheck();
            MonoScript newSourceCsScript = (MonoScript)EditorGUILayout.ObjectField("Source Script", sourceCsScript, typeof(MonoScript), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Changed source C# script");
                sourceCsScript = newSourceCsScript;
                dirty = true;
            }

            DrawPublicVariables(publicVariables, ref dirty);

            DrawAssemblyErrorTextArea();

            EditorGUILayout.Space();

            if (GUILayout.Button("Force Compile Script"))
            {
                AssembleCsProgram();
            }

            EditorGUILayout.Space();

            showProgramUasm = EditorGUILayout.Foldout(showProgramUasm, "Compiled C# Assembly");
            //EditorGUI.indentLevel++;
            if (showProgramUasm)
            {
                DrawAssemblyTextArea(/*!Application.isPlaying*/ false, ref dirty);

                if (program != null)
                    DrawProgramDisassembly();
            }
            //EditorGUI.indentLevel--;

            //base.RunProgramSourceEditor(publicVariables, ref dirty);
        }

        protected override void DoRefreshProgramActions()
        {
            AssembleCsProgram();

            Debug.Log("Assembled program!");
        }

        public void AssembleCsProgram()
        {
            Undo.RecordObject(this, "Compile C# program");

            UdonSharpCompiler compiler = new UdonSharpCompiler(sourceCsScript);
            udonAssembly = compiler.Compile(); 

            // After the assembly is compiled, we need to perform linking, for this we iterate all of the UdonAssemblyProgramAssets
            //  and find external functions that this behavior calls, 
            //  then we add calls to the appropriate functions with SendCustomEvent and SetProgramVariable for args

            // Call to base to assemble from the assembly
            AssembleProgram();

            // After the program is assembled, we need to resolve the constant symbols and assign their default values
            compiler.AssignHeapConstants(program);

            EditorUtility.SetDirty(this);
        }
    }
    
    [CustomEditor(typeof(UdonSharpProgramAsset))]
    public class UdonSharpProgramAssetEditor : UdonAssemblyProgramAssetEditor
    { }
}