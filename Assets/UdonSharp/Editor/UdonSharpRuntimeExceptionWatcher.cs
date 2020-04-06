
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    [InitializeOnLoad]
    public static class RuntimeExceptionWatcher
    {
        static Queue<string> debugOutputQueue;
        static Dictionary<long, (string, ClassDebugInfo)> scriptLookup;
        
        // Log watcher vars
        static FileSystemWatcher logDirectoryWatcher;
        static object logModifiedLock;
        static Dictionary<string, long> lastLogOffsets;
        static HashSet<string> modifiedLogPaths;

        static RuntimeExceptionWatcher()
        {
            debugOutputQueue = new Queue<string>();

            logModifiedLock = new object();
            lastLogOffsets = new Dictionary<string, long>();
            modifiedLogPaths = new HashSet<string>();

            Application.logMessageReceived += OnLog;
            EditorApplication.update += OnEditorUpdate;
            
            // Now setup the filesystem watcher
            string[] splitPath = Application.persistentDataPath.Split('/', '\\');
            string VRCDataPath = string.Join("\\", splitPath.Take(splitPath.Length - 2)) + "\\VRChat\\VRChat";

            AssemblyReloadEvents.beforeAssemblyReload += CleanupLogWatcher;
            logDirectoryWatcher = new FileSystemWatcher(VRCDataPath, "output_log_*.txt");
            logDirectoryWatcher.IncludeSubdirectories = false;
            logDirectoryWatcher.NotifyFilter = NotifyFilters.LastWrite;
            logDirectoryWatcher.Changed += OnLogFileChanged;
            logDirectoryWatcher.EnableRaisingEvents = false;
        }

        static bool InitializeScriptLookup()
        {
            if (scriptLookup != null)
                return true;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;

            scriptLookup = new Dictionary<long, (string, ClassDebugInfo)>();
            string[] udonSharpDataAssets = AssetDatabase.FindAssets($"t:{typeof(UdonSharpProgramAsset).Name}");

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

                scriptLookup.Add(programID, (AssetDatabase.GetAssetPath(programAsset.sourceCsScript), programAsset.debugInfo));
            }

            return true;
        }
        
        static void CleanupLogWatcher()
        {
            logDirectoryWatcher.Dispose();
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
                HandleLogError(debugOutputQueue.Dequeue(), "Udon runtime exception detected!");
            }

            UdonSharpSettings udonSharpSettings = UdonSharpSettings.GetSettings();
            bool shouldListenForVRC = udonSharpSettings != null && udonSharpSettings.buildDebugInfo && udonSharpSettings.listenForVRCExceptions;

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
                            long lastFileOffset;
                            if (!lastLogOffsets.TryGetValue(logPath, out lastFileOffset))
                                lastLogOffsets.Add(logPath, -1);

                            lastFileOffset = lastLogOffsets[logPath];

                            string newLogContent = "";

                            newLogPaths.Add(logPath);

                            try
                            {
                                FileInfo fileInfo = new FileInfo(logPath);

                                using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    using (StreamReader reader = new StreamReader(stream))
                                    {
                                        if (lastFileOffset == -1)
                                        {
                                            reader.BaseStream.Seek(0, SeekOrigin.End);
                                        }
                                        else
                                        {
                                            reader.BaseStream.Seek(lastFileOffset, SeekOrigin.Begin);
                                        }

                                        newLogContent = reader.ReadToEnd();

                                        lastLogOffsets[logPath] = reader.BaseStream.Position;
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
                            HandleLogError(modifiedFile.Item2.Substring(currentErrorIndex, modifiedFile.Item2.Length - currentErrorIndex), $"VRChat client runtime Udon exception detected! Source log file: {Path.GetFileName(modifiedFile.Item1)}");

                            currentErrorIndex = modifiedFile.Item2.IndexOf(errorMatchStr, currentErrorIndex + errorMatchStr.Length);
                        }
                    }
                }
            }
        }

        static void HandleLogError(string errorStr, string logPrefix)
        {
            if (!errorStr.StartsWith("[<color=yellow>UdonBehaviour</color>] An exception occurred during Udon execution, this UdonBehaviour will be halted.") && // Editor
                !errorStr.StartsWith("[UdonBehaviour] An exception occurred during Udon execution, this UdonBehaviour will be halted.")) // Client
                return;

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

            (string, ClassDebugInfo) assetInfo;

            if (!scriptLookup.TryGetValue(programID, out assetInfo))
                return;

            // No debug info was built
            if (assetInfo.Item2 == null)
                return;

            int debugSpanIdx = System.Array.BinarySearch(assetInfo.Item2.DebugLineSpans.Select(e => e.endInstruction).ToArray(), programCounter);
            if (debugSpanIdx < 0)
                debugSpanIdx = ~debugSpanIdx;

            debugSpanIdx = Mathf.Clamp(debugSpanIdx, 0, assetInfo.Item2.DebugLineSpans.Length - 1);

            ClassDebugInfo.DebugLineSpan debugLineSpan = assetInfo.Item2.DebugLineSpans[debugSpanIdx];

            UdonSharpUtils.LogBuildError($"{logPrefix}\n{errorMessage}", assetInfo.Item1, debugLineSpan.line, debugLineSpan.lineChar);
        }
    }
}
