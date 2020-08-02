
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace UdonSharp.Compiler
{
    public class UdonSharpDebugInfoManager
    {
        public enum DebugInfoType
        {
            Editor,
            Game,
        }

        private const string DEBUG_INFO_PATH = "Library/UdonSharpCache/DebugInfo/";

        static UdonSharpDebugInfoManager _instance;
        public static UdonSharpDebugInfoManager Instance => GetInstance();
        static object instanceLock = new object();

        static UdonSharpDebugInfoManager GetInstance()
        {
            lock (instanceLock)
            {
                if (_instance != null)
                    return _instance;

                _instance = new UdonSharpDebugInfoManager();
                AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadFlushDebugInfo;

                return _instance;
            }
        }

        static void AssemblyReloadFlushDebugInfo()
        {
            Instance.FlushDirtyDebugInfos();
            AssemblyReloadEvents.beforeAssemblyReload -= AssemblyReloadFlushDebugInfo;
        }

        class UdonSharpDebugInfoWriter : UnityEditor.AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                UdonSharpDebugInfoManager.Instance.FlushDirtyDebugInfos();

                return paths;
            }
        }

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

            if (debugInfoType == DebugInfoType.Game)
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
    }
}
