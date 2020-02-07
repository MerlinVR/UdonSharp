using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEditor;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    public class UdonSharpCompiler
    {
        private MonoScript source;
        private CompilationModule module;

        public UdonSharpCompiler(MonoScript sourceScript)
        {
            source = sourceScript ?? throw new System.ArgumentException("No valid C# source file specified!");
            module = new CompilationModule(source);
        }

        public string Compile()
        {
            return module.Compile();
        }

        public void AssignHeapConstants(IUdonProgram program)
        {
            if (program != null)
            {
                foreach (SymbolDefinition symbol in module.moduleSymbols.GetAllUniqueChildSymbols())
                {
                    if (symbol.symbolDefaultValue != null)
                    {
                        uint symbolAddress = program.SymbolTable.GetAddressFromSymbol(symbol.symbolUniqueName);

                        program.Heap.SetHeapVariable(symbolAddress, symbol.symbolDefaultValue, symbol.symbolCsType);
                    }
                }
            }
        }
    }

}
