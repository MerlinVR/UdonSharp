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
using VRC.Udon.EditorBindings;
using VRC.Udon.Serialization.OdinSerializer;

[assembly: UdonProgramSourceNewMenu(typeof(UdonSharp.UdonSharpProgramAsset), "Udon C# Program Asset")]

namespace UdonSharp
{
    [CreateAssetMenu(menuName = "VRChat/Udon/Udon C# Program Asset", fileName = "New Udon C# Program Asset")]
    public class UdonSharpProgramAsset : UdonAssemblyProgramAsset
    {
        [SerializeField]
        public MonoScript sourceCsScript;

        [NonSerialized, OdinSerialize]
        public Dictionary<string, FieldDefinition> fieldDefinitions;

        [HideInInspector]
        public string behaviourIDHeapVarName;

        [HideInInspector]
        public List<string> compileErrors = new List<string>();

        [HideInInspector]
        public ClassDebugInfo debugInfo = null;

        [SerializeField]
        private bool hasInteractEvent = false;

        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        private bool showProgramUasm = false;
        private bool showExtraOptions = false;

        private UdonBehaviour currentBehaviour = null;

        private static GUIStyle errorTextStyle;
        private static GUIStyle undoLabelStyle;
        private static GUIContent undoArrowLight;
        private static GUIContent undoArrowDark;
        private static GUIContent undoArrowContent;

        private void DrawCompileErrorTextArea()
        {
            if (compileErrors == null || compileErrors.Count == 0)
                return;

            if (errorTextStyle == null)
            {
                errorTextStyle = new GUIStyle(EditorStyles.textArea);
                errorTextStyle.normal.textColor = new Color32(211, 34, 34, 255);
                errorTextStyle.focused.textColor = errorTextStyle.normal.textColor;
            }

            // todo: convert this to a tree view that just has a list of selectable items that jump to the error
            EditorGUILayout.LabelField($"Compile Error{(compileErrors.Count > 1 ? "s" : "")}", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.Join("\n", compileErrors.Select(e => e.Replace("[<color=#FF00FF>UdonSharp</color>] ", ""))), errorTextStyle);
        }

        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            if (undoLabelStyle == null || 
                undoArrowDark == null || 
                undoArrowLight == null)
            {
                undoLabelStyle = new GUIStyle(EditorStyles.label);
                undoLabelStyle.alignment = TextAnchor.MiddleCenter;
                undoLabelStyle.padding = new RectOffset(0, 0, 1, 0);
                undoLabelStyle.margin = new RectOffset(0, 0, 0, 0);
                undoLabelStyle.border = new RectOffset(0, 0, 0, 0);
                undoLabelStyle.stretchWidth = false;
                undoLabelStyle.stretchHeight = false;

                undoArrowLight = new GUIContent((Texture)EditorGUIUtility.Load("Assets/UdonSharp/Editor/Resources/UndoArrowLight.png"), "Reset to default value");
                undoArrowDark = new GUIContent((Texture)EditorGUIUtility.Load("Assets/UdonSharp/Editor/Resources/UndoArrowBlack.png"), "Reset to default value");
            }
            
            undoArrowContent = EditorGUIUtility.isProSkin ? undoArrowLight : undoArrowDark;

            currentBehaviour = udonBehaviour;

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
            
            object behaviourID = null;
            bool shouldUseRuntimeValue = EditorApplication.isPlaying && currentBehaviour != null;

            // UdonBehaviours won't have valid heap values unless they have been enabled once to run their initialization. 
            // So we check against a value we know will exist to make sure we can use the heap variables.
            if (shouldUseRuntimeValue)
            {
                behaviourID = currentBehaviour.GetProgramVariable(behaviourIDHeapVarName);
                if (behaviourID == null)
                    shouldUseRuntimeValue = false;
            }

            // Just manually break the disabled scope in the UdonBehaviourEditor default drawing for now
            GUI.enabled = GUI.enabled || shouldUseRuntimeValue;
            shouldUseRuntimeValue &= GUI.enabled;

