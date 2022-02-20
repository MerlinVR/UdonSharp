
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace UdonSharpEditor
{
    public static class RuntimeLogWatcher
    {
        private class LogFileState
        {
            public string playerName;
            public long lineOffset = -1;
            public string nameColor = "0000ff";
        }

        private static Queue<string> _debugOutputQueue = new Queue<string>();
        private static Dictionary<long, (string, UdonSharpProgramAsset)> _scriptLookup;
        
        // Log watcher vars
        private static FileSystemWatcher _logDirectoryWatcher;
        private static object _logModifiedLock = new object();
        private static Dictionary<string, LogFileState> _logFileStates = new Dictionary<string, LogFileState>();
        private static HashSet<string> _modifiedLogPaths = new HashSet<string>();

        public static void InitLogWatcher()
        {
            EditorApplication.update += OnEditorUpdate;
            Application.logMessageReceived += OnLog;
        }

        private static bool ShouldListenForVRC()
        {
            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();

            return udonSharpSettings.listenForVRCExceptions || udonSharpSettings.watcherMode != UdonSharpSettings.LogWatcherMode.Disabled;
        }

        private static bool InitializeScriptLookup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;
            
            if (_logDirectoryWatcher == null && ShouldListenForVRC())
            {
                AssemblyReloadEvents.beforeAssemblyReload += CleanupLogWatcher;

                // Now setup the filesystem watcher
                string[] splitPath = Application.persistentDataPath.Split('/', '\\');
                string VRCDataPath = string.Join("\\", splitPath.Take(splitPath.Length - 2)) + "\\VRChat\\VRChat";

                if (Directory.Exists(VRCDataPath))
                {
                    _logDirectoryWatcher = new FileSystemWatcher(VRCDataPath, "output_log_*.txt");
                    _logDirectoryWatcher.IncludeSubdirectories = false;
                    _logDirectoryWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    _logDirectoryWatcher.Changed += OnLogFileChanged;
                    _logDirectoryWatcher.InternalBufferSize = 1024;
                    _logDirectoryWatcher.EnableRaisingEvents = false;
                }
                else
                {
                    Debug.LogError("[UdonSharp] Could not locate VRChat data directory for exception watcher");
                }
            }

            if (_scriptLookup != null)
                return true;

            _scriptLookup = new Dictionary<long, (string, UdonSharpProgramAsset)>();
            UdonSharpProgramAsset[] udonSharpDataAssets = UdonSharpProgramAsset.GetAllUdonSharpPrograms();

            foreach (UdonSharpProgramAsset programAsset in udonSharpDataAssets)
            {
                if (programAsset.sourceCsScript == null)
                    continue;

                if (programAsset.GetSerializedProgramAssetWithoutRefresh() == null)
                    continue;

                long programID = programAsset.scriptID;

                if (programID == 0)
                    continue;

                if (_scriptLookup.ContainsKey(programID))
                    continue;

                _scriptLookup.Add(programID, (AssetDatabase.GetAssetPath(programAsset.sourceCsScript), programAsset));
            }

            return true;
        }

        private static void CleanupLogWatcher()
        {
            if (_logDirectoryWatcher != null)
            {
                _logDirectoryWatcher.EnableRaisingEvents = false;
                _logDirectoryWatcher.Changed -= OnLogFileChanged;
                _logDirectoryWatcher.Dispose();
                _logDirectoryWatcher = null;
            }

            EditorApplication.update -= OnEditorUpdate;
            Application.logMessageReceived -= OnLog;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupLogWatcher;
        }

        private static void OnLogFileChanged(object source, FileSystemEventArgs args)
        {
            lock (_logModifiedLock)
            {
                _modifiedLogPaths.Add(args.FullPath);
            }
        }

        private static void OnLog(string logStr, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                _debugOutputQueue.Enqueue(logStr);
            }
        }

        private const string MATCH_STR = "\\n\\n\\r\\n\\d{4}.\\d{2}.\\d{2} \\d{2}:\\d{2}:\\d{2} ";
        private static Regex _lineMatch;

        private static void OnEditorUpdate()
        {
            if (!InitializeScriptLookup())
                return;

            while (_debugOutputQueue.Count > 0)
            {
                HandleLogError(_debugOutputQueue.Dequeue(), "Udon runtime exception detected!", null);
            }

            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();
            bool shouldListenForVRC = ShouldListenForVRC();

            if (_logDirectoryWatcher != null)
                _logDirectoryWatcher.EnableRaisingEvents = shouldListenForVRC;

            if (!shouldListenForVRC) 
                return;
            
            if (_lineMatch == null)
                _lineMatch = new Regex(MATCH_STR, RegexOptions.Compiled);

            List<(string, string)> modifiedFilesAndContents = null;

            lock (_logModifiedLock)
            {
                if (_modifiedLogPaths.Count > 0)
                {
                    modifiedFilesAndContents = new List<(string, string)>();
                    HashSet<string> newLogPaths = new HashSet<string>();

                    foreach (string logPath in _modifiedLogPaths)
                    {
                        if (!_logFileStates.TryGetValue(logPath, out LogFileState logState))
                            _logFileStates.Add(logPath, new LogFileState());

                        logState = _logFileStates[logPath];

                        newLogPaths.Add(logPath);

                        try
                        {
                            FileInfo fileInfo = new FileInfo(logPath);

                            string newLogContent;
                            
                            using (FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    if (logState.playerName == null) // Search for the player name that this log belongs to
                                    {
                                        string fullFileContents = reader.ReadToEnd();

                                        const string searchStr = "[Behaviour] User Authenticated: ";
                                        int userIdx = fullFileContents.IndexOf(searchStr, StringComparison.Ordinal);
                                        if (userIdx != -1)
                                        {
                                            userIdx += searchStr.Length;

                                            int endIdx = userIdx;

                                            while (fullFileContents[endIdx] != '\r' && fullFileContents[endIdx] != '\n') endIdx++; // Seek to end of name

                                            string username = fullFileContents.Substring(userIdx, endIdx - userIdx);

                                            logState.playerName = username;

                                            // Use the log path as well since Build & Test can have multiple of the same display named users
                                            Random random = new Random((username + logPath).GetHashCode());

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

                                    _logFileStates[logPath].lineOffset = reader.BaseStream.Position;
                                    reader.Close();
                                }

                                stream.Close();
                            }

                            newLogPaths.Remove(logPath);

                            if (newLogContent != "")
                                modifiedFilesAndContents.Add((logPath, newLogContent));
                        }
                        catch (IOException)
                        { }
                    }

                    _modifiedLogPaths = newLogPaths;
                }
            }

            if (modifiedFilesAndContents == null) 
                return;
            
            foreach ((string filePath, string contents) in modifiedFilesAndContents)
            {
                LogFileState state = _logFileStates[filePath];

                // Log forwarding
                if (udonSharpSettings.watcherMode != UdonSharpSettings.LogWatcherMode.Disabled)
                {
                    Match match = null;

                    do
                    {
                        int currentIdx = (match?.Index ?? -1);

                        match = _lineMatch.Match(contents, currentIdx + 1);

                        string logStr = null;

                        if (currentIdx == -1)
                        {
                            if (match.Success)
                            {
                                Match nextMatch = _lineMatch.Match(contents, match.Index + 1);

                                if (nextMatch.Success)
                                    logStr = contents.Substring(0, nextMatch.Index);
                                else
                                    logStr = contents;

                                match = nextMatch;
                            }
                        }
                        else if (match.Success)
                        {
                            logStr = contents.Substring(currentIdx < 0 ? 0 : currentIdx, match.Index - currentIdx);
                        }
                        else if (currentIdx != -1)
                        {
                            logStr = contents.Substring(currentIdx < 0 ? 0 : currentIdx, contents.Length - currentIdx);
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

                    int currentErrorIndex = contents.IndexOf(errorMatchStr, StringComparison.Ordinal);
                    while (currentErrorIndex != -1)
                    {
                        HandleLogError(contents.Substring(currentErrorIndex, contents.Length - currentErrorIndex), $"VRChat client runtime Udon exception detected!", $"{ state.playerName ?? "Unknown"}");

                        currentErrorIndex = contents.IndexOf(errorMatchStr, currentErrorIndex + errorMatchStr.Length, StringComparison.Ordinal);
                    }
                }
            }
        }

        // Common messages that can spam the log and have no use for debugging
        private static readonly string[] _filteredPrefixes = {
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

        private static void HandleForwardedLog(string logMessage, LogFileState state, UdonSharpSettings settings)
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

            foreach (string filteredPrefix in _filteredPrefixes)
            {
                if (trimmedMessage.StartsWith(filteredPrefix))
                    return;
            }

            string playerName = state.playerName ?? "Unknown";

            if (trimmedStr.StartsWith("Log"))
                Debug.Log($"[<color=#{state.nameColor}>{playerName}</color>]{message}");
            else if (trimmedStr.StartsWith("Warning"))
                Debug.LogWarning($"[<color=#{state.nameColor}>{playerName}</color>]{message}");
            else if (trimmedStr.StartsWith("Error"))
                Debug.LogError($"[<color=#{state.nameColor}>{playerName}</color>]{message}");
        }

        private static void HandleLogError(string errorStr, string logPrefix, string prePrefix)
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
            const string separatorStr = "----------------------";
            int errorMessageStart = errorStr.IndexOf(exceptionMessageStr, StringComparison.Ordinal) + exceptionMessageStr.Length;
            if (errorMessageStart == -1)
                return;

            int errorMessageEnd = errorStr.IndexOf(separatorStr, errorMessageStart, StringComparison.Ordinal);

            if (errorMessageEnd == -1 || errorMessageEnd < errorMessageStart)
            {
                if (debugType == UdonSharpEditorCache.DebugInfoType.Client)
                {
                    errorMessageEnd = errorStr.IndexOf("\n\n\r\n", StringComparison.Ordinal);

                    if (errorMessageEnd != -1)
                        errorStr = errorStr.Substring(0, errorMessageEnd);

                    Debug.LogError($"{(prePrefix != null ? $"[<color=#575ff2>{prePrefix}</color>]" : "")} Runtime error detected, but the client has not been launched with '--enable-udon-debug-logging' so the error cannot be traced. Add the argument to your client startup and try again. \n{errorStr}");
                }

                return;
            }

            string errorMessage = errorStr.Substring(errorMessageStart, errorMessageEnd - errorMessageStart).TrimStart('\n', '\r');
            int programCounter;
            long programID;
            // string programName;

            try
            {
                Match programCounterMatch = Regex.Match(errorStr, @"Program Counter was at: (?<counter>\d+)");

                programCounter = int.Parse(programCounterMatch.Groups["counter"].Value);
                
                Match programTypeMatch = Regex.Match(errorStr, @"Heap Dump:[\n\r\s]+[\d]x[\d]+: (?<programID>[-]?[\d]+)[\n\r\s]+[\d]x[\d]+: (?<programName>[\w]+)");

                programID = long.Parse(programTypeMatch.Groups["programID"].Value);
                // programName = programTypeMatch.Groups["programName"].Value;
            }
            catch (Exception)
            {
                return;
            }

            if (!_scriptLookup.TryGetValue(programID, out var assetInfo))
                return;

            if (assetInfo.Item2 == null)
                return;

            AssemblyDebugInfo debugInfo = UdonSharpEditorCache.Instance.GetDebugInfo(assetInfo.Item2, debugType);

            // No debug info was built
            if (debugInfo == null)
                return;

            debugInfo.GetPositionFromProgramCounter(programCounter, out string filePath, out string methodName, out int line, out int lineChar);

            UdonSharpUtils.LogRuntimeError($"{logPrefix}\n{errorMessage}", prePrefix != null ? $"[<color=#575ff2>{prePrefix}</color>]" : "", filePath, line, lineChar + 1);
        }
    }
}
