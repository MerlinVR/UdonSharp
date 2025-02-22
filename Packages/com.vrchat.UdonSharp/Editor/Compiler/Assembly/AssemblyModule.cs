
using System;
using System.Collections.Generic;
using System.Text;
using UdonSharp.Compiler.Assembly.Instructions;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Assembly
{
    /// <summary>
    /// Contains the assembly for a given module of compilation.
    /// A module in this context is all code and associated heap values that will be compiled into a single program asset.
    /// This includes primary events and methods on a given UdonBehaviour, along with all imported methods for referenced types.
    /// </summary>
    internal class AssemblyModule
    {
        public CompilationContext CompileContext { get; }
        
        private List<AssemblyInstruction> _instructions = new List<AssemblyInstruction>();
        private List<JumpLabel> _jumpLabels = new List<JumpLabel>();
        private uint _currentAddress;

        public uint CurrentAddress => _currentAddress;
        
        public int ExecutionOrder { get; set; }

        public ValueTable RootTable { get; }

        public Dictionary<Symbol, ExportAddress> Exports { get; } = new Dictionary<Symbol, ExportAddress>();

        public AssemblyModule(CompilationContext context)
        {
            CompileContext = context;
            RootTable = new ValueTable(this, null);
        }

        public AssemblyInstruction this[int index]
        {
            get
            {
                if (index < 0 || index >= _instructions.Count)
                    throw new IndexOutOfRangeException("Instruction index is not valid");

                return _instructions[index];
            }
        }

        public int InstructionCount => _instructions.Count;

        #region Instruction Adds
        private void AddInstruction(AssemblyInstruction instruction)
        {
            _instructions.Add(instruction);
            instruction.InstructionAddress = _currentAddress;
            _currentAddress += instruction.Size;
        }
        
        public void AddNop()
        {
            AddInstruction(new NopInstruction());
        }

        public void AddCommentTag(string comment)
        {
            AddInstruction(new Comment(comment));
        }

        public void AddExportTag(UdonSharpBehaviourMethodSymbol exportMethod)
        {
            AddInstruction(new ExportTag(exportMethod));
        }

        public void AddFieldChangeExportTag(UdonSharpBehaviourFieldSymbol fieldSymbol)
        {
            AddInstruction(new FieldCallbackExportTag(fieldSymbol));
        }

        public void AddSyncTag(Value syncValue, UdonSyncMode syncMode)
        {
            AddInstruction(new SyncTag(syncValue, syncMode));
        }

        public void AddPush(Value pushValue)
        {
            if (pushValue == null)
                throw new ArgumentNullException(nameof(pushValue));
            
            AddInstruction(new PushInstruction(pushValue));
        }

        public void AddPop()
        {
            AddInstruction(new PopInstruction());
        }

        public void AddCopy(Value sourceValue, Value targetValue)
        {
            if (targetValue.IsConstant)
                throw new ArgumentException("Cannot copy to const value");
            
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

        public void AddExtern(IExternSymbol method)
        {
            AddInstruction(new ExternInstruction(method));
        }

        public void AddExternSet(IExternAccessor externAccessor)
        {
            AddInstruction(new ExternSetInstruction(externAccessor));
        }
        
        public void AddExternGet(IExternAccessor externAccessor)
        {
            AddInstruction(new ExternGetInstruction(externAccessor));
        }

        public void AddReturn(Value returnVal)
        {
            AddInstruction(new RetInstruction(returnVal));
        }
        #endregion

        #region Jump Labels

        public JumpLabel CreateLabel()
        {
            JumpLabel newLabel = new JumpLabel();
            
            _jumpLabels.Add(newLabel);

            return newLabel;
        }

        public void LabelJump(JumpLabel label)
        {
            if (label.Address != uint.MaxValue)
                throw new ArgumentException("Label has already been set");

            label.Address = _currentAddress;
        }

        #endregion

        #region Address Allocation

        public void AddAddress(ExportAddress address)
        {
            Exports.Add(address.AddressSymbol, address);
        }
        #endregion

        private void BuildDataBlock(StringBuilder builder)
        {
            List<Value> allValues = RootTable.GetAllUniqueChildValues();

            builder.Append(".data_start\n");

            foreach (Value value in allValues)
            {
                if (value.AssociatedSymbol is FieldSymbol fieldSymbol)
                {
                    if (fieldSymbol.IsSerialized)
                        builder.AppendFormat("    .export {0}\n", value.UniqueID);

                    UdonSyncMode? syncMode = fieldSymbol.SyncMode;

                    switch (syncMode)
                    {
                        case UdonSyncMode.None:
                            builder.AppendFormat("    .sync {0}, none\n", value.UniqueID);
                            break;
                        case UdonSyncMode.Linear:
                            builder.AppendFormat("    .sync {0}, linear\n", value.UniqueID);
                            break;
                        case UdonSyncMode.Smooth:
                            builder.AppendFormat("    .sync {0}, smooth\n", value.UniqueID);
                            break;
                    }
                }
            }

            foreach (Value value in allValues)
            {
                builder.AppendFormat("    {0}\n", value.GetDeclarationStr());
            }

            builder.Append(".data_end\n");
        }
        
        private void BuildInstructionUasm(StringBuilder builder)
        {
            builder.Append(".code_start\n");

            if (ExecutionOrder != 0)
                builder.Append($".update_order {ExecutionOrder}\n");
            
            foreach (var instruction in _instructions)
                instruction.WriteAssembly(builder);

            builder.Append(".code_end\n");
        }
        
        public string BuildUasmStr()
        {
            StringBuilder uasmBuilder = new StringBuilder();
            
            BuildDataBlock(uasmBuilder);
            BuildInstructionUasm(uasmBuilder);

            return uasmBuilder.ToString();
        }

        public uint GetHeapSize()
        {
            int heapValCount = RootTable.GetAllUniqueChildValues().Count;

            HashSet<string> uniqueExternCount = new HashSet<string>();
            
            foreach (AssemblyInstruction instruction in _instructions)
            {
                switch (instruction)
                {
                    case ExternInstruction externMethodSymbol:
                        uniqueExternCount.Add(externMethodSymbol.Extern.ExternSignature);
                        break;
                    case ExternSetInstruction externSetInstruction:
                        uniqueExternCount.Add(externSetInstruction.Extern.ExternSetSignature);
                        break;
                    case ExternGetInstruction externGetInstruction:
                        uniqueExternCount.Add(externGetInstruction.Extern.ExternGetSignature);
                        break;
                }
            }

            return (uint)(heapValCount + uniqueExternCount.Count);
        }
    }
}
