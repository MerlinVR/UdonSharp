
using System.Collections.Generic;
using System.Text;

namespace UdonSharp
{
    public class AssemblyBuilder
    {
        StringBuilder assemblyTextBuilder = new StringBuilder();
        int programCounter = 0;
        
        public AssemblyBuilder()
        {
        }

        public string GetAssemblyStr()
        {
            return assemblyTextBuilder.ToString();
        }

        public void AppendCommentedLine(string line, string comment, int indent = 2)
        {
            // Make sure the comment stays on the same line
            comment.Replace('\n', ' ');
            comment.Replace('\r', ' ');

            assemblyTextBuilder.Append($"{new string(' ', indent * 4)}{line}");
            if (comment.Length > 0)
                assemblyTextBuilder.Append($" #{comment}");
            assemblyTextBuilder.AppendLine();
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
            if (jumpTarget.IsResolved)
            {
                AppendCommentedLine($"JUMP, {jumpTarget.AddresStr()}", comment);
            }
            else
            {
                AppendCommentedLine($"JUMP_LABEL, [{jumpTarget.uniqueName}]", comment);
            }

            programCounter += UdonSharpUtils.GetUdonInstructionSize("JUMP");
        }

        public void AddJumpIfFalse(JumpLabel jumpTarget, string comment = "")
        {
            if (jumpTarget.IsResolved)
            {
                AppendCommentedLine($"JUMP_IF_FALSE, {jumpTarget.AddresStr()}", comment);
            }
            else
            {
                AppendCommentedLine($"JUMP_IF_FALSE_LABEL, [{jumpTarget.uniqueName}]", comment);
            }

            programCounter += UdonSharpUtils.GetUdonInstructionSize("JUMP_IF_FALSE");
        }

        public void AddExternCall(string externCall, string comment = "")
        {
            AppendCommentedLine($"EXTERN, \"{externCall}\"", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("EXTERN");
        }

        public void AddJumpIndirect(SymbolDefinition addressSymbol, string comment = "")
        {
            throw new System.NotImplementedException("Jump indirect instruction is not implemented in UdonSharp yet."); // I'm not sure why JUMP_INDIRECT both takes an address and pops an address from the stack
        }

        public void AddJumpLabel(JumpLabel jumpLabel, string comment = "")
        {
            if (jumpLabel.IsResolved)
                throw new System.Exception($"Target jump label {jumpLabel.uniqueName} has already been used!");

            jumpLabel.resolvedAddress = (uint)programCounter;
            AppendCommentedLine("NOP", comment);
            programCounter += UdonSharpUtils.GetUdonInstructionSize("NOP");
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
            AddCopy();
        }
    }

}
