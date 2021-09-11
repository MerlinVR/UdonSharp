
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;

namespace UdonSharp.Compiler
{
    [System.Serializable]
    public struct MethodDebugMarker
    {
        public int startInstruction;

        public int startLine;
        public int startChar;
    }

    [System.Serializable]
    public class MethodDebugInfo
    {
        public string methodName;
        public string containingFilePath;
        public uint methodStartAddress;
        public uint methodEndAddress;
        public List<MethodDebugMarker> debugMarkers;
    }
    
    [System.Serializable]
    public class AssemblyDebugInfo
    {
        [OdinSerialize]
        private List<MethodDebugInfo> _methodDebugInfos;

        [OdinSerialize]
        private Dictionary<uint, MethodDebugInfo> _callSiteLookup = new Dictionary<uint, MethodDebugInfo>();

        private MethodSymbol _currentMethod;
        private MethodDebugInfo _currentDebugInfo;

        internal void StartMethodEmit(MethodSymbol methodSymbol, EmitContext context)
        {
            _currentMethod = methodSymbol;
            _currentDebugInfo = new MethodDebugInfo();
            _currentDebugInfo.methodName = methodSymbol.Name;
            _currentDebugInfo.containingFilePath = context.CompileContext
                .TranslateLocationToFileName(methodSymbol.RoslynSymbol
                .DeclaringSyntaxReferences.First().GetSyntax().GetLocation());

            _currentDebugInfo.methodStartAddress = context.Module.CurrentAddress;
        }

        internal void FinalizeMethodEmit(EmitContext context)
        {
            _currentDebugInfo.methodEndAddress = context.Module.CurrentAddress;
            
            if (_methodDebugInfos == null)
                _methodDebugInfos = new List<MethodDebugInfo>();
            
            _methodDebugInfos.Add(_currentDebugInfo);
            _currentMethod = null;
            _currentDebugInfo = null;
            _currentSpan = null;
        }

        private MethodDebugMarker? _currentSpan;
        
        internal void UpdateSyntaxNode(SyntaxNode node, EmitContext context)
        {
            var nodeLocation = node.GetLocation().GetLineSpan().EndLinePosition;

            MethodDebugMarker currentMarker = _currentSpan ?? new MethodDebugMarker() { startInstruction = -1 };

            int currentAddress = (int)context.Module.CurrentAddress;

            if (currentMarker.startInstruction == -1)
            {
                currentMarker.startInstruction = currentAddress;
                currentMarker.startLine = nodeLocation.Line;
                currentMarker.startChar = nodeLocation.Character;

                _currentSpan = currentMarker;
            }
            else if (currentMarker.startLine != nodeLocation.Line ||
                     currentMarker.startChar != nodeLocation.Character)
            {
                if (_currentDebugInfo.debugMarkers == null)
                    _currentDebugInfo.debugMarkers = new List<MethodDebugMarker>();
                
                _currentDebugInfo.debugMarkers.Add(currentMarker);
                _currentSpan = null;
            }
        }

        internal void AddCallSite(uint callsite)
        {
            _callSiteLookup.Add(callsite, _currentDebugInfo);
        }
        
        internal void FinalizeAssemblyInfo()
        {
            
        }
        
        /// <summary>
        /// Gets the debug line span from a given program counter
        /// </summary>
        [PublicAPI]
        public void GetPositionFromProgramCounter(int programCounter, out string sourceFilePath, out string methodName, out int line, out int character)
        {
            sourceFilePath = null;
            methodName = null;
            line = 0;
            character = 0;
            
            if (_methodDebugInfos == null) return;

            foreach (MethodDebugInfo debugInfo in _methodDebugInfos)
            {
                if (programCounter >= debugInfo.methodStartAddress && programCounter < debugInfo.methodEndAddress)
                {
                    sourceFilePath = debugInfo.containingFilePath;
                    methodName = debugInfo.methodName;

                    if (debugInfo.debugMarkers == null || debugInfo.debugMarkers.Count == 0) return;

                    if (programCounter >= debugInfo.debugMarkers.Last().startInstruction)
                    {
                        line = debugInfo.debugMarkers.Last().startLine;
                        character = debugInfo.debugMarkers.Last().startChar;
                        return;
                    }

                    for (int i = 0; i < debugInfo.debugMarkers.Count - 1; ++i)
                    {
                        MethodDebugMarker currentMarker = debugInfo.debugMarkers[i];
                        MethodDebugMarker nextMarker = debugInfo.debugMarkers[i + 1];

                        if (programCounter >= currentMarker.startInstruction &&
                            programCounter < nextMarker.startInstruction)
                        {
                            line = currentMarker.startLine;
                            character = currentMarker.startChar;
                            return;
                        }
                    }

                    line = debugInfo.debugMarkers.Last().startLine;
                    character = debugInfo.debugMarkers.Last().startChar;
                }
            }
        }
    }
    
    [System.Serializable]
    public class ClassDebugInfo
    {
        [System.Serializable]
        public class DebugLineSpan
        {
            public int startInstruction = 0;
            public int endInstruction = 0;

