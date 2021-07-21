
using System;
using System.Collections.Generic;
using UdonSharp.Compiler.Assembly.Instructions;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Assembly
{
    /// <summary>
    /// Contains the assembly for a given module of compilation.
    /// A module in this context is all code that will be compiled into a single program asset.
    /// This includes primary events and methods on a given UdonBehaviour, along with all imported methods for referenced types.
    /// </summary>
    internal class AssemblyModule
    {
        private List<AssemblyInstruction> _instructions = new List<AssemblyInstruction>();
        private HashSet<JumpLabel> _jumpLabels = new HashSet<JumpLabel>();
        private uint _currentAddress = 0;

        public AssemblyInstruction this[uint index]
        {
            get
            {
                if (index >= _instructions.Count)
                    throw new IndexOutOfRangeException("Instruction index is not valid");

                return _instructions[(int) index];
            }
        }

        
        
        private void AddInstruction(AssemblyInstruction instruction)
        {
            _instructions.Add(instruction);
            instruction.InstructionAddress = _currentAddress;
            _currentAddress += instruction.Size;
        }

        #region Instruction Adds
        public void AddNop()
        {
            AddInstruction(new NopInstruction());
        }

        public void AddPush(Value pushValue)
        {
            AddInstruction(new PushInstruction(pushValue));
        }

        public void AddPop()
        {
            AddInstruction(new PopInstruction());
        }

        public void AddCopy(Value sourceValue, Value targetValue)
        {
            AddInstruction(new CopyInstruction(sourceValue, targetValue));
        }

        public void AddJump(JumpLabel jumpTarget)
        {
            AddInstruction(new JumpInstruction(jumpTarget));
        }

        public void AddJumpIfFalse(JumpLabel jumpTarget, Value conditionValue)
        {
            AddInstruction(new JumpIfFalseInstruction(jumpTarget, conditionValue));
        }

        public void AddJumpIndrect(Value targetJumpValue)
        {
            AddInstruction(new JumpIndirectInstruction(targetJumpValue));
        }

        public void AddExtern(MethodSymbol method)
        {
            AddInstruction(new ExternInstruction(method));
        }

        public void AddReturn()
        {
            AddInstruction(new RetInstruction());
        }
        #endregion
        
        
    }
}
