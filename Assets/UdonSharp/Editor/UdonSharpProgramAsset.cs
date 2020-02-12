using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[assembly: UdonProgramSourceNewMenu(typeof(UdonSharp.UdonSharpProgramAsset), "Udon C# Program Asset")]

namespace UdonSharp
{

    public class UdonSharpProgramAsset : UdonAssemblyProgramAsset
    {
        private readonly string programCsTemplate = @"
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[AddComponentMenu("""")]
public class <TemplateClassName> : UdonSharpBehaviour
{
    void Start()
    {
        
    }
}
";

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

            if (sourceCsScript == null)
            {
                DrawCreateScriptButton();
                return;
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

        protected override (object value, Type declaredType) InitializePublicVariable(Type type, string symbol)
        {
            return (program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(symbol)), type);
        }

        public void AssembleCsProgram()
        {
            //Undo.RecordObject(this, "Compile C# program");

            System.Diagnostics.Stopwatch compileTimer = new System.Diagnostics.Stopwatch();
            compileTimer.Start();
            
            UdonSharpCompiler compiler = new UdonSharpCompiler(sourceCsScript);
            int errorCount;
            (udonAssembly, errorCount) = compiler.Compile(); 

            // After the assembly is compiled, we need to perform linking, for this we iterate all of the UdonAssemblyProgramAssets
            //  and find external functions that this behavior calls, 
            //  then we add calls to the appropriate functions with SendCustomEvent and SetProgramVariable for args

            // Call to base to assemble from the assembly
            AssembleProgram();

            // After the program is assembled, we need to resolve the constant symbols and assign their default values
            compiler.AssignHeapConstants(program);
            
            compileTimer.Stop();
            if(errorCount == 0) 
                Debug.Log($"[UdonSharp] Compile of script {Path.GetFileName(AssetDatabase.GetAssetPath(sourceCsScript))} finished in {compileTimer.Elapsed.ToString("mm\\:ss\\.fff")}");

            EditorUtility.SetDirty(this);
        }

        private void DrawCreateScriptButton()
        {
            if (GUILayout.Button("Create Script"))
            {
                string thisPath = AssetDatabase.GetAssetPath(this);
                //string initialPath = Path.GetDirectoryName(thisPath);
                string fileName = Path.GetFileNameWithoutExtension(thisPath).Replace(" Udon C# Program Asset", "").Replace(" ", "").Replace("#", "Sharp");

                string chosenFilePath = EditorUtility.SaveFilePanelInProject("Save UdonScript File", fileName, "cs", "Save UdonScript file");

                string chosenFileName = Path.GetFileNameWithoutExtension(chosenFilePath).Replace(" ", "").Replace("#", "Sharp");

                if (chosenFilePath.Length > 0)
                {
                    string fileContents = programCsTemplate.Replace("<TemplateClassName>", chosenFileName);

                    File.WriteAllText(chosenFilePath, fileContents);

                    AssetDatabase.ImportAsset(chosenFilePath, ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.Refresh();

                    sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(chosenFilePath);
                }
            }
        }
    }
    
    [CustomEditor(typeof(UdonSharpProgramAsset))]
    public class UdonSharpProgramAssetEditor : UdonAssemblyProgramAssetEditor
    {
        //static Texture2D udonSharpIcon;
        
        //public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
        //{
        //    base.RenderStaticPreview(assetPath, subAssets, width, height);

        //    return (Texture2D)EditorGUIUtility.IconContent("ScriptableObject Icon").image;

        //    if (udonSharpIcon == null)
        //        udonSharpIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UdonSharp/Editor/Resources/UdonsharpIcon.png");

        //    if (udonSharpIcon != null)
        //        return udonSharpIcon;

        //    return base.RenderStaticPreview(assetPath, subAssets, width, height);
        //}
    }
}