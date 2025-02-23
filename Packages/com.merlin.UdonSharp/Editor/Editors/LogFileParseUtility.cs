
using System;
using System.IO;
using JetBrains.Annotations;
using UdonSharp;
using UnityEditor;
using UnityEngine;

namespace UdonSharpEditor
{
    public static class LogFileParseUtility
    {
        [MenuItem("VRChat SDK/Udon Sharp/Parse Logs from File")]
        private static void ParseLogsFromFileMenuItem()
        {
            string filePath = EditorUtility.OpenFilePanel("Parse VRChat Log File", "", "txt");
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            
            ParseLogsFromFile(filePath);
        }

        [PublicAPI]
        public static void ParseLogsFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                UdonSharpUtils.LogError("Empty or Invalid path was provided for log file.");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(filePath);

                string contents;

                using (FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        reader.BaseStream.Position = 0;

                        contents = reader.ReadToEnd();

                        reader.Close();
                    }

                    stream.Close();
                }

                string username = null;
                // Search for the player name that this log belongs to
                const string searchStr = "User Authenticated: ";
                int userIdx = contents.IndexOf(searchStr, StringComparison.Ordinal);
                if (userIdx != -1)
                {
                    userIdx += searchStr.Length;

                    int endIdx = contents.IndexOf(" (", userIdx, StringComparison.Ordinal);

                    username = contents.Substring(userIdx, endIdx - userIdx);
                }
                
                RuntimeLogWatcher.ParseLogText(contents, username);
            }
            catch (IOException e)
            {
                UdonSharpUtils.LogError("An exception occured when trying to access the provided log file: " + e.Message);
            }
        }
    }
}