            if (currentBehaviour != null && hasInteractEvent)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Interact", EditorStyles.boldLabel);
                currentBehaviour.interactText = EditorGUILayout.TextField("Interaction Text", currentBehaviour.interactText);
                currentBehaviour.proximity = EditorGUILayout.Slider("Proximity", currentBehaviour.proximity, 0f, 100f);

                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                if (GUILayout.Button("Trigger Interact"))
                    currentBehaviour.SendCustomEvent("_interact");
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space();

            DrawPublicVariables(udonBehaviour, ref dirty);

            if (currentBehaviour != null && !shouldUseRuntimeValue && program != null)
            {
                string[] exportedSymbolNames = program.SymbolTable.GetExportedSymbols();

                foreach (string exportedSymbolName in exportedSymbolNames)
                {
                    bool foundValue = currentBehaviour.publicVariables.TryGetVariableValue(exportedSymbolName, out var variableValue);
                    bool foundType = currentBehaviour.publicVariables.TryGetVariableType(exportedSymbolName, out var variableType);

                    // Remove this variable from the publicVariable list since UdonBehaviours set all null GameObjects, UdonBehaviours, and Transforms to the current behavior's equivalent object regardless of if it's marked as a `null` heap variable or `this`
                    // This default behavior is not the same as Unity, where the references are just left null. And more importantly, it assumes that the user has interacted with the inspector on that object at some point which cannot be guaranteed. 
                    // Specifically, if the user adds some public variable to a class, and multiple objects in the scene reference the program asset, 
                    //   the user will need to go through each of the objects' inspectors to make sure each UdonBehavior has its `publicVariables` variable populated by the inspector
                    if (foundValue && foundType &&
                        variableValue == null &&
                        (variableType == typeof(GameObject) || variableType == typeof(UdonBehaviour) || variableType == typeof(Transform)))
                    {
                        currentBehaviour.publicVariables.RemoveVariable(exportedSymbolName);
                    }
                }
            }

            DrawCompileErrorTextArea();
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
            
            showExtraOptions = EditorGUILayout.Foldout(showExtraOptions, "Utilities");
            if (showExtraOptions)
            {
                if (GUILayout.Button("Export to Assembly Asset"))
                {
                    string savePath = EditorUtility.SaveFilePanelInProject("Assembly asset save location", Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourceCsScript)), "asset", "Choose a save location for the assembly asset");

