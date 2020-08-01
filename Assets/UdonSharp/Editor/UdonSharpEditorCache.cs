
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using VRC.Udon.Serialization.OdinSerializer;

namespace UdonSharp
{
    internal class UdonSharpEditorCache
    {
        #region Instance and serialization management
        private const string CACHE_PATH = "Library/UdonSharpCache/UdonSharpEditorCache.asset";

        public static UdonSharpEditorCache Instance => GetInstance();

        static UdonSharpEditorCache _currentCache;
        private static UdonSharpEditorCache GetInstance()
        {
            if (_currentCache != null)
                return _currentCache;

            if (File.Exists(CACHE_PATH))
            {
                _currentCache = SerializationUtility.DeserializeValue<UdonSharpEditorCache>(File.ReadAllBytes(CACHE_PATH), DataFormat.Binary);
                return _currentCache;
            }

            string dirName = Path.GetDirectoryName(CACHE_PATH);
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            _currentCache = new UdonSharpEditorCache();

            return _currentCache;
        }

        class UdonSharpEditorCacheWriter : UnityEditor.AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                UdonSharpEditorCache cache = UdonSharpEditorCache.Instance;
                if (cache._dirty)
                {
                    File.WriteAllBytes(CACHE_PATH, SerializationUtility.SerializeValue<UdonSharpEditorCache>(_currentCache, DataFormat.Binary));
                    cache._dirty = false;
                }

                return paths;
            }
        }
        #endregion

        bool _dirty = false;

        [OdinSerialize]
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

        public void UpdateSourceHash(UdonSharpProgramAsset programAsset)
        {
            if (programAsset?.sourceCsScript == null)
                return;
            
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(programAsset, out string programAssetGuid, out long _))
                return;

            string newHash = HashSourceFile(programAsset.sourceCsScript);

            if (sourceFileHashLookup.ContainsKey(programAssetGuid))
            {
                if (sourceFileHashLookup[programAssetGuid] != newHash)
                    _dirty = true;

                sourceFileHashLookup[programAssetGuid] = newHash;
            }
            else
            {
                sourceFileHashLookup.Add(programAssetGuid, newHash);
                _dirty = true;
            }
        }

        private static string HashSourceFile(MonoScript script)
        {
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string scriptText = UdonSharpUtils.ReadFileTextSync(scriptPath);

            return UdonSharpUtils.HashString(scriptText);
        }
    }
}
