
//#define USE_UDON_LABELS

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UdonSharp.Compiler
{
    public class AssemblyBuilder
    {
        StringBuilder assemblyTextBuilder = new StringBuilder();
        public int programCounter { get; private set; } = 0;

        private HashSet<string> externStringSet = new HashSet<string>();

        public AssemblyBuilder()
        {
        }

        public void ResetProgramCounter()
        {
            programCounter = 0;
        }

        // Resolve any labels that didn't have addresses at the time of creation
        public string GetAssemblyStr(LabelTable labelTable = null)
        {
            if (labelTable == null)
                return assemblyTextBuilder.ToString();

            string assemblyString = assemblyTextBuilder.ToString();

#if !USE_UDON_LABELS
            assemblyString = ReplaceLabels(assemblyString, labelTable);
#endif

            return assemblyString;
        }

#if !USE_UDON_LABELS
        private static readonly char[] _trimChars = new[] {' ', '\n', '\r'};
        
        private string ReplaceLabels(string assemblyString, LabelTable labelTable)
        {
            StringBuilder newAssemblyBuilder = new StringBuilder();
            
            using (StringReader reader = new StringReader(assemblyString))
            {
                string currentLine = reader.ReadLine();

                while (currentLine != null)
                {
                    string line = currentLine.TrimStart(_trimChars);
                    if (line.StartsWith("JUMP_LABEL,", StringComparison.Ordinal))
                    {
                        int startIdx = line.IndexOf('[') + 1;
                        int endIdx = line.IndexOf(']');
                        string labelName = line.Substring(startIdx, endIdx - startIdx);
                        JumpLabel label = labelTable.GetLabel(labelName);
                        newAssemblyBuilder.AppendFormat("        JUMP, {0}\n", label.AddresStr());
                    }
                    else if (line.StartsWith("JUMP_IF_FALSE_LABEL,", StringComparison.Ordinal))
                    {
                        int startIdx = line.IndexOf('[') + 1;
                        int endIdx = line.IndexOf(']');
                        string labelName = line.Substring(startIdx, endIdx - startIdx);
                        JumpLabel label = labelTable.GetLabel(labelName);
                        newAssemblyBuilder.AppendFormat("        JUMP_IF_FALSE, {0}\n", label.AddresStr());
                    }
                    else
                    {
                        newAssemblyBuilder.Append(currentLine);
                        newAssemblyBuilder.Append("\n");
                    }

                    currentLine = reader.ReadLine();
                }
            }

            return newAssemblyBuilder.ToString();
        }
#endif

        public int GetExternStrCount()
        {
            return externStringSet.Count;
        }

        public void AppendCommentedLine(string line, string comment, int indent = 2)
        {
            //if (programCounter > 0)
            //    assemblyTextBuilder.AppendFormat("        # {0:X8}\n", programCounter);

            assemblyTextBuilder.Append($"{new string(' ', indent * 4)}{line}");
            if (comment.Length > 0)
                assemblyTextBuilder.Append($" # {comment}");

            assemblyTextBuilder.Append("\n"); // Keep consistent between Linux/Windows
        }

        public void AppendLine(string line, int indent = 2)
        {
            AppendCommentedLine(line, "", indent);
        }

        public void AddNop(string comment = "")
        {
            AppendCommentedLine("NOP", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("NOP");
        }

        public void AddPush(SymbolDefinition heapAddressSymbol, string comment = "")
        {
            AddPush(heapAddressSymbol.symbolUniqueName, comment);
        }

        private void AddPush(string heapAddress, string comment)
        {
            AppendCommentedLine($"PUSH, {heapAddress}", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("PUSH");
        }

        public void AddPop(string comment = "")
        {
            AppendCommentedLine("POP", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("POP");
        }

        public void AddJump(JumpLabel jumpTarget, string comment = "")
        {
#if USE_UDON_LABELS
            AppendCommentedLine($"JUMP, {jumpTarget.uniqueName}", comment);
#else
            if (jumpTarget.IsResolved)
            {
                AppendCommentedLine($"JUMP, {jumpTarget.AddresStr()}", comment);
            }
            else
            {
                AppendCommentedLine($"JUMP_LABEL, [{jumpTarget.uniqueName}]", comment);
            }
#endif

            programCounter += UdonSharpUtils.GetUdonInstructionSize("JUMP");
        }

        public void AddJumpToExit()
        {
            AppendCommentedLine($"JUMP, 0xFFFFFFFF", "");
            programCounter += UdonSharpUtils.GetUdonInstructionSize("JUMP");
        }

        public void AddJumpIfFalse(JumpLabel jumpTarget, SymbolDefinition conditionSymbol, string comment = "")
        {
            AddPush(conditionSymbol);
            AddJumpIfFalse(jumpTarget, comment);
        }

        public void AddJumpIfFalse(JumpLabel jumpTarget, string comment = "")
        {
#if USE_UDON_LABELS
            AppendCommentedLine($"JUMP_IF_FALSE, {jumpTarget.uniqueName}", comment);
#else
            if (jumpTarget.IsResolved)
            {
                AppendCommentedLine($"JUMP_IF_FALSE, {jumpTarget.AddresStr()}", comment);
            }
            else
            {
                AppendCommentedLine($"JUMP_IF_FALSE_LABEL, [{jumpTarget.uniqueName}]", comment);
            }
#endif

            programCounter += UdonSharpUtils.GetUdonInstructionSize("JUMP_IF_FALSE");
        }

        public void AddExternCall(string externCall, string comment = "")
        {
            externStringSet.Add(externCall);

            AppendCommentedLine($"EXTERN, \"{externCall}\"", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("EXTERN");
        }

        public void AddJumpIndirect(SymbolDefinition addressSymbol, string comment = "")
        {
            AppendCommentedLine($"JUMP_INDIRECT, {addressSymbol.symbolUniqueName}", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("JUMP_INDIRECT");
        }

        public void AddReturnSequence(SymbolDefinition returnTrampolineSymbol, string comment = "")
        {
            AddPush(returnTrampolineSymbol, comment);
            AddCopy();
            AddJumpIndirect(returnTrampolineSymbol);
        }

        public void AddJumpLabel(JumpLabel jumpLabel, string comment = "")
        {
            if (jumpLabel.IsResolved)
                throw new System.Exception($"Target jump label {jumpLabel.uniqueName} has already been used!");

            jumpLabel.resolvedAddress = (uint)programCounter;

#if USE_UDON_LABELS
            AppendCommentedLine($"{jumpLabel.uniqueName}:", comment);
#endif
            //AppendCommentedLine("NOP", comment);
            //programCounter += UdonSharpUtils.GetUdonInstructionSize("NOP");
        }

        public void AddCopy(string comment = "")
        {
            AppendCommentedLine("COPY", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("COPY");
        }

        public void AddCopy(SymbolDefinition target, SymbolDefinition source, string comment = "")
        {
            AddPush(source);
            AddPush(target);
            AddCopy(comment);
        }
    }

}
