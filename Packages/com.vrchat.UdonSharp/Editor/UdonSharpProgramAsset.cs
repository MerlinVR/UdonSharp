
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp.Compiler;
using UdonSharp.Compiler.Udon;
using UdonSharp.Lib.Internal;
using UdonSharp.Localization;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.Editor.ProgramSources.Attributes;
using VRC.Udon.ProgramSources;
using VRC.Udon.Serialization.OdinSerializer;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: UdonProgramSourceNewMenu(typeof(UdonSharp.UdonSharpProgramAsset), "Udon C# Program Asset")]

namespace UdonSharp
{
    public enum UdonSharpProgramVersion
    {
        Unknown,
        V0,
        /// <summary>
        /// Any fields that can't be serialized by Unity need to get marked with OdinSerialized
        /// </summary>
        V1SerializationUpdate,
        NextVer,
        CurrentVersion = NextVer - 1,
    }
    
    [CreateAssetMenu(menuName = "VRChat/Udon/Udon C# Program Asset", fileName = "New Udon C# Program Asset")]
    public class UdonSharpProgramAsset : UdonAssemblyProgramAsset
    {
        [HideInInspector]
        public MonoScript sourceCsScript;

        [SerializeField]
        private UdonSharpProgramVersion scriptVersion = UdonSharpProgramVersion.Unknown;

        /// <summary>
        /// The version the attached C# script is on, determines if this needs an upgrade pass that rewrites the script file.
        /// </summary>
        public UdonSharpProgramVersion ScriptVersion
        {
            get => scriptVersion;
            set
            {
                if (scriptVersion != value)
                {
                    scriptVersion = value;
                    EditorUtility.SetDirty(this);
                }
            }
        }
        
        [SerializeField]
        private UdonSharpProgramVersion compiledVersion = UdonSharpProgramVersion.Unknown;

        /// <summary>
        /// The version this program asset has been built for. Is used for tracking what format serialized program data is in.
        /// </summary>
        public UdonSharpProgramVersion CompiledVersion
        {
            get => compiledVersion;
            set
            {
                if (compiledVersion != value)
                {
                    compiledVersion = value;
                    EditorUtility.SetDirty(this);
                }
            }
        }

        [NonSerialized, OdinSerialize]
        public Dictionary<string, FieldDefinition> fieldDefinitions;
        
        [HideInInspector]
        public BehaviourSyncMode behaviourSyncMode = BehaviourSyncMode.Any;

        [HideInInspector]
        public bool hasInteractEvent;

        [HideInInspector]
        public long scriptID;

        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        private UdonBehaviour currentBehaviour;

        internal bool showUtilityDropdown;

        internal void DrawErrorTextAreas()
        {
            UdonSharpGUI.DrawCompileErrorTextArea();
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

        internal new void DrawProgramDisassembly()
        {
            base.DrawProgramDisassembly();
        }

        internal new void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            currentBehaviour = udonBehaviour;

            if (!udonBehaviour)
            {
                EditorGUI.BeginDisabledGroup(sourceCsScript != null);
                EditorGUI.BeginChangeCheck();
                MonoScript newSourceCsScript = (MonoScript)EditorGUILayout.ObjectField(Loc.Get(LocStr.UI_SourceScript), sourceCsScript, typeof(MonoScript), false);
                EditorGUI.EndDisabledGroup();
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Changed source C# script");
                    sourceCsScript = newSourceCsScript;
                    dirty = true;
                }

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(Loc.Get(LocStr.UI_SerializedUdonProgramAsset), serializedUdonProgramAsset, typeof(AbstractSerializedUdonProgramAsset), false);
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

            bool shouldUseRuntimeValue = EditorApplication.isPlaying && currentBehaviour != null;

            // UdonBehaviours won't have valid heap values unless they have been enabled once to run their initialization. 
            // So we check against a value we know will exist to make sure we can use the heap variables.
            if (shouldUseRuntimeValue)
            {
                var behaviourID = currentBehaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (behaviourID == null)
                    shouldUseRuntimeValue = false;
            }

            // Just manually break the disabled scope in the UdonBehaviourEditor default drawing for now
            GUI.enabled = GUI.enabled || shouldUseRuntimeValue;
            shouldUseRuntimeValue &= GUI.enabled;

            // DrawPublicVariables(udonBehaviour, ref dirty);

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
            UdonSharpGUI.DrawUtilities(this);

            currentBehaviour = null;
        }

