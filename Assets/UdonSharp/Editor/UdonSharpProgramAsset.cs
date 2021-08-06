
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
using VRC.Udon.ProgramSources;
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
        public BehaviourSyncMode behaviourSyncMode = BehaviourSyncMode.Any;

        [HideInInspector]
        public string behaviourIDHeapVarName;

        [HideInInspector]
        public List<string> compileErrors = new List<string>();

        [HideInInspector]
        public bool hasInteractEvent = false;

        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        private UdonBehaviour currentBehaviour = null;

        internal bool showUtilityDropdown = false;

        internal void DrawErrorTextAreas()
        {
            UdonSharpGUI.DrawCompileErrorTextArea(this);
            DrawAssemblyErrorTextArea();
        }

        internal void DrawAssemblyText()
        {
            string uasmText = UdonSharpEditorCache.Instance.GetUASMStr(this);

            EditorGUILayout.LabelField("Assembly Code", EditorStyles.boldLabel);
            if (GUILayout.Button("Copy Assembly To Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = uasmText;
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(uasmText);
            EditorGUI.EndDisabledGroup();
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
                    bool shouldReplace = true;

                    if (sourceCsScript != null)
                        shouldReplace = EditorUtility.DisplayDialog("Modifying script on program asset", "If you modify a script on a program asset while it is being used by objects in a scene it can cause issues. Are you sure you want to change the source script?", "Ok", "Cancel");

                    if (shouldReplace)
                    {
                        Undo.RecordObject(this, "Changed source C# script");
                        sourceCsScript = newSourceCsScript;
                        dirty = true;
                    }
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
                ImmutableArray<string> exportedSymbolNames = program.SymbolTable.GetExportedSymbols();

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
            if (sourceCsScript != null &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                !UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
            {
                CompileAllCsPrograms(true);
            }
        }
        
        protected override object GetPublicVariableDefaultValue(string symbol, Type type)
        {
            if (program == null && SerializedProgramAsset != null)
                program = SerializedProgramAsset.RetrieveProgram();

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

        /// <summary>
        /// Compiles all U# programs in the project. If forceCompile is true, will skip checking for file changes to skip compile tasks
        /// </summary>
        /// <param name="forceCompile"></param>
        [PublicAPI]
        public static void CompileAllCsPrograms(bool forceCompile = false, bool editorBuild = true)
        {
            UdonSharpProgramAsset[] programs = GetAllUdonSharpPrograms();

            if (!forceCompile)
            {
                UdonSharpEditorCache cache = UdonSharpEditorCache.Instance;
                bool needsCompile = false;
                foreach (UdonSharpProgramAsset programAsset in programs)
                {
                    if (cache.IsSourceFileDirty(programAsset))
                    {
                        needsCompile = true;
                        break;
                    }
                }

                if (!needsCompile)
                    return;
            }

            UdonSharpCompiler compiler = new UdonSharpCompiler(programs, editorBuild);
            compiler.Compile();
        }

        static UdonSharpProgramAsset[] _programAssetCache;
        internal static void ClearProgramAssetCache()
        {
            _programAssetCache = null;
            UdonSharpEditorUtility._programAssetLookup = null;
            UdonSharpEditorUtility._programAssetTypeLookup = null;
        }

        [PublicAPI]
        public static UdonSharpProgramAsset[] GetAllUdonSharpPrograms()
        {
            if (_programAssetCache == null)
            {
                string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{nameof(UdonSharpProgramAsset)}");

                _programAssetCache = new UdonSharpProgramAsset[udonSharpDataAssets.Length];

                for (int i = 0; i < _programAssetCache.Length; ++i)
                    _programAssetCache[i] = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(udonSharpDataAssets[i]));

                bool neededFallback = false;
                var fallbackAssets1 = Resources.FindObjectsOfTypeAll<UdonProgramAsset>().OfType<UdonSharpProgramAsset>();

                foreach (UdonSharpProgramAsset fallbackAsset in fallbackAssets1)
                {
                    if (!_programAssetCache.Contains(fallbackAsset))
                    {
                        Debug.LogWarning($"Repairing program asset {fallbackAsset} which Unity has broken");
                        neededFallback = true;
                        
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(fallbackAsset), ImportAssetOptions.ForceUpdate);
                    }
                }

                if (!neededFallback)
                {
                    var fallbackAssets2 = AssetDatabase.FindAssets($"t:{nameof(UdonProgramAsset)}").Select(e => AssetDatabase.LoadAssetAtPath<UdonProgramAsset>(AssetDatabase.GUIDToAssetPath(e))).OfType<UdonSharpProgramAsset>();
                    foreach (UdonSharpProgramAsset fallbackAsset in fallbackAssets2)
                    {
                        if (!_programAssetCache.Contains(fallbackAsset))
                        {
                            Debug.LogWarning($"Repairing program asset {fallbackAsset} which Unity has broken pass 2");
                            neededFallback = true;

                            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(fallbackAsset),
                                ImportAssetOptions.ForceUpdate);
                        }
                    }
                }

                if (neededFallback)
                {
                    udonSharpDataAssets = AssetDatabase.FindAssets($"t:{nameof(UdonSharpProgramAsset)}");

                    _programAssetCache = new UdonSharpProgramAsset[udonSharpDataAssets.Length];

                    for (int i = 0; i < _programAssetCache.Length; ++i)
                    {
                        _programAssetCache[i] =
                            AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(
                                AssetDatabase.GUIDToAssetPath(udonSharpDataAssets[i]));
                    }
                }
            }

            return (UdonSharpProgramAsset[])_programAssetCache.Clone();
        }

        [PublicAPI]
        public static bool AnyUdonSharpScriptHasError()
        {
            FieldInfo assemblyErrorField = typeof(UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (UdonSharpProgramAsset programAsset in GetAllUdonSharpPrograms())
            {
                if (programAsset.sourceCsScript == null)
                    continue;

                if (programAsset.compileErrors.Count > 0)
                    return true;

                if (!string.IsNullOrEmpty((string)assemblyErrorField.GetValue(programAsset)))
                    return true;
            }

            return false;
        }

        [PublicAPI]
        public static UdonSharpProgramAsset GetProgramAssetForClass(System.Type classType)
        {
            if (classType == null)
                throw new System.ArgumentNullException();
            
            return UdonSharpEditorUtility.GetUdonSharpProgramAsset(classType); 
        }
        
        [PublicAPI]
        public static System.Type GetBehaviourClass(UdonBehaviour behaviour)
        {
            if (behaviour == null)
                throw new NullReferenceException();

            if (behaviour.programSource is UdonSharpProgramAsset programAsset)
            {
                return programAsset.GetClass();
            }

            return null;
        }

        [PublicAPI]
        public System.Type GetClass()
        {
            // Needs to be an explicit null check because of Unity's object equality operator overloads
            if (sourceCsScript != null)
                return sourceCsScript.GetClass();

            return null;
        }

        static UdonEditorInterface editorInterfaceInstance;
        static UdonSharp.HeapFactory heapFactoryInstance;

        internal bool AssembleCsProgram(uint heapSize)
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

                return false;
            }

            return true;
        }

        internal AbstractSerializedUdonProgramAsset GetSerializedProgramAssetWithoutRefresh()
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out string guid, out long _);

            if (serializedUdonProgramAsset != null)
            {
                if (serializedUdonProgramAsset.name == guid)
                    return serializedUdonProgramAsset;

                string oldSerializedProgramAssetPath = Path.Combine("Assets", "SerializedUdonPrograms", $"{serializedUdonProgramAsset.name}.asset");
                AssetDatabase.DeleteAsset(oldSerializedProgramAssetPath);
            }

            string serializedUdonProgramAssetPath = Path.Combine("Assets", "SerializedUdonPrograms", $"{guid}.asset");

            serializedUdonProgramAsset = AssetDatabase.LoadAssetAtPath<SerializedUdonProgramAsset>(serializedUdonProgramAssetPath);

            if (serializedUdonProgramAsset)
                return serializedUdonProgramAsset;

            serializedUdonProgramAsset = CreateInstance<SerializedUdonProgramAsset>();
            if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(serializedUdonProgramAssetPath)))
            {
                AssetDatabase.CreateFolder("Assets", "SerializedUdonPrograms");
            }

            AssetDatabase.CreateAsset(serializedUdonProgramAsset, serializedUdonProgramAssetPath);

            return serializedUdonProgramAsset;
        }

        public void ApplyProgram()
        {
            GetSerializedProgramAssetWithoutRefresh().StoreProgram(program);
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