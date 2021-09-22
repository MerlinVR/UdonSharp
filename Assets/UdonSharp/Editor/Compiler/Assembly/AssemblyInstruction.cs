
using System;
using System.Text;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

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
        ExternSet,
        ExternGet,
        JumpIndirect,
        Return,
        Comment,
        ExportTag,
        SyncTag,
    }

    internal abstract class AssemblyInstruction
    {
        public virtual InstructionKind GetKind() => throw new NotImplementedException();
        public virtual uint Size => throw new NotImplementedException();

        public uint InstructionAddress { get; set; } = UInt32.MaxValue;

        public string Comment { get; set; }

        public abstract void WriteAssembly(StringBuilder builder);

        protected void WriteIndentedLine(string str, StringBuilder builder, int indent = 2)
        {
            if (indent == 2)
                builder.AppendFormat("        {0}\n", str);
            else
                builder.AppendFormat("{0}{1}\n", new string(' ', indent * 4), str);
        }
    }

    namespace Instructions
    {
        internal class NopInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Nop;
            public override uint Size => 4;
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine("NOP", builder);
            }

            public override string ToString()
            {
                return "NOP";
            }
        }

        internal class Comment : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Comment;
            public override uint Size => 0;
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"# {Comment}", builder, 0);
            }

            public Comment(string comment)
            {
                Comment = comment;
            }
            
            public override string ToString()
            {
                return "# " + Comment;
            }
        }

        internal class ExportTag : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.ExportTag;
            public override uint Size => 0;

            public UdonSharpBehaviourMethodSymbol ExportedMethod { get; }
            
            public ExportTag(UdonSharpBehaviourMethodSymbol methodSymbol)
            {
                ExportedMethod = methodSymbol;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($".export {ExportedMethod.ExportedMethodAddress.AddressString}", builder, 1);
                WriteIndentedLine($"{ExportedMethod.ExportedMethodAddress.AddressString}:", builder, 1);
            }
            
            public override string ToString()
            {
                return "export " + ExportedMethod.ExportedMethodAddress.AddressString;
            }
        }
        
        internal class FieldCallbackExportTag : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.ExportTag;
            public override uint Size => 0;

            public UdonSharpBehaviourFieldSymbol ExportedField { get; }
            
            public FieldCallbackExportTag(UdonSharpBehaviourFieldSymbol fieldSymbol)
            {
                ExportedField = fieldSymbol;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                string fieldChangeName = $"_onVarChange_{ExportedField.Name}";
                
                WriteIndentedLine($".export {fieldChangeName}", builder, 1);
                WriteIndentedLine($"{fieldChangeName}:", builder, 1);
            }
            
            public override string ToString()
            {
                return "export field change" + ExportedField.Name;
            }
        }
        
        internal class SyncTag : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.SyncTag;
            public override uint Size => 0;

            public Value SyncedValue { get; }
            public UdonSyncMode SyncMode { get; }
            
            public SyncTag(Value syncedValue, UdonSyncMode syncMode)
            {
                SyncedValue = syncedValue;
                SyncMode = syncMode;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                // WriteIndentedLine($".export {}");
            }
            
            public override string ToString()
            {
                return $"sync {SyncedValue}, {SyncMode}";
            }
        }

        internal class PushInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Push;
            public override uint Size => 8;

            public Value PushValue { get; }
            
            public PushInstruction(Value value)
            {
                PushValue = value;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"PUSH, {PushValue.UniqueID}", builder);
            }

            public override string ToString()
            {
                return "PUSH: " + PushValue;
            }
        }

        internal class PopInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Pop;
            public override uint Size => 4;

            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"POP", builder);
            }
            
            public override string ToString()
            {
                return "POP";
            }
        }

        internal class CopyInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Copy;
            public override uint Size => 20;

            public Value SourceValue { get; }
            public Value TargetValue { get; }
            
            public CopyInstruction(Value sourceValue, Value targetValue)
            {
                SourceValue = sourceValue;
                TargetValue = targetValue;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"PUSH, {SourceValue.UniqueID}", builder);
                WriteIndentedLine($"PUSH, {TargetValue.UniqueID}", builder);
                WriteIndentedLine($"COPY", builder);
            }

            public override string ToString()
            {
                return $"COPY: {SourceValue} -> {TargetValue}";
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
            
            public override void WriteAssembly(StringBuilder builder)
            {
                if (JumpTarget.Address == uint.MaxValue)
                    throw new InvalidOperationException($"Cannot jump to uninitialized jump label!" + (JumpTarget.DebugMethod == null ? "" : $" Target method: {JumpTarget.DebugMethod}"));
                
                WriteIndentedLine($"JUMP, 0x{JumpTarget.Address:X8}", builder);
            }

            public override string ToString()
            {
                return $"JUMP: {JumpTarget}";
            }
        }

        internal class JumpIfFalseInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.JumpIfFalse;
            public override uint Size => 16;
            
            public JumpLabel JumpTarget { get; }
            public Value ConditionValue { get; }

            public JumpIfFalseInstruction(JumpLabel jumpTarget, Value conditionValue)
            {
                JumpTarget = jumpTarget;
                ConditionValue = conditionValue;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                if (JumpTarget.Address == uint.MaxValue)
                    throw new InvalidOperationException($"Cannot jump to uninitialized jump label!" + (JumpTarget.DebugMethod == null ? "" : $" Target method: {JumpTarget.DebugMethod}"));
                
                WriteIndentedLine($"PUSH, {ConditionValue.UniqueID}", builder);
                WriteIndentedLine($"JUMP_IF_FALSE, 0x{JumpTarget.Address:x8}", builder);
            }

            public override string ToString()
            {
                return $"JUMP_IF_FALSE: target: {JumpTarget}, condition {ConditionValue}";
            }
        }

        internal class JumpIndirectInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.JumpIndirect;
            public override uint Size => 8;
            
            public Value JumpTargetValue { get; }

            public JumpIndirectInstruction(Value jumpTargetValue)
            {
                JumpTargetValue = jumpTargetValue;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"JUMP_INDIRECT, {JumpTargetValue.UniqueID}", builder);
            }

            public override string ToString()
            {
                return $"JUMP_INDIRECT: {JumpTargetValue}";
            }
        }

        internal class ExternInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Extern;
            public override uint Size => 8;
            
            public IExternSymbol Extern { get; }

            public ExternInstruction(IExternSymbol @extern)
            {
                Extern = @extern;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"EXTERN, \"{Extern.ExternSignature}\"", builder);
            }

            public override string ToString()
            {
                return $"EXTERN: {Extern}, {Extern.ExternSignature}";
            }
        }

        internal class ExternSetInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.ExternSet;
            public override uint Size => 8;
            
            public IExternAccessor Extern { get; }
            
            public ExternSetInstruction(IExternAccessor externSet)
            {
                Extern = externSet;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"EXTERN, \"{Extern.ExternSetSignature}\"", builder);
            }
        }
        
        internal class ExternGetInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.ExternGet;
            public override uint Size => 8;
            
            public IExternAccessor Extern { get; }
            
            public ExternGetInstruction(IExternAccessor externGet)
            {
                Extern = externGet;
            }
            
            public override void WriteAssembly(StringBuilder builder)
            {
                WriteIndentedLine($"EXTERN, \"{Extern.ExternGetSignature}\"", builder);
            }
        }

        internal class RetInstruction : AssemblyInstruction
        {
            public override InstructionKind GetKind() => InstructionKind.Return;
            public override uint Size => 20; // Push, Copy, JumpIndirect
            // public override uint Size => 12; // Temp force exit jump

            public Value RetValRef { get; }

            public RetInstruction(Value retVal)
            {
                RetValRef = retVal;
            }

            public override void WriteAssembly(StringBuilder builder)
            {
                // WriteIndentedLine("POP", builder);
                // WriteIndentedLine("JUMP, 0xFFFFFFF0", builder);
                
                WriteIndentedLine($"PUSH, {RetValRef.UniqueID}", builder);
                WriteIndentedLine("COPY", builder);
                WriteIndentedLine($"JUMP_INDIRECT, {RetValRef.UniqueID}", builder);
            }
            
            public override string ToString()
            {
                return "RET";
            }
        }
    }
    
    
}
