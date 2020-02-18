using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using VRC.Udon.Common.Interfaces;

[assembly: UdonProgramSourceNewMenu(typeof(UdonSharp.UdonSharpProgramAsset), "Udon C# Program Asset")]

namespace UdonSharp
{
    [CreateAssetMenu(menuName = "VRChat/Udon/Udon C# Program Asset", fileName = "New Udon C# Program Asset")]
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
                CompileCsProgram();
            }

            if (GUILayout.Button("Compile All UdonSharp Programs"))
            {
                CompileAllCsPrograms();
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
            CompileCsProgram();
        }

        protected override (object value, Type declaredType) InitializePublicVariable(Type type, string symbol)
        {
            return (program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(symbol)), type);
        }

        public void CompileCsProgram()
        {
            UdonSharpCompiler compiler = new UdonSharpCompiler(this);
            compiler.Compile();

            EditorUtility.SetDirty(this);
        }

        private void CompileAllCsPrograms()
        {
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            List<UdonSharpProgramAsset> udonSharpPrograms = new List<UdonSharpProgramAsset>();

            foreach (string dataGuid in udonSharpDataAssets)
            {
                udonSharpPrograms.Add(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid)));
            }

            UdonSharpCompiler compiler = new UdonSharpCompiler(udonSharpPrograms.ToArray());
            compiler.Compile();
        }

        public void AssembleCsProgram()
        {
            AssembleProgram();
        }

        public void SetUdonAssembly(string assembly)
        {
            udonAssembly = assembly;
        }
        
        public IUdonProgram GetRealProgram()
        {
            return program;
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

        [NonSerialized]
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

        private object DrawFieldForType(string fieldName, string symbol, (object value, Type declaredType) publicVariable, ref bool dirty, bool enabled)
        {
            if (fieldName == null)
                fieldName = ObjectNames.NicifyVariableName(symbol);

            (object value, Type declaredType) = publicVariable;

            if (declaredType.IsArray)
            {
                bool foldoutEnabled;

                if (!foldoutStates.TryGetValue(symbol, out foldoutEnabled))
                {
                    foldoutStates.Add(symbol, false);
                }

                foldoutEnabled = EditorGUILayout.Foldout(foldoutEnabled, fieldName);
                foldoutStates[symbol] = foldoutEnabled;

                if (foldoutEnabled)
                {
                    EditorGUI.indentLevel++;

                    Array valueArray = value as Array;

                    EditorGUI.BeginChangeCheck();
                    int newLength = EditorGUILayout.IntField("Size", valueArray.Length);

                    // We need to resize the array
                    if (EditorGUI.EndChangeCheck())
                    {
                        Array newArray = Activator.CreateInstance(declaredType, new object[] { newLength }) as Array;

                        for (int i = 0; i < newLength && i < valueArray.Length; ++i)
                        {
                            newArray.SetValue(valueArray.GetValue(i), i);
                        }

                        dirty = true;

                        EditorGUI.indentLevel--;
                        return newArray;
                    }

                    Type elementType = declaredType.GetElementType();

                    for (int i = 0; i < valueArray.Length; ++i)
                    {
                        var elementData = (valueArray.GetValue(i), elementType);

                        EditorGUI.BeginChangeCheck();
                        object newArrayVal = DrawFieldForType($"Element {i}", $"{symbol}_element{i}", elementData, ref dirty, enabled);

                        if (EditorGUI.EndChangeCheck())
                        {
                            valueArray.SetValue(newArrayVal, i);
                            dirty = true;
                        }
                    }

                    EditorGUI.indentLevel--;

                    return valueArray;
                }
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
            {
                return EditorGUILayout.ObjectField(fieldName, (UnityEngine.Object)value, declaredType, true);
            }
            else if (declaredType == typeof(string))
            {
                return EditorGUILayout.TextField(fieldName, (string)value);
            }
            else if (declaredType == typeof(float))
            {
                return EditorGUILayout.FloatField(fieldName, (float?)value ?? default);
            }
            else if (declaredType == typeof(double))
            {
                return EditorGUILayout.DoubleField(fieldName, (double?)value ?? default);
            }
            else if (declaredType == typeof(int))
            {
                return EditorGUILayout.IntField(fieldName, (int?)value ?? default);
            }
            else if (declaredType == typeof(long))
            {
                return EditorGUILayout.LongField(fieldName, (long?)value ?? default);
            }
            else if (declaredType == typeof(bool))
            {
                return EditorGUILayout.Toggle(fieldName, (bool?)value ?? default);
            }
            else if (declaredType == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(fieldName, (Vector2?)value ?? default);
            }
            else if (declaredType == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(fieldName, (Vector3?)value ?? default);
            }
            else if (declaredType == typeof(Vector4))
            {
                return EditorGUILayout.Vector4Field(fieldName, (Vector4?)value ?? default);
            }
            else if (declaredType == typeof(Color))
            {
                return EditorGUILayout.ColorField(fieldName, (Color?)value ?? default);
            }
            else if (declaredType == typeof(Color32))
            {
                return (Color32)EditorGUILayout.ColorField(fieldName, (Color32?)value ?? default);
            }
            else if (declaredType == typeof(Quaternion))
            {
                Quaternion quatVal = (Quaternion?)value ?? default;
                Vector4 newQuat = EditorGUILayout.Vector4Field(fieldName, new Vector4(quatVal.x, quatVal.y, quatVal.z, quatVal.w));
                return new Quaternion(newQuat.x, newQuat.y, newQuat.z, newQuat.w);
            }
            else if (declaredType == typeof(Bounds))
            {
                return EditorGUILayout.BoundsField(fieldName, (Bounds?)value ?? default);
            }
            else if (declaredType == typeof(ParticleSystem.MinMaxCurve))
            {
                // This is just matching the standard Udon editor's capability at the moment, I want to eventually switch it to use the proper curve editor, but that will take a chunk of work
                ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve?)value ?? default;

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(fieldName);
                EditorGUI.indentLevel++;
                minMaxCurve.curveMultiplier = EditorGUILayout.FloatField("Multiplier", minMaxCurve.curveMultiplier);
                minMaxCurve.curveMin = EditorGUILayout.CurveField("Min Curve", minMaxCurve.curveMin);
                minMaxCurve.curveMax = EditorGUILayout.CurveField("Max Curve", minMaxCurve.curveMax);

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();

                return minMaxCurve;
            }
            else if (declaredType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(fieldName, (Enum)(value ?? Activator.CreateInstance(declaredType)));
            }
            else if (declaredType == typeof(System.Type))
            {
                string typeName = value != null ? ((Type)value).FullName : "null";
                EditorGUILayout.LabelField(fieldName, typeName);
            }
            else if (declaredType == typeof(Gradient))
            {
                return EditorGUILayout.GradientField(fieldName, (Gradient)value);
            }
            else if (declaredType == typeof(AnimationCurve))
            {
                return EditorGUILayout.CurveField(fieldName, (AnimationCurve)value);
            }
            else
            {
                EditorGUILayout.LabelField($"{fieldName}: no drawer for type {declaredType}");

                return value;
            }

            return value;
        }

        protected override void DrawFieldForTypeString(string symbol, ref (object value, Type declaredType) publicVariable, ref bool dirty, bool enabled)
        {
            EditorGUI.BeginDisabledGroup(!enabled);

            EditorGUI.BeginChangeCheck();
            object newValue = DrawFieldForType(null, symbol, publicVariable, ref dirty, enabled);

            if (EditorGUI.EndChangeCheck())
            {
                dirty = true;
                publicVariable.value = newValue;
            }

            EditorGUI.EndDisabledGroup();
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