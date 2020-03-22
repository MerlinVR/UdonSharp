

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp
{
    [System.Serializable]
    public class ClassDebugInfo
    {
        [System.Serializable]
        public class DebugLineSpan
        {
            public int StartInstruction { get; set; } = 0;
            public int EndInstruction { get; set; } = 0;

            public int StartSourceChar { get; set; } = 0;
            public int EndSourceChar { get; set; } = 0;
        }

        [UnityEngine.SerializeField]
        private DebugLineSpan[] serializedDebugSpans;

        // Purposefully won't be serialized
        public AssemblyBuilder assemblyBuilder { get; set; }
        private string sourceText;
        private int mostRecentSpanStart;
        private int lastLineStart;
        private List<DebugLineSpan> debugSpans;
        private bool includeInlineCode;

        public ClassDebugInfo(string source, bool includeInlineCodeIn)
        {
            sourceText = source;
            mostRecentSpanStart = 0;
            debugSpans = new List<DebugLineSpan>();
            includeInlineCode = includeInlineCodeIn;
        }

        public void UpdateSyntaxNode(SyntaxNode node)
        {
            if (debugSpans.Count == 0)
                debugSpans.Add(new DebugLineSpan());

            int nodeSpanStart = node.Span.Start;

            if (nodeSpanStart < mostRecentSpanStart || nodeSpanStart >= sourceText.Length)
                return;

            mostRecentSpanStart = nodeSpanStart;

            DebugLineSpan lastLineSpan = debugSpans.Last();

            lastLineSpan.EndInstruction = assemblyBuilder.programCounter - 1;
            lastLineSpan.EndSourceChar = node.SpanStart;

            DebugLineSpan nextLineSpan = new DebugLineSpan();
            nextLineSpan.StartInstruction = assemblyBuilder.programCounter;
            nextLineSpan.StartSourceChar = node.SpanStart;

            debugSpans.Add(nextLineSpan);

            if (includeInlineCode)
            {
                int lineStart = nextLineSpan.StartSourceChar;

                for (; lineStart > 0 && sourceText[lineStart] != '\n' && sourceText[lineStart] != '\r'; --lineStart) { }

                if (lineStart >= lastLineStart - 1)
                {
                    List<string> spanCodeLines = new List<string>();

                    for (int idx = nextLineSpan.StartSourceChar; idx < sourceText.Length; ++idx)
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

        public void FinalizeDebugInfo()
        {
            serializedDebugSpans = new DebugLineSpan[debugSpans.Count];

            for (int i = 0; i < serializedDebugSpans.Length; ++i)
            {
                serializedDebugSpans[i] = debugSpans[i];
            }
        }
    }
}
