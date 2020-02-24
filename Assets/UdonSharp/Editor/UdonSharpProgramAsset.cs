using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.Editor.ProgramSources.Attributes;
using VRC.Udon.Serialization.OdinSerializer;

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

        [NonSerialized, OdinSerialize]
        public Dictionary<string, FieldDefinition> fieldDefinitions;

        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        private static bool showProgramUasm = false;

        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
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

            DrawPublicVariables(udonBehaviour, ref dirty);

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

        protected override void RefreshProgramImpl()
        {
            if (sourceCsScript != null && !EditorApplication.isCompiling)
                CompileCsProgram();
        }
        
        protected override object GetPublicVariableDefaultValue(string symbol, Type type)
        {
            return program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(symbol));
        }

        public void CompileCsProgram()
        {
            UdonSharpCompiler compiler = new UdonSharpCompiler(this);
            compiler.Compile();
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

        public void ApplyProgram()
        {
            SerializedProgramAsset.StoreProgram(program);
            EditorUtility.SetDirty(this);
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

        private static MonoScript currentUserScript = null;
        private UnityEngine.Object ValidateObjectReference(UnityEngine.Object[] references, System.Type objType, SerializedProperty property, Enum options)
        {
            if (property != null)
                throw new ArgumentException("Serialized property on validate object reference should be null!");

            if (currentUserScript != null)
            {
                foreach (UnityEngine.Object reference in references)
                {
                    GameObject referenceObject = reference as GameObject;
                    UdonBehaviour referenceBehaviour = reference as UdonBehaviour;

                    if (referenceObject != null)
                    {
                        UdonBehaviour[] components = referenceObject.GetComponents<UdonBehaviour>();

                        UdonBehaviour foundComponent = null;

                        foreach (UdonBehaviour component in components)
                        {
                            foundComponent = ValidateObjectReference(new UnityEngine.Object[] { component }, objType, null, UdonSyncMode.NotSynced /* just any enum, we don't care */) as UdonBehaviour;

                            if (foundComponent != null)
                            {
                                return foundComponent;
                            }
                        }
                    }
                    else if (referenceBehaviour != null)
                    {
                        if (referenceBehaviour.programSource != null &&
                            referenceBehaviour.programSource is UdonSharpProgramAsset udonSharpProgram &&
                            udonSharpProgram.sourceCsScript != null)
                        {
                            if (udonSharpProgram.sourceCsScript == currentUserScript)
                                return referenceBehaviour;
                        }
                    }
                }
            }
            else
            {
                // Fallback to default handling if the user has not compiled with the new info
                if (references[0] != null && references[0] is GameObject && typeof(Component).IsAssignableFrom(objType))
                {
                    GameObject gameObject = (GameObject)references[0];
                    references = gameObject.GetComponents(typeof(Component));
                }
                foreach (UnityEngine.Object component in references)
                {
                    if (component != null && objType.IsAssignableFrom(component.GetType()))
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private object DrawUnityObjectField(string fieldName, string symbol, (object value, Type declaredType) publicVariable, ref bool dirty)
        {
            (object value, Type declaredType) = publicVariable;

            FieldDefinition fieldDefinition = null;
            if (fieldDefinitions != null)
                fieldDefinitions.TryGetValue(symbol, out fieldDefinition);

            bool isNormalUnityObject = fieldDefinition == null || fieldDefinition.fieldSymbol.userCsType == null || !fieldDefinition.fieldSymbol.IsUserDefinedBehaviour();

            if (isNormalUnityObject)
                return EditorGUILayout.ObjectField(fieldName, (UnityEngine.Object)value, declaredType, true);

            MethodInfo doObjectFieldMethod = typeof(EditorGUI).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Where(e => e.Name == "DoObjectField" && e.GetParameters().Length == 8).FirstOrDefault();

            if (doObjectFieldMethod == null)
                throw new Exception("Could not find DoObjectField() method");

            Rect objectRect = EditorGUILayout.GetControlRect();
            int id = GUIUtility.GetControlID(typeof(UnityEngine.Object).GetHashCode(), FocusType.Keyboard, objectRect);

            System.Type validatorDelegateType = typeof(EditorGUI).GetNestedType("ObjectFieldValidator", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo validateMethodInfo = typeof(UdonSharpProgramAsset).GetMethod("ValidateObjectReference", BindingFlags.NonPublic | BindingFlags.Instance);

            objectRect = EditorGUI.PrefixLabel(objectRect, new GUIContent(fieldName));

            currentUserScript = fieldDefinition.userBehaviourSource;

            UnityEngine.Object objectFieldValue = (UnityEngine.Object)doObjectFieldMethod.Invoke(null, new object[] {
                objectRect,
                objectRect,
                id,
                (UnityEngine.Object)value,
                fieldDefinition.fieldSymbol.symbolCsType,
                null,
                Delegate.CreateDelegate(validatorDelegateType, this, validateMethodInfo),
                true
            });

            currentUserScript = null;

            return objectFieldValue;
        }

        [NonSerialized]
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

        private object DrawFieldForType(string fieldName, string symbol, (object value, Type declaredType) publicVariable, ref bool dirty, bool enabled)
        {
            bool isArrayElement = fieldName != null;
            FieldDefinition fieldDefinition = null;
            if (fieldDefinitions != null)
                fieldDefinitions.TryGetValue(symbol, out fieldDefinition);

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
                    if (value == null) // We can abuse that the foldout modified the outer scope when it was expanded to make sure this gets set
                    {
                        return Activator.CreateInstance(declaredType, new object[] { 0 });
                    }

                    EditorGUI.indentLevel++;

                    Array valueArray = value as Array;

                    using (EditorGUILayout.VerticalScope verticalScope = new EditorGUILayout.VerticalScope())
                    {
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
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
            {
                return DrawUnityObjectField(fieldName, symbol, publicVariable, ref dirty);
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
        
        protected override object DrawPublicVariableField(string symbol, object variableValue, Type variableType, ref bool dirty, bool enabled)
        {
            object newValue = variableValue;

            EditorGUI.BeginDisabledGroup(!enabled);

            bool shouldDraw = true;
            bool isArray = variableType.IsArray;

            FieldDefinition symbolField;
            if (fieldDefinitions != null && fieldDefinitions.TryGetValue(symbol, out symbolField))
            {
                HideInInspector hideAttribute = symbolField.GetAttribute<HideInInspector>();

                if (hideAttribute != null)
                {
                    shouldDraw = false;
                }
            }
            else
            {
                symbolField = new FieldDefinition(null);
            }

            if (shouldDraw)
            {
                if (!isArray) // Drawing horizontal groups on arrays screws them up, there's probably better handling for this using a manual rect
                    EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                newValue = DrawFieldForType(null, symbol, (variableValue, variableType), ref dirty, enabled);

                if (EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                    variableValue = newValue;
                }
                
                if (symbolField.fieldSymbol != null && symbolField.fieldSymbol.syncMode != UdonSyncMode.NotSynced)
                {
                    if (symbolField.fieldSymbol.syncMode == UdonSyncMode.None)
                        GUILayout.Label("synced", GUILayout.Width(55f));
                    else
                        GUILayout.Label($"sync: {Enum.GetName(typeof(UdonSyncMode), symbolField.fieldSymbol.syncMode)}", GUILayout.Width(85f));
                }

                if (!isArray)
                    EditorGUILayout.EndHorizontal();
            }

            EditorGUI.EndDisabledGroup();

            return variableValue;
        }

        protected override void OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
            base.OnBeforeSerialize();
        }

        protected override void OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
            base.OnAfterDeserialize();
        }
    }
    
    [CustomEditor(typeof(UdonSharpProgramAsset))]
    public class UdonSharpProgramAssetEditor : UdonAssemblyProgramAssetEditor
    {
    }
}