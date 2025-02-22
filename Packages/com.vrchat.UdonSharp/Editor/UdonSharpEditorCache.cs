
using System;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Debug = UnityEngine.Debug;
using SerializationUtility = VRC.Udon.Serialization.OdinSerializer.SerializationUtility;

namespace UdonSharp
{
    /// <summary>
    /// Handles cache data for U# that gets saved to the Library. All data this uses is intermediate generated data that is not required and can be regenerated from the source files.
    /// </summary>
    [InitializeOnLoad]
    internal class UdonSharpEditorCache
    {
        private const string CACHE_DIR_PATH = "Library/UdonSharpCache/";
        private const string CACHE_FILE_PATH = "Library/UdonSharpCache/UdonSharpEditorCache.dat"; // Old cache ended in .asset
        private const string UASM_DIR_PATH = "Library/UdonSharpCache/UASM/";
        private const string SERIALIZATION_INFO_PATH = "Library/UdonSharpCache/SerializedClassData.dat";
        
    #region Instance and serialization management
        [Serializable]
        internal struct CompileDiagnostic
        {
            public DiagnosticSeverity severity;
            public string file;
            public int line;
            public int character;
            public string message;
        }
        
        [Serializable]
        private struct UdonSharpCacheStorage
        {
            public ProjectInfo info;
            
            [OdinSerialize, NonSerialized]
            public Dictionary<string, string> sourceFileHashLookup;
            
            public DebugInfoType lastScriptBuildType;

            public CompileDiagnostic[] diagnostics;
        }

        public static UdonSharpEditorCache Instance => GetInstance();

        private static UdonSharpEditorCache _instance;
        private static readonly object _instanceLock = new object();

        private const int CURR_CACHE_VER = 2;
        
        private static UdonSharpEditorCache GetInstance()
        {
            lock (_instanceLock)
            {
                if (_instance != null)
                    return _instance;

                _instance = new UdonSharpEditorCache();
                _instance._info.version = CURR_CACHE_VER;
                _instance._info.projectNeedsUpgrade = true;

                if (!File.Exists(CACHE_FILE_PATH))
                    return _instance;
                
                UdonSharpCacheStorage storage = SerializationUtility.DeserializeValue<UdonSharpCacheStorage>(File.ReadAllBytes(CACHE_FILE_PATH), DataFormat.Binary);
                _instance._sourceFileHashLookup = storage.sourceFileHashLookup;
                _instance.LastBuildType = storage.lastScriptBuildType;
                _instance._info = storage.info;
                _instance._diagnostics = storage.diagnostics ?? Array.Empty<CompileDiagnostic>();

                // For now we just use this to see if we need to check for project serialization upgrade, may be extended later on. At the moment only used to avoid wasting time on extra validation when possible.
                // Hey now we use this to nuke out old data too
                if (_instance._info.version < CURR_CACHE_VER)
                {
                    _instance._info.version = CURR_CACHE_VER;
                    _instance._sourceFileHashLookup = new Dictionary<string, string>();
                    _instance._info.projectNeedsUpgrade = true;
                }

                return _instance;
            }
        }

        static UdonSharpEditorCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadSave;
        }

