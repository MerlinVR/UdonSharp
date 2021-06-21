
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
    public static class RuntimeLogWatcher
    {
        class LogFileState
        {
            public string playerName;
            public long lineOffset = -1;
            public string nameColor = "0000ff";
        }

        static Queue<string> debugOutputQueue = new Queue<string>();
        static Dictionary<long, (string, UdonSharpProgramAsset)> scriptLookup;
        
        // Log watcher vars
        static FileSystemWatcher logDirectoryWatcher;
        static object logModifiedLock = new object();
        static Dictionary<string, LogFileState> logFileStates = new Dictionary<string, LogFileState>();
        static HashSet<string> modifiedLogPaths = new HashSet<string>();

        public static void InitLogWatcher()
        {
            EditorApplication.update += OnEditorUpdate;
            Application.logMessageReceived += OnLog;
        }

        static bool ShouldListenForVRC()
        {
            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();

            if (udonSharpSettings == null)
                return false;

            if (udonSharpSettings.listenForVRCExceptions || udonSharpSettings.watcherMode != UdonSharpSettings.LogWatcherMode.Disabled)
                return true;

            return false;
        }

        static bool InitializeScriptLookup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;
            
            if (logDirectoryWatcher == null && ShouldListenForVRC())
            {
                AssemblyReloadEvents.beforeAssemblyReload += CleanupLogWatcher;

                // Now setup the filesystem watcher
                string[] splitPath = Application.persistentDataPath.Split('/', '\\');
                string VRCDataPath = string.Join("\\", splitPath.Take(splitPath.Length - 2)) + "\\VRChat\\VRChat";

                if (Directory.Exists(VRCDataPath))
                {
                    logDirectoryWatcher = new FileSystemWatcher(VRCDataPath, "output_log_*.txt");
                    logDirectoryWatcher.IncludeSubdirectories = false;
                    logDirectoryWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    logDirectoryWatcher.Changed += OnLogFileChanged;
                    logDirectoryWatcher.InternalBufferSize = 1024;
                    logDirectoryWatcher.EnableRaisingEvents = false;
                }
                else
                {
                    Debug.LogError("[UdonSharp] Could not locate VRChat data directory for exception watcher");
                }
            }

            if (scriptLookup != null)
                return true;

            scriptLookup = new Dictionary<long, (string, UdonSharpProgramAsset)>();
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

            UdonSharpEditorCache editorCache = UdonSharpEditorCache.Instance;

            foreach (string dataGuid in udonSharpDataAssets)
            {
                UdonSharpProgramAsset programAsset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(dataGuid));

                if (programAsset.sourceCsScript == null)
                    continue;

                if (programAsset.GetSerializedProgramAssetWithoutRefresh() == null)
                    continue;

                IUdonProgram program = programAsset.GetSerializedProgramAssetWithoutRefresh().RetrieveProgram();

                if (program == null ||
                    program.Heap == null ||
                    program.SymbolTable == null)
                {
                    //Debug.LogWarning($"Could not load program for '{programAsset}', exceptions for this script will not be handled until scripts have been reloaded");
                    continue;
                }

                long programID;

                if (program.SymbolTable.TryGetAddressFromSymbol(programAsset.behaviourIDHeapVarName, out uint address))
                    programID = program.Heap.GetHeapVariable<long>(address);
                else
                {
                    Debug.LogWarning($"No symbol found for debug info on program asset '{programAsset}', exceptions for this program will not be caught until scripts have been reloaded.");
                    continue;
                }

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
            if (type == LogType.Error || type == LogType.Exception)
            {
                debugOutputQueue.Enqueue(logStr);
            }
        }

        const string MATCH_STR = "\\n\\n\\r\\n\\d{4}.\\d{2}.\\d{2} \\d{2}:\\d{2}:\\d{2} ";
        static Regex lineMatch;

        static void OnEditorUpdate()
        {
            if (!InitializeScriptLookup())
                return;

            while (debugOutputQueue.Count > 0)
            {
                HandleLogError(debugOutputQueue.Dequeue(), "Udon runtime exception detected!", null);
            }

            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();
            bool shouldListenForVRC = udonSharpSettings != null && ShouldListenForVRC();

            if (logDirectoryWatcher != null)
                logDirectoryWatcher.EnableRaisingEvents = shouldListenForVRC;

            if (shouldListenForVRC)
            {
                if (lineMatch == null)
                    lineMatch = new Regex(MATCH_STR, RegexOptions.Compiled);

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
                                        if (logState.playerName == null) // Search for the player name that this log belongs to
                                        {
                                            string fullFileContents = reader.ReadToEnd();

                                            const string SEARCH_STR = "[Behaviour] User Authenticated: ";
                                            int userIdx = fullFileContents.IndexOf(SEARCH_STR);
                                            if (userIdx != -1)
                                            {
                                                userIdx += SEARCH_STR.Length;

                                                int endIdx = userIdx;

                                                while (fullFileContents[endIdx] != '\r' && fullFileContents[endIdx] != '\n') endIdx++; // Seek to end of name

                                                string username = fullFileContents.Substring(userIdx, endIdx - userIdx);

                                                logState.playerName = username;

                                                // Use the log path as well since Build & Test can have multiple of the same display named users
                                                System.Random random = new System.Random((username + logPath).GetHashCode());

                                                Color randomUserColor = Color.HSVToRGB((float)random.NextDouble(), 1.00f, EditorGUIUtility.isProSkin ? 0.9f : 0.6f);
                                                string colorStr = ColorUtility.ToHtmlStringRGB(randomUserColor);

                                                logState.nameColor = colorStr;
                                            }
                                        }

                                        if (logState.lineOffset == -1)
                                        {
                                            reader.BaseStream.Seek(0, SeekOrigin.End);
                                        }
                                        else
                                        {
                                            reader.BaseStream.Seek(logState.lineOffset - 4 < 0 ? 0 : logState.lineOffset - 4, SeekOrigin.Begin); // Subtract 4 characters to pick up the newlines from the prior line for the log forwarding
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
                        LogFileState state = logFileStates[modifiedFile.Item1];

                        // Log forwarding
                        if (udonSharpSettings.watcherMode != UdonSharpSettings.LogWatcherMode.Disabled)
                        {
                            int currentIdx = 0;
                            Match match = null;

                            do
                            {
                                currentIdx = (match?.Index ?? -1);

                                match = lineMatch.Match(modifiedFile.Item2, currentIdx + 1);

                                string logStr = null;

                                if (currentIdx == -1)
                                {
                                    if (match.Success)
                                    {
                                        Match nextMatch = lineMatch.Match(modifiedFile.Item2, match.Index + 1);

                                        if (nextMatch.Success)
                                            logStr = modifiedFile.Item2.Substring(0, nextMatch.Index);
                                        else
                                            logStr = modifiedFile.Item2;

                                        match = nextMatch;
                                    }
                                }
                                else if (match.Success)
                                {
                                    logStr = modifiedFile.Item2.Substring(currentIdx < 0 ? 0 : currentIdx, match.Index - currentIdx);
                                }
                                else if (currentIdx != -1)
                                {
                                    logStr = modifiedFile.Item2.Substring(currentIdx < 0 ? 0 : currentIdx, modifiedFile.Item2.Length - currentIdx);
                                }

                                if (logStr != null)
                                {
                                    logStr = logStr.Trim('\n', '\r');

                                    HandleForwardedLog(logStr, state, udonSharpSettings);
                                }
                            } while (match.Success);
                        }

                        if (udonSharpSettings.listenForVRCExceptions)
                        {
                            // Exception handling
                            const string errorMatchStr = "[UdonBehaviour] An exception occurred during Udon execution, this UdonBehaviour will be halted.";

                            int currentErrorIndex = modifiedFile.Item2.IndexOf(errorMatchStr);
                            while (currentErrorIndex != -1)
                            {
                                HandleLogError(modifiedFile.Item2.Substring(currentErrorIndex, modifiedFile.Item2.Length - currentErrorIndex), $"VRChat client runtime Udon exception detected!", $"{ state.playerName ?? "Unknown"}");

                                currentErrorIndex = modifiedFile.Item2.IndexOf(errorMatchStr, currentErrorIndex + errorMatchStr.Length);
                            }
                        }
                    }
                }
            }
        }

        // Common messages that can spam the log and have no use for debugging
        static readonly string[] filteredPrefixes = new string[]
        {
            "Received Notification: <Notification from username:",
            "Received Message of type: notification content: {{\"id\":\"",
            "Received Message of type: friend-update received at",
            "Received Message of type: friend-active received at",
            "Received Message of type: friend-online received at",
            "Received Message of type: friend-offline received at",
            "Received Message of type: friend-location received at",
            "[VRCFlowNetworkManager] Sending token from provider vrchat",
            "[Always] uSpeak:",
            "Internal: JobTempAlloc has allocations",
            "To Debug, enable the define: TLA_DEBUG_STACK_LEAK in ThreadsafeLinearAllocator.cpp.",
            "PLAYLIST GET id=",
            "Checking server time received at ",
            "[RoomManager] Room metadata is unchanged, skipping update",
            "Setting Custom Properties for Local Player: avatarEyeHeight",
            "HTTPFormUseage:UrlEncoded",
            // Big catch-alls for random irrelevant VRC stuff
            "[API] ",
            "[Behaviour] ",
        };

        static void HandleForwardedLog(string logMessage, LogFileState state, UdonSharpSettings settings)
        {
            const string FMT_STR = "0000.00.00 00:00:00 ";

            string trimmedStr = logMessage.Substring(FMT_STR.Length);

            string message = trimmedStr.Substring(trimmedStr.IndexOf('-') + 2);
            string trimmedMessage = message.TrimStart(' ', '\t');

            if (settings.watcherMode == UdonSharpSettings.LogWatcherMode.Prefix)
            {
                string prefixStr = trimmedMessage;
                bool prefixFound = false;
                foreach (string prefix in settings.logWatcherMatchStrings)
                {
                    if (!string.IsNullOrEmpty(prefix) && prefixStr.StartsWith(prefix))
                    {
                        prefixFound = true;
                        break;
                    }
                }

                if (!prefixFound)
                    return;
            }

            foreach (string filteredPrefix in filteredPrefixes)
            {
                if (trimmedMessage.StartsWith(filteredPrefix))
                    return;
            }

            string playername = state.playerName ?? "Unknown";

            if (trimmedStr.StartsWith("Log"))
                Debug.Log($"[<color=#{state.nameColor}>{playername}</color>]{message}");
            else if (trimmedStr.StartsWith("Warning"))
                Debug.LogWarning($"[<color=#{state.nameColor}>{playername}</color>]{message}");
            else if (trimmedStr.StartsWith("Error"))
                Debug.LogError($"[<color=#{state.nameColor}>{playername}</color>]{message}");
        }

        static void HandleLogError(string errorStr, string logPrefix, string prePrefix)
        {
            if (errorStr.StartsWith("ExecutionEngineException: String conversion error: Illegal byte sequence encounted in the input.")) // Nice typo Mono
            {
                Debug.LogError("ExecutionEngineException detected! This means you have hit a bug in Mono. To fix this, move your project to a path without any unicode characters.");
                return;
            }

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
                return;

            const string exceptionMessageStr = "Exception Message:";
            const string seperatorStr = "----------------------";
            int errorMessageStart = errorStr.IndexOf(exceptionMessageStr) + exceptionMessageStr.Length;
            if (errorMessageStart == -1)
                return;

            int errorMessageEnd = errorStr.IndexOf(seperatorStr, errorMessageStart);

            if (errorMessageEnd == -1 || errorMessageEnd < errorMessageStart)
            {
                if (debugType == UdonSharpEditorCache.DebugInfoType.Client)
                {
                    errorMessageEnd = errorStr.IndexOf("\n\n\r\n");

                    if (errorMessageEnd != -1)
                        errorStr = errorStr.Substring(0, errorMessageEnd);

                    Debug.LogError($"{(prePrefix != null ? $"[<color=#575ff2>{prePrefix}</color>]" : "")} Runtime error detected, but the client has not been launched with '--enable-udon-debug-logging' so the error cannot be traced. Add the argument to your client startup and try again. \n{errorStr}");
                }

                return;
            }

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

            ClassDebugInfo.DebugLineSpan debugLineSpan = debugInfo.GetLineFromProgramCounter(programCounter);

            UdonSharpUtils.LogRuntimeError($"{logPrefix}\n{errorMessage}", prePrefix != null ? $"[<color=#575ff2>{prePrefix}</color>]" : "", assetInfo.Item1, debugLineSpan.line, debugLineSpan.lineChar);
        }
    }
}
