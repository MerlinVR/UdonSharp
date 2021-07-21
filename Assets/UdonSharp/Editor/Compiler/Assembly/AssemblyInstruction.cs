
using System;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UnityEngine.Assertions;

namespace UdonSharp.Compiler.Assembly
{
    internal enum InstructionKind
    {
        Nop,
        Push,
        Pop,
        Copy,
        Jump,
        JumpIfFalse,
        Extern,
        JumpIndirect,
        Return,
    }

    internal abstract class AssemblyInstruction
    {
        public virtual InstructionKind GetKind() => throw new NotImplementedException();
        public virtual uint Size => throw new NotImplementedException();

        public uint InstructionAddress { get; set; } = UInt32.MaxValue;

        public string Comment { get; set; } = null;
    }

    namespace Instructions
    {
        internal class NopInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Nop;
            public override uint Size => 4;
        }

        internal class PushInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Push;
            public override uint Size => 8;

            public Value PushValue { get; private set; }
            
            public PushInstruction(Value value)
            {
                PushValue = value;
            }
        }

        internal class PopInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Pop;
            public override uint Size => 4;
        }

        internal class CopyInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Copy;
            public override uint Size => 4;

            public Value SourceValue { get; private set; }
            public Value TargetValue { get; private set; }
            
            public CopyInstruction(Value sourceValue, Value targetValue)
            {
                SourceValue = sourceValue;
                TargetValue = targetValue;
            }
        }

        internal class JumpInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Jump;
            public override uint Size => 8;
            
            public JumpLabel JumpTarget { get; private set; }

            public JumpInstruction(JumpLabel jumpTarget)
            {
                JumpTarget = jumpTarget;
            }
        }

        internal class JumpIfFalseInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.JumpIfFalse;
            public override uint Size => 8;
            
            public JumpLabel JumpTarget { get; private set; }
            public Value ConditionValue { get; private set; }

            public JumpIfFalseInstruction(JumpLabel jumpTarget, Value conditionValue)
            {
                JumpTarget = jumpTarget;
                ConditionValue = conditionValue;
            }
        }

        internal class JumpIndirectInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.JumpIndirect;
            public override uint Size => 8;
            
            public Value JumpTargetValue { get; private set; }

            public JumpIndirectInstruction(Value jumpTargetValue)
            {
                JumpTargetValue = jumpTargetValue;
            }
        }

        internal class ExternInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Extern;
            public override uint Size => 8;
            
            public MethodSymbol Method { get; private set; }

            public ExternInstruction(MethodSymbol method)
            {
                #if UDONSHARP_DEBUG
                if (!(method is ExternMethodSymbol))
                    throw new ArgumentException("Method must be extern method symbol");
                #endif
                
                Method = method;
            }
        }

        internal class RetInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Return;
            public override uint Size => 20; // Push, Copy, JumpIndirect
        }
    }
    
    
}
