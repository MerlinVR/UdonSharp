
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    [InitializeOnLoad]
    public static class RuntimeExceptionWatcher
    {
        class LogFileState
        {
            public string playerName;
            public long lineOffset = -1;
        }

        static Queue<string> debugOutputQueue = new Queue<string>();
        static Dictionary<long, (string, UdonSharpProgramAsset)> scriptLookup;
        
        // Log watcher vars
        static FileSystemWatcher logDirectoryWatcher;
        static object logModifiedLock = new object();
        static Dictionary<string, LogFileState> logFileStates = new Dictionary<string, LogFileState>();
        static HashSet<string> modifiedLogPaths = new HashSet<string>();

        static RuntimeExceptionWatcher()
        {
            EditorApplication.update += OnEditorUpdate;
            Application.logMessageReceived += OnLog;
        }

        static bool InitializeScriptLookup()
        {
            if (scriptLookup != null)
                return true;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;

            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();

            if (udonSharpSettings == null || !udonSharpSettings.listenForVRCExceptions)
                return false;

            AssemblyReloadEvents.beforeAssemblyReload += CleanupLogWatcher;

            if (logDirectoryWatcher == null)
            {
                // Now setup the filesystem watcher
                string[] splitPath = Application.persistentDataPath.Split('/', '\\');
                string VRCDataPath = string.Join("\\", splitPath.Take(splitPath.Length - 2)) + "\\VRChat\\VRChat";
                
                if (Directory.Exists(VRCDataPath))
                {
                    logDirectoryWatcher = new FileSystemWatcher(VRCDataPath, "output_log_*.txt");
                    logDirectoryWatcher.IncludeSubdirectories = false;
                    logDirectoryWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    logDirectoryWatcher.Changed += OnLogFileChanged;
                    logDirectoryWatcher.EnableRaisingEvents = false;
                }
                else
                {
                    Debug.LogError("[UdonSharp] Could not locate VRChat data directory for exception watcher");
                }
            }

            scriptLookup = new Dictionary<long, (string, UdonSharpProgramAsset)>();
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            UdonSharpEditorCache editorCache = UdonSharpEditorCache.Instance;

            foreach (string dataGuid in udonSharpDataAssets)
            {
                UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid));

                if (programAsset.sourceCsScript == null)
                    continue;

                if (programAsset.SerializedProgramAsset == null)
                    continue;

                IUdonProgram program = programAsset.SerializedProgramAsset.RetrieveProgram();

                if (program == null ||
                    program.Heap == null ||
                    program.SymbolTable == null)
                    continue;

                long programID = program.Heap.GetHeapVariable<long>(program.SymbolTable.GetAddressFromSymbol(programAsset.behaviourIDHeapVarName));

                if (scriptLookup.ContainsKey(programID))
                    continue;

                scriptLookup.Add(programID, (AssetDatabase.GetAssetPath(programAsset.sourceCsScript), programAsset));
            }

            return true;
        }
        
        static void CleanupLogWatcher()
        {
            if (logDirectoryWatcher != null)
            {
                logDirectoryWatcher.EnableRaisingEvents = false;
                logDirectoryWatcher.Changed -= OnLogFileChanged;
                logDirectoryWatcher.Dispose();
                logDirectoryWatcher = null;
            }

            EditorApplication.update -= OnEditorUpdate;
            Application.logMessageReceived -= OnLog;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupLogWatcher;
        }

        static void OnLogFileChanged(object source, FileSystemEventArgs args)
        {
            lock (logModifiedLock)
            {
                modifiedLogPaths.Add(args.FullPath);
            }
        }

        static void OnLog(string logStr, string stackTrace, LogType type)
        {
            if (type == LogType.Error)
            {
                debugOutputQueue.Enqueue(logStr);
            }
        }

        static void OnEditorUpdate()
        {
            if (!InitializeScriptLookup())
                return;

            while (debugOutputQueue.Count > 0)
            {
                HandleLogError(debugOutputQueue.Dequeue(), "Udon runtime exception detected!", "");
            }

            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();
            bool shouldListenForVRC = udonSharpSettings != null && udonSharpSettings.buildDebugInfo && udonSharpSettings.listenForVRCExceptions;

            if (logDirectoryWatcher != null)
                logDirectoryWatcher.EnableRaisingEvents = shouldListenForVRC;

            if (shouldListenForVRC)
            {
                List<(string, string)> modifiedFilesAndContents = null;

                lock (logModifiedLock)
                {
                    if (modifiedLogPaths.Count > 0)
                    {
                        modifiedFilesAndContents = new List<(string, string)>();
                        HashSet<string> newLogPaths = new HashSet<string>();

                        foreach (string logPath in modifiedLogPaths)
                        {
                            if (!logFileStates.TryGetValue(logPath, out LogFileState logState))
                                logFileStates.Add(logPath, new LogFileState());

                            logState = logFileStates[logPath];

                            string newLogContent = "";

                            newLogPaths.Add(logPath);

                            try
                            {
                                FileInfo fileInfo = new FileInfo(logPath);

                                using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    using (StreamReader reader = new StreamReader(stream))
                                    {
                                        if (logState.playerName == null)
                                        {
                                            string fullFileContents = reader.ReadToEnd();

                                            const string searchStr = "[VRCFlowManagerVRC] User Authenticated: ";
                                            int userIdx = fullFileContents.IndexOf(searchStr);
                                            if (userIdx != -1)
                                            {
                                                userIdx += searchStr.Length;

                                                int endIdx = userIdx;

                                                while (fullFileContents[endIdx] != '\r' && fullFileContents[endIdx] != '\n') endIdx++;

                                                string username = fullFileContents.Substring(userIdx, endIdx - userIdx);

                                                logState.playerName = username;
                                            }
                                        }

                                        if (logState.lineOffset == -1)
                                        {
                                            reader.BaseStream.Seek(0, SeekOrigin.End);
                                        }
                                        else
                                        {
                                            reader.BaseStream.Seek(logState.lineOffset, SeekOrigin.Begin);
                                        }

                                        newLogContent = reader.ReadToEnd();

                                        logFileStates[logPath].lineOffset = reader.BaseStream.Position;
                                        reader.Close();
                                    }

                                    stream.Close();
                                }

                                newLogPaths.Remove(logPath);

                                if (newLogContent != "")
                                    modifiedFilesAndContents.Add((logPath, newLogContent));
                            }
                            catch (System.IO.IOException)
                            { }
                        }

                        modifiedLogPaths = newLogPaths;
                    }
                }

                if (modifiedFilesAndContents != null)
                {
                    foreach (var modifiedFile in modifiedFilesAndContents)
                    {
                        const string errorMatchStr = "[UdonBehaviour] An exception occurred during Udon execution, this UdonBehaviour will be halted.";

                        int currentErrorIndex = modifiedFile.Item2.IndexOf(errorMatchStr);
                        while (currentErrorIndex != -1)
                        {
                            LogFileState state = logFileStates[modifiedFile.Item1];
                            HandleLogError(modifiedFile.Item2.Substring(currentErrorIndex, modifiedFile.Item2.Length - currentErrorIndex), $"VRChat client runtime Udon exception detected!", $"{ state.playerName ?? "Unknown"}");

                            currentErrorIndex = modifiedFile.Item2.IndexOf(errorMatchStr, currentErrorIndex + errorMatchStr.Length);
                        }
                    }
                }
            }
        }

        static void HandleLogError(string errorStr, string logPrefix, string prePrefix)
        {
            UdonSharpEditorCache.DebugInfoType debugType;
            if (errorStr.StartsWith("[<color=yellow>UdonBehaviour</color>] An exception occurred during Udon execution, this UdonBehaviour will be halted.")) // Editor
            {
                debugType = UdonSharpEditorCache.DebugInfoType.Editor;
            }
            else if (errorStr.StartsWith("[UdonBehaviour] An exception occurred during Udon execution, this UdonBehaviour will be halted.")) // Client
            {
                debugType = UdonSharpEditorCache.DebugInfoType.Client;
            }
            else
            {
                return;
            }

            const string exceptionMessageStr = "Exception Message:";
            const string seperatorStr = "----------------------";
            int errorMessageStart = errorStr.IndexOf(exceptionMessageStr) + exceptionMessageStr.Length;
            if (errorMessageStart == -1)
                return;

            int errorMessageEnd = errorStr.IndexOf(seperatorStr, errorMessageStart);

            if (errorMessageEnd == -1 || errorMessageEnd < errorMessageStart)
                return;

            string errorMessage = errorStr.Substring(errorMessageStart, errorMessageEnd - errorMessageStart).TrimStart('\n', '\r');
            int programCounter;
            long programID;
            string programName;

            try
            {
                Match programCounterMatch = Regex.Match(errorStr, @"Program Counter was at: (?<counter>\d+)");

                programCounter = int.Parse(programCounterMatch.Groups["counter"].Value);
                
                Match programTypeMatch = Regex.Match(errorStr, @"Heap Dump:[\n\r\s]+[\d]x[\d]+: (?<programID>[-]?[\d]+)[\n\r\s]+[\d]x[\d]+: (?<programName>[\w]+)");

                programID = long.Parse(programTypeMatch.Groups["programID"].Value);
                programName = programTypeMatch.Groups["programName"].Value;
            }
            catch (System.Exception)
            {
                return;
            }

            (string, UdonSharpProgramAsset) assetInfo;

            if (!scriptLookup.TryGetValue(programID, out assetInfo))
                return;

            if (assetInfo.Item2 == null)
                return;

            ClassDebugInfo debugInfo = UdonSharpEditorCache.Instance.GetDebugInfo(assetInfo.Item2, debugType);

            // No debug info was built
            if (debugInfo == null)
                return;

            int debugSpanIdx = System.Array.BinarySearch(debugInfo.DebugLineSpans.Select(e => e.endInstruction).ToArray(), programCounter);
            if (debugSpanIdx < 0)
                debugSpanIdx = ~debugSpanIdx;

            debugSpanIdx = Mathf.Clamp(debugSpanIdx, 0, debugInfo.DebugLineSpans.Length - 1);

            ClassDebugInfo.DebugLineSpan debugLineSpan = debugInfo.DebugLineSpans[debugSpanIdx];

            UdonSharpUtils.LogRuntimeError($"{logPrefix}\n{errorMessage}", $"[<color=#575ff2>{prePrefix}</color>]", assetInfo.Item1, debugLineSpan.line, debugLineSpan.lineChar);
        }
    }
}
