
using JetBrains.Annotations;
using System.Collections.Generic;
using System.IO;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace UdonSharp
{
    /// <summary>
    /// Handles cache data for U# that gets saved to the Library. All data this uses is intermediate generated data that is not required and can be regenerated from the source files.
    /// </summary>
    [InitializeOnLoad]
    public class UdonSharpEditorCache
    {
        #region Instance and serialization management
        [System.Serializable]
        struct SourceHashLookupStorage
        {
            [OdinSerialize, System.NonSerialized]
            public Dictionary<string, string> sourceFileHashLookup;
            
            public DebugInfoType lastScriptBuildType;
        }

        private const string CACHE_DIR_PATH = "Library/UdonSharpCache/";
        private const string CACHE_FILE_PATH = "Library/UdonSharpCache/UdonSharpEditorCache.asset";

        public static UdonSharpEditorCache Instance => GetInstance();

        static UdonSharpEditorCache _instance;
        static readonly object instanceLock = new object();

        private static UdonSharpEditorCache GetInstance()
        {
            lock (instanceLock)
            {
                if (_instance != null)
                    return _instance;

                _instance = new UdonSharpEditorCache();

                if (File.Exists(CACHE_FILE_PATH))
                {
                    SourceHashLookupStorage storage = SerializationUtility.DeserializeValue<SourceHashLookupStorage>(File.ReadAllBytes(CACHE_FILE_PATH), DataFormat.Binary);
                    _instance.sourceFileHashLookup = storage.sourceFileHashLookup;
                    _instance.LastBuildType = storage.lastScriptBuildType;
                }

                return _instance;
            }
        }

        static UdonSharpEditorCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadSave;
        }

        // Saves cache on play mode exit/enter and once we've entered the target mode reload the state from disk to persist the changes across play/edit mode
        static internal void SaveOnPlayExit(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.ExitingEditMode)
            {
                SaveAllCache();
            }
        }

        static internal void SaveAllCache()
        {
            if (_instance != null)
                Instance.SaveAllCacheData();
        }

        internal static void ResetInstance()
        {
            _instance = null;
        }

        class UdonSharpEditorCacheWriter : UnityEditor.AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                Instance.SaveAllCacheData();

                return paths;
            }

            public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
            {
                UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);

                if (programAsset)
                {
                    Instance.ClearSourceHash(programAsset);
                }
                else if(AssetDatabase.IsValidFolder(assetPath))
                {
                    string[] assetGuids = AssetDatabase.FindAssets($"t:{nameof(UdonSharpProgramAsset)}", new string[] { assetPath });

                    foreach (string guid in assetGuids)
                    {
                        programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(guid));

                        if (programAsset)
                            Instance.ClearSourceHash(programAsset);
                    }
                }

                return AssetDeleteResult.DidNotDelete;
            }
        }

        static void AssemblyReloadSave()
        {
            Instance.SaveAllCacheData();
        }

        void SaveAllCacheData()
        {
            if (!Directory.Exists(CACHE_DIR_PATH))
                Directory.CreateDirectory(CACHE_DIR_PATH);
            
            if (_sourceDirty)
            {
                SourceHashLookupStorage storage = new SourceHashLookupStorage() {
                    sourceFileHashLookup = _instance.sourceFileHashLookup,
                    lastScriptBuildType = LastBuildType,
                };
                File.WriteAllBytes(CACHE_FILE_PATH, SerializationUtility.SerializeValue<SourceHashLookupStorage>(storage, DataFormat.Binary));
                _sourceDirty = false;
            }

            FlushDirtyDebugInfos();
            FlushUasmCache();
        }
        #endregion

        #region Source file modification cache
        bool _sourceDirty = false;
        Dictionary<string, string> sourceFileHashLookup = new Dictionary<string, string>();
         
        public bool IsSourceFileDirty(UdonSharpProgramAsset programAsset)
        {
            if (programAsset?.sourceCsScript == null)
                return false;
            
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string programAssetGuid, out long _))
                return false;
            
            // We haven't seen the source file before, so it needs to be compiled
            if (!sourceFileHashLookup.TryGetValue(programAssetGuid, out string sourceFileHash))
                return true;

            string currentHash = HashSourceFile(programAsset.sourceCsScript);

            if (currentHash != sourceFileHash)
                return true;

            return false;
        }

        public void UpdateSourceHash(UdonSharpProgramAsset programAsset, string sourceText)
        {
            if (programAsset?.sourceCsScript == null)
                return;
            
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string programAssetGuid, out long _))
                return;

            string newHash = UdonSharpUtils.HashString(sourceText);

            if (sourceFileHashLookup.ContainsKey(programAssetGuid))
            {
                if (sourceFileHashLookup[programAssetGuid] != newHash)
                    _sourceDirty = true;

                sourceFileHashLookup[programAssetGuid] = newHash;
            }
            else
            {
                sourceFileHashLookup.Add(programAssetGuid, newHash);
                _sourceDirty = true;
            }
        }

        /// <summary>
        /// Clears the source hash, this is used when a script hits a compile error in order to allow an undo to compile the scripts.
        /// </summary>
        /// <param name="programAsset"></param>
        public void ClearSourceHash(UdonSharpProgramAsset programAsset)
        {
            if (programAsset?.sourceCsScript == null)
                return;

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string programAssetGuid, out long _))
                return;

            if (sourceFileHashLookup.ContainsKey(programAssetGuid))
            {
                if (sourceFileHashLookup[programAssetGuid] != "")
                    _sourceDirty = true;

                sourceFileHashLookup[programAssetGuid] = "";
            }
            else
            {
                sourceFileHashLookup.Add(programAssetGuid, "");
                _sourceDirty = true;
            }
        }

        private static string HashSourceFile(MonoScript script)
        {
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string scriptText = "";

            try
            {
                scriptText = UdonSharpUtils.ReadFileTextSync(scriptPath);
            }
            catch (System.Exception e)
            {
                scriptText = Random.value.ToString();
                Debug.Log(e);
            }

            return UdonSharpUtils.HashString(scriptText);
        }

        DebugInfoType _lastBuildType = DebugInfoType.Editor;
        public DebugInfoType LastBuildType
        {
            get => _lastBuildType;
            set
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

        ClassDebugInfo LoadDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceProgram, out string guid, out long _))
            {
                return null;
            }

            string debugInfoPath = $"{DEBUG_INFO_PATH}{guid}_{debugInfoType}.asset";

            if (!File.Exists(debugInfoPath))
                return null;

            ClassDebugInfo classDebugInfo = null;

            try
            {
                classDebugInfo = SerializationUtility.DeserializeValue<ClassDebugInfo>(File.ReadAllBytes(debugInfoPath), DataFormat.Binary);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                return null;
            }

            return classDebugInfo;
        }

        void SaveDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType, ClassDebugInfo debugInfo)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceProgram, out string guid, out long _))
            {
                return;
            }

            string debugInfoPath = $"{DEBUG_INFO_PATH}{guid}_{debugInfoType}.asset";

            if (!Directory.Exists(DEBUG_INFO_PATH))
                Directory.CreateDirectory(DEBUG_INFO_PATH);

            File.WriteAllBytes(debugInfoPath, SerializationUtility.SerializeValue<ClassDebugInfo>(debugInfo, DataFormat.Binary));
        }

        Dictionary<UdonSharpProgramAsset, Dictionary<DebugInfoType, ClassDebugInfo>> _classDebugInfoLookup = new Dictionary<UdonSharpProgramAsset, Dictionary<DebugInfoType, ClassDebugInfo>>();

        /// <summary>
        /// Gets the debug info for a given program asset. If debug info type for Client is specified when there is no client debug info, will fall back to Editor debug info.
        /// </summary>
        /// <param name="sourceProgram"></param>
        /// <param name="debugInfoType"></param>
        /// <returns></returns>
        [PublicAPI]
        public ClassDebugInfo GetDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType)
        {
            if (!_classDebugInfoLookup.TryGetValue(sourceProgram, out var debugInfo))
            {
                debugInfo = new Dictionary<DebugInfoType, ClassDebugInfo>();
                _classDebugInfoLookup.Add(sourceProgram, debugInfo);
            }

            if (debugInfo.TryGetValue(debugInfoType, out ClassDebugInfo info))
            {
                return info;
            }

            ClassDebugInfo loadedInfo = LoadDebugInfo(sourceProgram, debugInfoType);
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

        HashSet<ClassDebugInfo> dirtyDebugInfos = new HashSet<ClassDebugInfo>(new ReferenceEqualityComparer<ClassDebugInfo>());
        object setDebugInfoLock = new object();

        public void SetDebugInfo(UdonSharpProgramAsset sourceProgram, DebugInfoType debugInfoType, ClassDebugInfo debugInfo)
        {
            lock (setDebugInfoLock)
            {
                dirtyDebugInfos.Add(debugInfo);

                if (!_classDebugInfoLookup.TryGetValue(sourceProgram, out var debugInfos))
                {
                    debugInfos = new Dictionary<DebugInfoType, ClassDebugInfo>();
                    _classDebugInfoLookup.Add(sourceProgram, debugInfos);
                }

                if (!debugInfos.ContainsKey(debugInfoType))
                    debugInfos.Add(debugInfoType, debugInfo);
                else
                    debugInfos[debugInfoType] = debugInfo;
            }
        }

        void FlushDirtyDebugInfos()
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
        #endregion

        #region UASM cache
        const string UASM_DIR_PATH = "Library/UdonSharpCache/UASM/";

        // UdonSharpProgramAsset GUID to uasm lookup
        Dictionary<string, string> _uasmCache = new Dictionary<string, string>();

        void FlushUasmCache()
        {
            if (!Directory.Exists(UASM_DIR_PATH))
                Directory.CreateDirectory(UASM_DIR_PATH);

            foreach (var uasmCacheEntry in _uasmCache)
            {
                string filePath = $"{UASM_DIR_PATH}{uasmCacheEntry.Key}.uasm";

                File.WriteAllText(filePath, uasmCacheEntry.Value);
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

            if (_uasmCache.TryGetValue(guid, out string uasm))
                return uasm;

            string filePath = $"{UASM_DIR_PATH}{guid}.uasm";
            if (File.Exists(filePath))
            {
                uasm = UdonSharpUtils.ReadFileTextSync(filePath);

                _uasmCache.Add(guid, uasm);
                return uasm;
            }

            return "";
        }

        static object uasmSetLock = new object();
        public void SetUASMStr(UdonSharpProgramAsset programAsset, string uasm)
        {
            lock (uasmSetLock)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string guid, out long _))
                    return;

                if (_uasmCache.ContainsKey(guid))
                    _uasmCache[guid] = uasm;
                else
                    _uasmCache.Add(guid, uasm);
            }
        }
        #endregion
    }
}