        protected override void RefreshProgramImpl()
        {
            if (sourceCsScript != null &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                !AnyUdonSharpScriptHasError())
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
        public static bool IsAnyProgramAssetSourceDirty()
        {
            UdonSharpProgramAsset[] programs = GetAllUdonSharpPrograms();
            
            UdonSharpEditorCache cache = UdonSharpEditorCache.Instance;
            bool needsCompile = false;
            foreach (UdonSharpProgramAsset programAsset in programs)
            {
                if (cache.IsSourceFileDirty(programAsset.sourceCsScript))
                {
                    needsCompile = true;
                    break;
                }
            }

            return needsCompile;
        }

        internal static bool IsAnyScriptDirty()
        {
            UdonSharpEditorCache cache = UdonSharpEditorCache.Instance;

            foreach (MonoScript script in CompilationContext.GetAllFilteredScripts(true))
            {
                if (cache.IsSourceFileDirty(script))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsAnyProgramAssetOutOfDate()
        {
            UdonSharpProgramAsset[] programs = GetAllUdonSharpPrograms();
            
            bool isOutOfDate = false;
            foreach (UdonSharpProgramAsset programAsset in programs)
            {
                if (programAsset.CompiledVersion != UdonSharpProgramVersion.CurrentVersion)
                {
                    isOutOfDate = true;
                    break;
                }
            }

            return isOutOfDate;
        }

        /// <summary>
        /// Compiles all U# programs in the project. If forceCompile is true, will skip checking for file changes to skip compile tasks
        /// </summary>
        /// <param name="forceCompile"></param>
        /// <param name="editorBuild"></param>
        [PublicAPI]
        public static void CompileAllCsPrograms(bool forceCompile = false, bool editorBuild = true)
        {
            if (!forceCompile && !IsAnyScriptDirty())
                return;

            UdonSharpCompilerV1.Compile(new UdonSharpCompileOptions() { IsEditorBuild = editorBuild });
        }

        private static UdonSharpProgramAsset[] _programAssetCache;
        internal static void ClearProgramAssetCache()
        {
            _programAssetCache = null;
            UdonSharpEditorUtility.ResetCaches();
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
                IEnumerable<UdonSharpProgramAsset> fallbackAssets1 = Resources.FindObjectsOfTypeAll<UdonProgramAsset>().OfType<UdonSharpProgramAsset>();

                foreach (UdonSharpProgramAsset fallbackAsset in fallbackAssets1)
                {
                    if (_programAssetCache != null && fallbackAsset != null && !_programAssetCache.Contains(fallbackAsset))
                    {
                        Debug.LogWarning($"Repairing program asset '{fallbackAsset}' which Unity has broken");
                        neededFallback = true;

                        string assetPath = AssetDatabase.GetAssetPath(fallbackAsset);
                        
                        if (!string.IsNullOrEmpty(assetPath))
                            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                }

                if (!neededFallback)
                {
                    IEnumerable<UdonSharpProgramAsset> fallbackAssets2 = AssetDatabase.FindAssets($"t:{nameof(UdonProgramAsset)}").Select(e => AssetDatabase.LoadAssetAtPath<UdonProgramAsset>(AssetDatabase.GUIDToAssetPath(e))).OfType<UdonSharpProgramAsset>();
                    foreach (UdonSharpProgramAsset fallbackAsset in fallbackAssets2)
                    {
                        if (_programAssetCache != null && fallbackAsset != null && !_programAssetCache.Contains(fallbackAsset))
                        {
                            Debug.LogWarning($"Repairing program asset '{fallbackAsset}' which Unity has broken pass 2");
                            neededFallback = true;
                            
                            string assetPath = AssetDatabase.GetAssetPath(fallbackAsset);
                        
                            if (!string.IsNullOrEmpty(assetPath))
                                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
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

            bool cacheNeedsCleanup = false;
            
            foreach (UdonSharpProgramAsset programAsset in _programAssetCache)
            {
                if (programAsset == null)
                {
                    cacheNeedsCleanup = true;
                    break;
                }
            }

            if (cacheNeedsCleanup)
            {
                UdonSharpUtils.LogWarning("Null program assets were found in cache and cleaned up.");
                _programAssetCache = _programAssetCache.Where(e => e != null).ToArray();
            }
            
            return (UdonSharpProgramAsset[])_programAssetCache.Clone();
        }

        [MenuItem("VRChat SDK/Udon Sharp/Refresh All UdonSharp Assets")]
        public static void UdonSharpCheckAbsent()
        {
            Debug.Log( "Checking Absent" );
            
            int cycles = -1;
            int lastNumAssets;
            int currentNumAssets;

            // Loop until we stop picking up assets.
            do
            {
                string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{nameof(UdonSharpProgramAsset)}");
                lastNumAssets = udonSharpDataAssets.Length;
                
                Debug.Log( $"Found {udonSharpDataAssets.Length} assets." );

                _programAssetCache = new UdonSharpProgramAsset[udonSharpDataAssets.Length];

                for (int i = 0; i < _programAssetCache.Length; ++i)
                {
                    udonSharpDataAssets[i] = AssetDatabase.GUIDToAssetPath(udonSharpDataAssets[i]);
                }

                foreach(string s in AssetDatabase.GetAllAssetPaths() )
                {
                    if(!udonSharpDataAssets.Contains(s))
                    {
                        Type t = AssetDatabase.GetMainAssetTypeAtPath(s);
                        if (t != null && t.FullName == "UdonSharp.UdonSharpProgramAsset")
                        {
                            Debug.Log( $"Trying to recover {s}" );
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(s);
                        }
                    }
                }

                ClearProgramAssetCache();

                GetAllUdonSharpPrograms();

                currentNumAssets = AssetDatabase.FindAssets($"t:{nameof(UdonSharpProgramAsset)}").Length;
                Debug.Log( $"Checking to see if we need to re-run. Last: {lastNumAssets}, This: {currentNumAssets}" );
                cycles++;
            } while( lastNumAssets != currentNumAssets );
            
            Debug.Log( $"Completed {cycles} refresh cycles, found {lastNumAssets} assets." );
        }

        [PublicAPI]
        public static bool AnyUdonSharpScriptHasError()
        {
            FieldInfo assemblyErrorField = typeof(UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (UdonSharpProgramAsset programAsset in GetAllUdonSharpPrograms())
            {
                if (programAsset.sourceCsScript == null)
                    continue;

                if (!string.IsNullOrEmpty((string)assemblyErrorField.GetValue(programAsset)))
                    return true;
            }

            return UdonSharpEditorCache.Instance.HasUdonSharpCompileError();
        }

        [PublicAPI]
        public static UdonSharpProgramAsset GetProgramAssetForClass(System.Type classType)
        {
            if (classType == null)
                throw new System.ArgumentNullException();
            
            return UdonSharpEditorUtility.GetUdonSharpProgramAsset(classType); 
        }
        
        [PublicAPI]
        public static Type GetBehaviourClass(UdonBehaviour behaviour)
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
        public Type GetClass()
        {
            // Needs to be an explicit null check because of Unity's object equality operator overloads
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (sourceCsScript != null)
                return sourceCsScript.GetClass();

            return null;
        }
        
        private static readonly FieldInfo _assemblyErrorField = typeof(UdonAssemblyProgramAsset).GetField("assemblyError", BindingFlags.NonPublic | BindingFlags.Instance);

        internal void AssembleCsProgram(string assembly, uint heapSize)
        {
            try
            {
                program = CompilerUdonInterface.Assemble(assembly, heapSize);
                _assemblyErrorField.SetValue(this, null);

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
                _assemblyErrorField.SetValue(this, e.Message);
                Debug.LogException(e);

                throw;
            }
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
    
#if UNITY_EDITOR
    [CustomEditor(typeof(UdonSharpProgramAsset))]
    internal class UdonSharpProgramAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var programAsset = (UdonSharpProgramAsset)target;

            bool refBool = false;
            programAsset.DrawProgramSourceGUI(null, ref refBool);
        }
    }
#endif
}