        // Saves cache on play mode exit/enter and once we've entered the target mode reload the state from disk to persist the changes across play/edit mode
        internal static void SaveOnPlayExit(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.ExitingEditMode)
            {
                SaveAllCache();
            }
        }

        internal static void SaveAllCache()
        {
            if (_instance != null)
                Instance.SaveAllCacheData();
        }

        internal static void ResetInstance()
        {
            _instance = null;
        }

        private class UdonSharpEditorCacheWriter : UnityEditor.AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                Instance.SaveAllCacheData();

                return paths;
            }

            public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                if (script)
                {
                    Instance.ClearSourceHash(script);
                }
                else if(AssetDatabase.IsValidFolder(assetPath))
                {
                    string[] assetGuids = AssetDatabase.FindAssets($"t:{nameof(MonoScript)}", new [] { assetPath });

                    foreach (string guid in assetGuids)
                    {
                        script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));

                        if (script)
                            Instance.ClearSourceHash(script);
                    }
                }

                return AssetDeleteResult.DidNotDelete;
            }
        }

        private static void AssemblyReloadSave()
        {
            Instance.SaveAllCacheData();
        }

        private void SaveAllCacheData()
        {
            if (_sourceDirty || _infoDirty)
            {
                if (!Directory.Exists(CACHE_DIR_PATH))
                    Directory.CreateDirectory(CACHE_DIR_PATH);

                UdonSharpCacheStorage storage = new UdonSharpCacheStorage() {
                    sourceFileHashLookup = _instance._sourceFileHashLookup,
                    lastScriptBuildType = LastBuildType,
                    info = _info,
                    diagnostics = _diagnostics.ToArray(),
                };
                File.WriteAllBytes(CACHE_FILE_PATH, SerializationUtility.SerializeValue<UdonSharpCacheStorage>(storage, DataFormat.Binary));
                _sourceDirty = false;
                _infoDirty = false;
            }

            FlushDirtyDebugInfos();
            FlushUasmCache();
            FlushSerializationInfo();
        }
    #endregion

    #region Project Global State
        
        [Serializable]
        public struct ProjectInfo
        {
            [SerializeField]
            internal int version;
            
            public bool projectNeedsUpgrade;
        }

        private bool _infoDirty;
        private ProjectInfo _info;

        public ProjectInfo Info
        {
            get => _info;
            private set
            {
                _info = value;
                _infoDirty = true;
            }
        }

        public void QueueUpgradePass()
        {
            ProjectInfo info = Info;

            info.projectNeedsUpgrade = true;

            Info = info;
        }
        
        public void ClearUpgradePassQueue()
        {
            ProjectInfo info = Info;

            info.projectNeedsUpgrade = false;

            Info = info;
        }

        private CompileDiagnostic[] _diagnostics = Array.Empty<CompileDiagnostic>();

        internal CompileDiagnostic[] LastCompileDiagnostics
        {
            get => _diagnostics;
            set
            {
                _diagnostics = value;
                
                if (_diagnostics == null)
                    _diagnostics = Array.Empty<CompileDiagnostic>();

                _infoDirty = true;
            }
        }

        internal bool HasUdonSharpCompileError()
        {
            foreach (var diagnostic in LastCompileDiagnostics)
            {
                if (diagnostic.severity == DiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

    #endregion

    #region Source file modification cache

        private bool _sourceDirty;
        private Dictionary<string, string> _sourceFileHashLookup = new Dictionary<string, string>();
         
        public bool IsSourceFileDirty(MonoScript script)
        {
            if (script == null)
                return false;
            
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGuid, out long _))
                return false;
            
            // We haven't seen the source file before, so it needs to be compiled
            if (!_sourceFileHashLookup.TryGetValue(scriptGuid, out string sourceFileHash))
                return true;

            string currentHash = HashSourceFile(script);

            if (currentHash != sourceFileHash)
                return true;

            return false;
        }

        public void UpdateSourceHash(MonoScript script, string sourceText)
        {
            if (script == null)
                return;
            
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGuid, out long _))
                return;

            string newHash = UdonSharpUtils.HashString(sourceText);

            if (_sourceFileHashLookup.ContainsKey(scriptGuid))
            {
                if (_sourceFileHashLookup[scriptGuid] != newHash)
                    _sourceDirty = true;

                _sourceFileHashLookup[scriptGuid] = newHash;
            }
            else
            {
                _sourceFileHashLookup.Add(scriptGuid, newHash);
                _sourceDirty = true;
            }
        }

        public void RehashAllScripts()
        {
            HashSet<string> hashesToPrune = new HashSet<string>(_sourceFileHashLookup.Keys);

            foreach (string path in CompilationContext.GetAllFilteredSourcePaths(true))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                
                if (!script)
                    continue;
                
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGuid, out long _))
                    continue;

                hashesToPrune.Remove(scriptGuid);

                string newHash = HashSourceFile(script);
                
                if (_sourceFileHashLookup.ContainsKey(scriptGuid))
                {
                    if (_sourceFileHashLookup[scriptGuid] != newHash)
                    {
                        _sourceDirty = true;
                        _sourceFileHashLookup[scriptGuid] = newHash;
                    }
                }
                else
                {
                    _sourceFileHashLookup.Add(scriptGuid, newHash);
                    _sourceDirty = true;
                }
            }

            foreach (string pruneHash in hashesToPrune)
            {
                _sourceFileHashLookup.Remove(pruneHash);
                _sourceDirty = true;
            }
        }

        /// <summary>
        /// Clears the source hash, this is used when a script hits a compile error in order to allow an undo to compile the scripts.
        /// </summary>
        private void ClearSourceHash(MonoScript script)
        {
            if (script == null)
                return;

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGuid, out long _))
                return;

            if (!_sourceFileHashLookup.ContainsKey(scriptGuid)) 
                return;
            
            _sourceFileHashLookup.Remove(scriptGuid);
            _sourceDirty = true;
        }

        private static string HashSourceFile(MonoScript script)
        {
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string scriptText;

            try
            {
                scriptText = UdonSharpUtils.ReadFileTextSync(scriptPath);
            }
            catch (Exception e)
            {
                scriptText = "";
                UdonSharpUtils.LogError($"Unable to read source file for hashing. Exception: {e}");
            }

            return UdonSharpUtils.HashString(scriptText);
        }

        private DebugInfoType _lastBuildType = DebugInfoType.Editor;
        public DebugInfoType LastBuildType
        {
            get => _lastBuildType;
            internal set
            {
                if (_lastBuildType != value)
                    _sourceDirty = true;

                _lastBuildType = value;
            }
        }

    #endregion

    #region Debug info cache
        public enum DebugInfoType
        {
            Editor,
            Client,
        }

        private const string DEBUG_INFO_PATH = "Library/UdonSharpCache/DebugInfo/";

        private static AssemblyDebugInfo LoadDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceProgram, out string guid, out long _))
            {
                return null;
            }

            string debugInfoPath = $"{DEBUG_INFO_PATH}{guid}_{debugInfoType}.asset";

            if (!File.Exists(debugInfoPath))
                return null;

            AssemblyDebugInfo debugInfo;

            try
            {
                debugInfo = SerializationUtility.DeserializeValue<AssemblyDebugInfo>(File.ReadAllBytes(debugInfoPath), DataFormat.Binary);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                return null;
            }

            return debugInfo;
        }

        private static void SaveDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType, AssemblyDebugInfo debugInfo)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceProgram, out string guid, out long _))
            {
                return;
            }

            string debugInfoPath = $"{DEBUG_INFO_PATH}{guid}_{debugInfoType}.asset";

            if (!Directory.Exists(DEBUG_INFO_PATH))
                Directory.CreateDirectory(DEBUG_INFO_PATH);

            File.WriteAllBytes(debugInfoPath, SerializationUtility.SerializeValue<AssemblyDebugInfo>(debugInfo, DataFormat.Binary));
        }

        private Dictionary<UdonSharpProgramAsset, Dictionary<DebugInfoType, AssemblyDebugInfo>> _classDebugInfoLookup = new Dictionary<UdonSharpProgramAsset, Dictionary<DebugInfoType, AssemblyDebugInfo>>();

        /// <summary>
        /// Gets the debug info for a given program asset. If debug info type for Client is specified when there is no client debug info, will fall back to Editor debug info.
        /// </summary>
        /// <param name="sourceProgram"></param>
        /// <param name="debugInfoType"></param>
        /// <returns></returns>
        [PublicAPI]
        public AssemblyDebugInfo GetDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType)
        {
            lock (setDebugInfoLock)
            {
                if (!_classDebugInfoLookup.TryGetValue(sourceProgram, out var debugInfo))
                {
                    debugInfo = new Dictionary<DebugInfoType, AssemblyDebugInfo>();
                    _classDebugInfoLookup.Add(sourceProgram, debugInfo);
                }

                if (debugInfo.TryGetValue(debugInfoType, out AssemblyDebugInfo info))
                {
                    return info;
                }

                AssemblyDebugInfo loadedInfo = LoadDebugInfo(sourceProgram, debugInfoType);
                if (loadedInfo != null)
                {
                    debugInfo.Add(debugInfoType, loadedInfo);
                    return loadedInfo;
                }

                if (debugInfoType == DebugInfoType.Client)
                {
                    if (debugInfo.TryGetValue(DebugInfoType.Editor, out info))
                        return info;

                    loadedInfo = LoadDebugInfo(sourceProgram, DebugInfoType.Editor);
                    if (loadedInfo != null)
                    {
                        debugInfo.Add(DebugInfoType.Editor, loadedInfo);
                        return loadedInfo;
                    }
                }

                return null;
            }
        }

        private HashSet<AssemblyDebugInfo> dirtyDebugInfos = new HashSet<AssemblyDebugInfo>(new ReferenceEqualityComparer<AssemblyDebugInfo>());
        private readonly object setDebugInfoLock = new object();

        public void SetDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType, AssemblyDebugInfo debugInfo)
        {
            lock (setDebugInfoLock)
            {
                dirtyDebugInfos.Add(debugInfo);

                if (!_classDebugInfoLookup.TryGetValue(sourceProgram, out var debugInfos))
                {
                    debugInfos = new Dictionary<DebugInfoType, AssemblyDebugInfo>();
                    _classDebugInfoLookup.Add(sourceProgram, debugInfos);
                }

                debugInfos[debugInfoType] = debugInfo;
            }
        }

        private void FlushDirtyDebugInfos()
        {
            lock (setDebugInfoLock)
            {
                foreach (var sourceProgramInfos in _classDebugInfoLookup)
                {
                    foreach (var debugInfo in sourceProgramInfos.Value)
                    {
                        if (dirtyDebugInfos.Contains(debugInfo.Value))
                        {
                            SaveDebugInfo(sourceProgramInfos.Key, debugInfo.Key, debugInfo.Value);
                        }
                    }
                }

                dirtyDebugInfos.Clear();
            }
        }
    #endregion

    #region UASM cache
        // UdonSharpProgramAsset GUID to uasm lookup
        private Dictionary<string, string> _uasmCache = new Dictionary<string, string>();

        private static readonly object _uasmSetLock = new object();
        
        private void FlushUasmCache()
        {
            lock (_uasmSetLock)
            {
                if (!Directory.Exists(UASM_DIR_PATH))
                    Directory.CreateDirectory(UASM_DIR_PATH);

                foreach (var uasmCacheEntry in _uasmCache)
                {
                    string filePath = $"{UASM_DIR_PATH}{uasmCacheEntry.Key}.uasm";

                    File.WriteAllText(filePath, uasmCacheEntry.Value);
                }
            }
        }

        /// <summary>
        /// Gets the uasm string for the last build of the given program asset
        /// </summary>
        /// <param name="programAsset"></param>
        /// <returns></returns>
        [PublicAPI]
        public string GetUASMStr(UdonSharpProgramAsset programAsset)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string guid, out long _))
                return "";

            lock (_uasmSetLock)
            {
                if (_uasmCache.TryGetValue(guid, out string uasm))
                    return uasm;

                string filePath = $"{UASM_DIR_PATH}{guid}.uasm";
                if (!File.Exists(filePath))
                    return "";
                
                uasm = UdonSharpUtils.ReadFileTextSync(filePath);

                _uasmCache.Add(guid, uasm);
                return uasm;
            }
        }
        
        internal void SetUASMStr(UdonSharpProgramAsset programAsset, string uasm)
        {
            lock (_uasmSetLock)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string guid, out long _))
                    return;

                _uasmCache[guid] = uasm;
            }
        }
    #endregion

    #region Serialization Info

        [Serializable]
        internal class UdonFieldInfo
        {
            public string fieldName;
            public Type fieldType;
            public int fieldIndex;
            public bool isStrongBoxed;
            
            public UdonFieldInfo()
            {
            }
            
            public UdonFieldInfo(UdonFieldInfo other)
            {
                fieldName = other.fieldName;
                fieldType = other.fieldType;
                fieldIndex = other.fieldIndex;
                isStrongBoxed = other.isStrongBoxed;
            }
        }
        
        [Serializable]
        internal class UdonClassInfo
        {
            public Type classType;
            public UdonFieldInfo[] fields = Array.Empty<UdonFieldInfo>();
        }
        
        [Serializable]
        internal class UdonClassCache
        {
            public UdonClassInfo[] classInfos = Array.Empty<UdonClassInfo>();
        }
        
        private Dictionary<string, UdonClassInfo> _serializationInfo;
        
        private static readonly object _serializationInfoLock = new object();
        
        private void FlushSerializationInfo()
        {
            lock (_serializationInfoLock)
            {
                if (_serializationInfo == null)
                    return;

                string directoryName = Path.GetDirectoryName(SERIALIZATION_INFO_PATH);
                
                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);
                
                byte[] serializedData = SerializationUtility.SerializeValue(_serializationInfo, DataFormat.Binary);
                File.WriteAllBytes(SERIALIZATION_INFO_PATH, serializedData);
            }
        }
        
        internal void SetSerializationInfo(Type type, UdonClassInfo classInfo)
        {
            lock (_serializationInfoLock)
            {
                if (_serializationInfo == null)
                    _serializationInfo = new Dictionary<string, UdonClassInfo>();

                if (type.IsConstructedGenericType)
                {
                    type = type.GetGenericTypeDefinition();
                    classInfo.classType = type;

                    foreach (UdonFieldInfo field in classInfo.fields)
                    {
                        FieldInfo genericField = type.GetField(field.fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        if (genericField != null)
                        {
                            field.fieldType = genericField.FieldType;
                        }
                    }
                }
                
                _serializationInfo[type.FullName] = classInfo;
            }
        }
        
        internal void ClearSerializationInfos()
        {
            lock (_serializationInfoLock)
            {
                _serializationInfo?.Clear();
            }
        }
        
        internal UdonClassInfo GetSerializationInfo(Type type)
        {
            lock (_serializationInfoLock)
            {
                if (_serializationInfo == null)
                {
                    _serializationInfo = new Dictionary<string, UdonClassInfo>();

                    if (File.Exists(SERIALIZATION_INFO_PATH))
                    {
                        Dictionary<string, UdonClassInfo> classCache = SerializationUtility.DeserializeValue<Dictionary<string, UdonClassInfo>>(File.ReadAllBytes(SERIALIZATION_INFO_PATH), DataFormat.Binary);

                        if (classCache != null)
                        {
                            List<string> classesToRemove = new List<string>();

                            // It's possible that the class cache has some classes that are no longer valid due to users deleting or renaming the types, so we'll prune those out
                            foreach (var currentInfo in classCache)
                            {
                                if (currentInfo.Value.classType == null)
                                {
                                    classesToRemove.Add(currentInfo.Key);
                                }
                            }

                            foreach (string key in classesToRemove)
                            {
                                classCache.Remove(key);
                            }

                            _serializationInfo = classCache;
                        }
                    }
                }

                Type[] genericArguments = null;
                
                if (type.IsConstructedGenericType)
                {
                    genericArguments = type.GetGenericArguments();
                    type = type.GetGenericTypeDefinition();
                }

                if (!_serializationInfo.TryGetValue(type.FullName, out UdonClassInfo classInfo))
                {
                    return null;
                }

                if (genericArguments == null)
                {
                    return classInfo;
                }

                UdonClassInfo constructedClassInfo = new UdonClassInfo()
                {
                    classType = type.MakeGenericType(genericArguments),
                    fields = classInfo.fields.Select(e => new UdonFieldInfo(e)).ToArray(), // Copy the fields so we don't modify the cached data
                };

                foreach (UdonFieldInfo fieldInfo in constructedClassInfo.fields)
                {
                    // Get matching field from the constructed type and update the field type
                    FieldInfo constructedField = constructedClassInfo.classType.GetField(fieldInfo.fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (constructedField != null)
                    {
                        fieldInfo.fieldType = constructedField.FieldType;
                    }
                }

                return constructedClassInfo;
            }
        }

    #endregion
    }
}