                    if (savePath.Length > 0)
                    {
                        UdonSharpEditorUtility.UdonSharpProgramToAssemblyProgram(this, savePath);
                    }
                }
            }

            showProgramUasm = EditorGUILayout.Foldout(showProgramUasm, "Compiled C# Assembly");
            if (showProgramUasm)
            {
                DrawAssemblyTextArea(/*!Application.isPlaying*/ false, ref dirty);

                if (program != null)
                    DrawProgramDisassembly();
            }

            currentBehaviour = null;
        }

        protected override void RefreshProgramImpl()
        {
            bool hasAssemblyError = typeof(UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this) != null;

            if (sourceCsScript != null &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                !hasAssemblyError &&
                compileErrors.Count == 0)
            {
                CompileCsProgram();
            }
        }
        
        protected override object GetPublicVariableDefaultValue(string symbol, Type type)
        {
            return program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(symbol));
        }

        public void CompileCsProgram()
        {
            try
            {
                UdonSharpCompiler compiler = new UdonSharpCompiler(this);
                compiler.Compile();
            }
            catch (Exception e)
            {
                compileErrors.Add(e.ToString());
                throw e;
            }
        }

        public static void CompileAllCsPrograms()
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

        public void AssembleCsProgram(uint heapSize)
        {
            // The heap size is determined by the symbol count + the unique extern string count
            UdonSharp.HeapFactory heapFactory = new UdonSharp.HeapFactory(heapSize); 
            UdonEditorInterface assemblerInterface = new UdonEditorInterface(null, heapFactory, null, null, null, null, null, null, null);
            assemblerInterface.AddTypeResolver(new UdonBehaviourTypeResolver());

            FieldInfo assemblyError = typeof(UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                program = assemblerInterface.Assemble(udonAssembly);
                assemblyError.SetValue(this, null);

                hasInteractEvent = false;

                foreach (string entryPoint in program.EntryPoints.GetExportedSymbols())
                {
                    if (entryPoint == "_interact")
                    {
                        hasInteractEvent = true;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                program = null;
                assemblyError.SetValue(this, e.Message);
                Debug.LogException(e);
            }
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

                string chosenFilePath = EditorUtility.SaveFilePanelInProject("Save UdonSharp File", fileName, "cs", "Save UdonSharp file");

                if (chosenFilePath.Length > 0)
                {
                    string chosenFileName = Path.GetFileNameWithoutExtension(chosenFilePath).Replace(" ", "").Replace("#", "Sharp");
                    string fileContents = UdonSharpSettings.GetProgramTemplateString().Replace("<TemplateClassName>", chosenFileName);

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

        private object DrawUnityObjectField(GUIContent fieldName, string symbol, (object value, Type declaredType, FieldDefinition symbolField) publicVariable, ref bool dirty)
        {
            (object value, Type declaredType, FieldDefinition symbolField) = publicVariable;

            FieldDefinition fieldDefinition = symbolField;

            bool isNormalUnityObject = !UdonSharpUtils.IsUserDefinedBehaviour(declaredType) && (fieldDefinition == null || fieldDefinition.fieldSymbol.userCsType == null || !fieldDefinition.fieldSymbol.IsUserDefinedBehaviour());

            if (isNormalUnityObject)
                return EditorGUILayout.ObjectField(fieldName, (UnityEngine.Object)value, declaredType, true);

            MethodInfo doObjectFieldMethod = typeof(EditorGUI).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Where(e => e.Name == "DoObjectField" && e.GetParameters().Length == 8).FirstOrDefault();

            if (doObjectFieldMethod == null)
                throw new Exception("Could not find DoObjectField() method");

            Rect objectRect = EditorGUILayout.GetControlRect();
            Rect originalRect = objectRect;
            int id = GUIUtility.GetControlID(typeof(UnityEngine.Object).GetHashCode(), FocusType.Keyboard, originalRect);

            System.Type validatorDelegateType = typeof(EditorGUI).GetNestedType("ObjectFieldValidator", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo validateMethodInfo = typeof(UdonSharpProgramAsset).GetMethod("ValidateObjectReference", BindingFlags.NonPublic | BindingFlags.Instance);

            objectRect = EditorGUI.PrefixLabel(objectRect, id, new GUIContent(fieldName));

            currentUserScript = fieldDefinition.userBehaviourSource;
            
            UnityEngine.Object objectFieldValue = (UnityEngine.Object)doObjectFieldMethod.Invoke(null, new object[] {
                objectRect,
                objectRect,
                id,
                (UnityEngine.Object)value,
                typeof(UdonBehaviour),
                null,
                Delegate.CreateDelegate(validatorDelegateType, this, validateMethodInfo),
                true
            });

            currentUserScript = null;

            System.Type variableRootType = fieldDefinition.fieldSymbol.userCsType;
            while (variableRootType.IsArray)
                variableRootType = variableRootType.GetElementType();

            string labelText = "";
            if (objectFieldValue != null)
            {
                labelText = $"{objectFieldValue.name} ({ObjectNames.NicifyVariableName(variableRootType.Name)})";
            }
            else
            {
                labelText = $"None ({ObjectNames.NicifyVariableName(variableRootType.Name)})";
            }

            // Manually draw this using the same ID so that we can get some of the style information to bleed over
            objectRect = EditorGUI.PrefixLabel(originalRect, id, new GUIContent(fieldName));
            if (Event.current.type == EventType.Repaint)
                EditorStyles.objectField.Draw(objectRect, new GUIContent(labelText, AssetPreview.GetMiniThumbnail(this)), id);

            return objectFieldValue;
        }

        [NonSerialized]
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

        private object DrawFieldForType(string fieldName, string symbol, (object value, Type declaredType, FieldDefinition symbolField) publicVariable, System.Type currentType, ref bool dirty, bool enabled)
        {
            bool isArrayElement = fieldName != null;

            (object value, Type declaredType, FieldDefinition symbolField) = publicVariable;

            FieldDefinition fieldDefinition = symbolField;
            
            if (fieldName == null)
                fieldName = ObjectNames.NicifyVariableName(symbol);

            GUIContent fieldLabel = null;

            TooltipAttribute tooltip = fieldDefinition == null ? null : fieldDefinition.GetAttribute<TooltipAttribute>();

            if (tooltip != null)
                fieldLabel = new GUIContent(fieldName, tooltip.tooltip);
            else
                fieldLabel = new GUIContent(fieldName);
            
            if (declaredType.IsArray)
            {
                bool foldoutEnabled;

                if (!foldoutStates.TryGetValue(symbol, out foldoutEnabled))
                {
                    foldoutStates.Add(symbol, false);
                }

                foldoutEnabled = EditorGUILayout.Foldout(foldoutEnabled, fieldLabel);
                foldoutStates[symbol] = foldoutEnabled;

                if (foldoutEnabled)
                {
                    Type elementType = currentType.GetElementType();
                    Type arrayDataType = currentType;

                    if (UdonSharpUtils.IsUserJaggedArray(currentType))
                    {
                        arrayDataType = typeof(object[]);
                    }
                    else if (currentType.IsArray && UdonSharpUtils.IsUserDefinedBehaviour(currentType))
                    {
                        arrayDataType = typeof(Component[]);
                    }


                    if (value == null) // We can abuse that the foldout modified the outer scope when it was expanded to make sure this gets set
                    {
                        return Activator.CreateInstance(arrayDataType, new object[] { 0 });
                    }

                    EditorGUI.indentLevel++;

                    Array valueArray = value as Array;

                    using (EditorGUILayout.VerticalScope verticalScope = new EditorGUILayout.VerticalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        int newLength = EditorGUILayout.DelayedIntField("Size", valueArray.Length);
                        if (newLength < 0)
                        {
                            Debug.LogError("Array size must be non-negative.");
                            newLength = valueArray.Length;
                        }

                        // We need to resize the array
                        if (EditorGUI.EndChangeCheck())
                        {
                            Array newArray = Activator.CreateInstance(arrayDataType, new object[] { newLength }) as Array;

                            for (int i = 0; i < newLength && i < valueArray.Length; ++i)
                            {
                                newArray.SetValue(valueArray.GetValue(i), i);
                            }

                            dirty = true;

                            EditorGUI.indentLevel--;
                            return newArray;
                        }

                        for (int i = 0; i < valueArray.Length; ++i)
                        {
                            var elementData = (valueArray.GetValue(i), elementType, fieldDefinition);

                            EditorGUI.BeginChangeCheck();
                            object newArrayVal = DrawFieldForType($"Element {i}", $"{symbol}_element{i}", elementData, currentType.GetElementType(), ref dirty, enabled);

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
                return DrawUnityObjectField(fieldLabel, symbol, (value, declaredType, symbolField), ref dirty);
            }
            else if (declaredType == typeof(string))
            {
                TextAreaAttribute textArea = fieldDefinition == null ? null : fieldDefinition.GetAttribute<TextAreaAttribute>();

                if (textArea != null)
                {
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(fieldLabel);
                    string textAreaText = EditorGUILayout.TextArea((string)value);
                    EditorGUILayout.EndVertical();

                    return textAreaText;
                }
                else
                {
                    return EditorGUILayout.TextField(fieldLabel, (string)value);
                }
            }
            else if (declaredType == typeof(float))
            {
                return EditorGUILayout.FloatField(fieldLabel, (float?)value ?? default);
            }
            else if (declaredType == typeof(double))
            {
                return EditorGUILayout.DoubleField(fieldLabel, (double?)value ?? default);
            }
            else if (declaredType == typeof(int))
            {
                return EditorGUILayout.IntField(fieldLabel, (int?)value ?? default);
            }
            else if (declaredType == typeof(long))
            {
                return EditorGUILayout.LongField(fieldLabel, (long?)value ?? default);
            }
            else if (declaredType == typeof(bool))
            {
                return EditorGUILayout.Toggle(fieldLabel, (bool?)value ?? default);
            }
            else if (declaredType == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(fieldLabel, (Vector2?)value ?? default);
            }
            else if (declaredType == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(fieldLabel, (Vector3?)value ?? default);
            }
            else if (declaredType == typeof(Vector4))
            {
                return EditorGUILayout.Vector4Field(fieldLabel, (Vector4?)value ?? default);
            }
            else if (declaredType == typeof(Color))
            {
                ColorUsageAttribute colorUsage = fieldDefinition == null ? null : fieldDefinition.GetAttribute<ColorUsageAttribute>();

                if (colorUsage != null)
                {
                    return EditorGUILayout.ColorField(fieldLabel, (Color?)value ?? default, false, colorUsage.showAlpha, colorUsage.hdr);
                }
                else
                {
                    return EditorGUILayout.ColorField(fieldLabel, (Color?)value ?? default);
                }
            }
            else if (declaredType == typeof(Color32))
            {
                return (Color32)EditorGUILayout.ColorField(fieldLabel, (Color32?)value ?? default);
            }
            else if (declaredType == typeof(Quaternion))
            {
                Quaternion quatVal = (Quaternion?)value ?? default;
                Vector4 newQuat = EditorGUILayout.Vector4Field(fieldLabel, new Vector4(quatVal.x, quatVal.y, quatVal.z, quatVal.w));
                return new Quaternion(newQuat.x, newQuat.y, newQuat.z, newQuat.w);
            }
            else if (declaredType == typeof(Bounds))
            {
                return EditorGUILayout.BoundsField(fieldLabel, (Bounds?)value ?? default);
            }
            else if (declaredType == typeof(ParticleSystem.MinMaxCurve))
            {
                // This is just matching the standard Udon editor's capability at the moment, I want to eventually switch it to use the proper curve editor, but that will take a chunk of work
                ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve?)value ?? default;

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(fieldLabel);
                EditorGUI.indentLevel++;
                minMaxCurve.curveMultiplier = EditorGUILayout.FloatField("Multiplier", minMaxCurve.curveMultiplier);
                minMaxCurve.curveMin = EditorGUILayout.CurveField("Min Curve", minMaxCurve.curveMin);
                minMaxCurve.curveMax = EditorGUILayout.CurveField("Max Curve", minMaxCurve.curveMax);

                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();

                return minMaxCurve;
            }
            else if (declaredType == typeof(LayerMask)) // Lazy layermask support, todo: make it more like the editor layer mask and also don't do all these LINQ operations and such every draw
            {
                return (LayerMask)EditorGUILayout.MaskField(fieldLabel, (LayerMask?)value ?? default, Enumerable.Range(0, 32).Select(e => LayerMask.LayerToName(e).Length > 0 ? e + ": " + LayerMask.LayerToName(e) : "").ToArray());
            }
            else if (declaredType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(fieldLabel, (Enum)(value ?? Activator.CreateInstance(declaredType)));
            }
            else if (declaredType == typeof(System.Type))
            {
                string typeName = value != null ? ((Type)value).FullName : "null";
                EditorGUILayout.LabelField(fieldLabel, typeName);
            }
            else if (declaredType == typeof(Gradient))
            {
                GradientUsageAttribute gradientUsage = fieldDefinition == null ? null : fieldDefinition.GetAttribute<GradientUsageAttribute>();

                if (value == null)
                {
                    value = new Gradient();
                    GUI.changed = true;
                }

                if (gradientUsage != null)
                {
                    return EditorGUILayout.GradientField(fieldLabel, (Gradient)value, gradientUsage.hdr);
                }
                else
                {
                    return EditorGUILayout.GradientField(fieldLabel, (Gradient)value);
                }
            }
            else if (declaredType == typeof(AnimationCurve))
            {
                return EditorGUILayout.CurveField(fieldLabel, (AnimationCurve)value);
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
            bool shouldUseRuntimeValue = EditorApplication.isPlaying && currentBehaviour != null && GUI.enabled; // GUI.enabled is determined in DrawProgramSourceGUI

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

                foreach (Attribute attribute in symbolField.fieldAttributes)
                {
                    if (attribute == null)
                        continue;

                    if (attribute is HeaderAttribute)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField((attribute as HeaderAttribute).header, EditorStyles.boldLabel);
                    }
                    else if (attribute is SpaceAttribute)
                    {
                        GUILayout.Space((attribute as SpaceAttribute).height);
                    }
                }
            }
            else
            {
                symbolField = new FieldDefinition(null);
            }

            if (shouldDraw)
            {
                if (shouldUseRuntimeValue)
                {
                    variableValue = currentBehaviour.GetProgramVariable(symbol);
                }

                if (!isArray) // Drawing horizontal groups on arrays screws them up, there's probably better handling for this using a manual rect
                    EditorGUILayout.BeginHorizontal();

                FieldDefinition fieldDefinition = null;
                if (fieldDefinitions != null)
                    fieldDefinitions.TryGetValue(symbol, out fieldDefinition);

                EditorGUI.BeginChangeCheck();
                object newValue = DrawFieldForType(null, symbol, (variableValue, variableType, fieldDefinition), fieldDefinition != null ? fieldDefinition.fieldSymbol.userCsType : null, ref dirty, enabled);

                bool changed = EditorGUI.EndChangeCheck();

                if (changed)
                {
                    if (shouldUseRuntimeValue)
                    {
                        currentBehaviour.SetProgramVariable(symbol, newValue);
                    }
                    else
                    {
                        dirty = true;
                        variableValue = newValue;
                    }
                }
                
                if (symbolField.fieldSymbol != null && symbolField.fieldSymbol.syncMode != UdonSyncMode.NotSynced)
                {
                    if (symbolField.fieldSymbol.syncMode == UdonSyncMode.None)
                        GUILayout.Label("synced", GUILayout.Width(55f));
                    else
                        GUILayout.Label($"sync: {Enum.GetName(typeof(UdonSyncMode), symbolField.fieldSymbol.syncMode)}", GUILayout.Width(85f));
                }

                if (!isArray)
                {
                    object originalValue = program.Heap.GetHeapVariable(program.SymbolTable.GetAddressFromSymbol(symbol));

                    if (originalValue != null && !originalValue.Equals(variableValue))
                    {
                        int originalIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        // Check if changed because otherwise the UI throw an error since we changed that we want to draw the undo arrow in the middle of drawing when we're modifying stuff like colors
                        if (!changed && GUI.Button(EditorGUILayout.GetControlRect(GUILayout.Width(14f), GUILayout.Height(11f)), undoArrowContent, undoLabelStyle))
                        {
                            if (shouldUseRuntimeValue)
                            {
                                currentBehaviour.SetProgramVariable(symbol, originalValue);
                            }
                            else
                            {
                                dirty = true;
                                variableValue = originalValue;
                            }
                        }
                        EditorGUI.indentLevel = originalIndent;
                    }

                    EditorGUILayout.EndHorizontal();
                }
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