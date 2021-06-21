

using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp.Compiler
{
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
