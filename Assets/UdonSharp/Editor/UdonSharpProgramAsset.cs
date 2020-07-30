using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
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
        public MonoScript sourceCsScript;

        [NonSerialized, OdinSerialize]
        public Dictionary<string, FieldDefinition> fieldDefinitions;

        [HideInInspector]
        public string behaviourIDHeapVarName;

        [HideInInspector]
        public List<string> compileErrors = new List<string>();

        [HideInInspector]
        public ClassDebugInfo debugInfo = null;
        
        [HideInInspector]
        public bool hasInteractEvent = false;

        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        private UdonBehaviour currentBehaviour = null;

        internal void DrawErrorTextAreas()
        {
            UdonSharpGUI.DrawCompileErrorTextArea(this);
            DrawAssemblyErrorTextArea();
        }

        internal void DrawAssemblyText()
        {
            bool dirtyDummy = false;
            DrawAssemblyTextArea(false, ref dirtyDummy);
        }

        new internal void DrawProgramDisassembly()
        {
            base.DrawProgramDisassembly();
        }

        new void DrawPublicVariables(UdonBehaviour behaviour, ref bool dirty)
        {
            UdonSharpGUI.DrawPublicVariables(behaviour, this, ref dirty);
        }

        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            currentBehaviour = udonBehaviour;

            if (!udonBehaviour)
            {
                EditorGUI.BeginChangeCheck();
                MonoScript newSourceCsScript = (MonoScript)EditorGUILayout.ObjectField("Source Script", sourceCsScript, typeof(MonoScript), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Changed source C# script");
                    sourceCsScript = newSourceCsScript;
                    dirty = true;
                }

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Serialized Udon Program Asset", serializedUdonProgramAsset, typeof(AbstractSerializedUdonProgramAsset), false);
                EditorGUI.EndDisabledGroup();

                if (sourceCsScript == null)
                {
                    if (UdonSharpGUI.DrawCreateScriptButton(this))
                    {
                        dirty = true;
                    }
                    return;
                }
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

            DrawErrorTextAreas();
            UdonSharpGUI.DrawUtilities(udonBehaviour, this);

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

        public object GetPublicVariableDefaultValue(string symbol)
        {
            return GetPublicVariableDefaultValue(symbol, null);
        }

        [PublicAPI]
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

        [PublicAPI]
        public static void CompileAllCsPrograms()
        {
            UdonSharpCompiler compiler = new UdonSharpCompiler(GetAllUdonSharpPrograms());
            compiler.Compile();
        }

        [PublicAPI]
        public static UdonSharpProgramAsset[] GetAllUdonSharpPrograms()
        {
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            UdonSharpProgramAsset[] udonSharpPrograms = new UdonSharpProgramAsset[udonSharpDataAssets.Length];

            for (int i = 0; i < udonSharpPrograms.Length; ++i)
            {
                udonSharpPrograms[i] = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(udonSharpDataAssets[i]));
            }

            return udonSharpPrograms;
        }

        static UdonEditorInterface editorInterfaceInstance;
        static UdonSharp.HeapFactory heapFactoryInstance;

        internal void AssembleCsProgram(uint heapSize)
        {
            if (editorInterfaceInstance == null || heapFactoryInstance == null)
            {
                // The heap size is determined by the symbol count + the unique extern string count
                heapFactoryInstance = new UdonSharp.HeapFactory();
                editorInterfaceInstance = new UdonEditorInterface(null, heapFactoryInstance, null, null, null, null, null, null, null);
                editorInterfaceInstance.AddTypeResolver(new UdonBehaviourTypeResolver()); // todo: can be removed with SDK's >= VRCSDK-UDON-2020.06.15.14.08_Public
            }

            heapFactoryInstance.FactoryHeapSize = heapSize;

            FieldInfo assemblyError = typeof(UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                program = editorInterfaceInstance.Assemble(udonAssembly);
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

        public void UpdateProgram()
        {
            if (program == null && SerializedProgramAsset != null)
                program = SerializedProgramAsset.RetrieveProgram();

            if (program == null)
                RefreshProgram();
        }

        // Skips the property since it will create an asset if one doesn't exist and we do not want that.
        public AbstractSerializedUdonProgramAsset GetSerializedUdonProgramAsset()
        {
            return serializedUdonProgramAsset;
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
        // Allow people to drag program assets onto objects in the scene and automatically create a corresponding UdonBehaviour with everything set up
        // https://forum.unity.com/threads/drag-and-drop-scriptable-object-to-scene.546975/#post-4534333
        void OnSceneDrag(SceneView sceneView)
        {
            Event e = Event.current;
            GameObject gameObject = HandleUtility.PickGameObject(e.mousePosition, false);

            if (e.type == EventType.DragUpdated)
            {
                if (gameObject)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                else
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;

                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                e.Use();
                
                if (gameObject)
                {
                    UdonBehaviour component = Undo.AddComponent<UdonBehaviour>(gameObject);
                    component.programSource = target as UdonSharpProgramAsset;
                }
            }
        }
    }
}