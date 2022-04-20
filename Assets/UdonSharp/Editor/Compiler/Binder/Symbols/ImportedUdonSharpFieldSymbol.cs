
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal class ImportedUdonSharpFieldSymbol : FieldSymbol
    {
        public ImportedUdonSharpFieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {

        }
    }
}