            public int startSourceChar = 0;
            public int endSourceChar = 0;

            public int line = 0;
            public int lineChar = 0;

            //public string spanCodeSection = "";
        }

        [UnityEngine.SerializeField]
        private DebugLineSpan[] serializedDebugSpans;

        public DebugLineSpan[] DebugLineSpans { get { return serializedDebugSpans; } }

        // Purposefully won't be serialized
        public AssemblyBuilder assemblyBuilder { get; set; }
        private string sourceText;
        private int mostRecentSpanStart;
        private int lastLineStart;
        private List<DebugLineSpan> debugSpans;
        private bool includeInlineCode;

        internal ClassDebugInfo(string source, bool includeInlineCodeIn)
        {
            sourceText = source;
            mostRecentSpanStart = 0;
            debugSpans = new List<DebugLineSpan>();
            includeInlineCode = includeInlineCodeIn;
        }

        internal void UpdateSyntaxNode(SyntaxNode node)
        {
            if (debugSpans.Count == 0)
                debugSpans.Add(new DebugLineSpan());

            int nodeSpanStart = node.SpanStart;

            if (nodeSpanStart < mostRecentSpanStart || nodeSpanStart >= sourceText.Length)
                return;

            mostRecentSpanStart = nodeSpanStart;

            DebugLineSpan lastLineSpan = debugSpans.Last();

            lastLineSpan.endInstruction = assemblyBuilder.programCounter - 1;
            lastLineSpan.endSourceChar = nodeSpanStart;
            //lastLineSpan.spanCodeSection = sourceText.Substring(lastLineSpan.startSourceChar, lastLineSpan.endSourceChar - lastLineSpan.startSourceChar);

            DebugLineSpan nextLineSpan = new DebugLineSpan();
            nextLineSpan.startInstruction = assemblyBuilder.programCounter;
            nextLineSpan.startSourceChar = nodeSpanStart;

            debugSpans.Add(nextLineSpan);

            if (includeInlineCode)
            {
                int lineStart = nextLineSpan.startSourceChar;

                for (; lineStart > 0 && sourceText[lineStart] != '\n' && sourceText[lineStart] != '\r'; --lineStart) { }

                if (lineStart >= lastLineStart - 1)
                {
                    List<string> spanCodeLines = new List<string>();

                    for (int idx = nextLineSpan.startSourceChar; idx < sourceText.Length; ++idx)
                    {
                        if (sourceText[idx] == '\n' || sourceText[idx] == '\r')
                        {
                            spanCodeLines.Add(sourceText.Substring(lineStart, idx - lineStart).Trim(' ', '\n', '\r'));

                            while (sourceText[idx] == '\n' || sourceText[idx] == '\r')
                                ++idx;

                            lastLineStart = idx;
                            break;
                        }
                    }

                    foreach (string spanCodeLine in spanCodeLines)
                    {
                        assemblyBuilder.AppendCommentedLine("", "");
                        assemblyBuilder.AppendCommentedLine("", $" {spanCodeLine}");
                    }
                }
            }
        }

        internal void FinalizeDebugInfo()
        {
            serializedDebugSpans = new DebugLineSpan[debugSpans.Count];

            int lastStart = 0;
            int lastLineCount = 0;
            int lastCharCount = 0;
            for (int i = 0; i < serializedDebugSpans.Length; ++i)
            {
                serializedDebugSpans[i] = debugSpans[i];
                DebugLineSpan span = serializedDebugSpans[i];
                if (span.endInstruction <= 0 && span.startInstruction > 0)
                    span.endInstruction = span.startInstruction;

                if (span.endSourceChar == 0 && span.startSourceChar > 0)
                    span.endSourceChar = span.startSourceChar;

                int lineCount = lastLineCount;
                int lineChar = lastCharCount;
                for (int j = lastStart; j < serializedDebugSpans[i].startSourceChar; ++j)
                {
                    ++lineChar;
                    if (sourceText[j] == '\n')
                    {
                        ++lineCount;
                        lineChar = 0;
                    }
                }

                lastCharCount = lineChar;
                lastLineCount = lineCount;
                lastStart = span.startSourceChar;

                serializedDebugSpans[i].line = lineCount;
                serializedDebugSpans[i].lineChar = lineChar;
            }
        }

        /// <summary>
        /// Gets the debug line span from a given program counter
        /// </summary>
        /// <param name="programCounter"></param>
        /// <returns></returns>
        [PublicAPI]
        public DebugLineSpan GetLineFromProgramCounter(int programCounter)
        {
            int debugSpanIdx = System.Array.BinarySearch(DebugLineSpans.Select(e => e.endInstruction).ToArray(), programCounter);
            if (debugSpanIdx < 0)
                debugSpanIdx = ~debugSpanIdx;

            debugSpanIdx = UnityEngine.Mathf.Clamp(debugSpanIdx, 0, DebugLineSpans.Length - 1);

            ClassDebugInfo.DebugLineSpan debugLineSpan = DebugLineSpans[debugSpanIdx];

            return debugLineSpan;
        }
    }
}
