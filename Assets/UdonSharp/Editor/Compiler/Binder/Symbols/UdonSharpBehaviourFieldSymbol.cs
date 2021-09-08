
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourFieldSymbol : FieldSymbol
    {
        public UdonSharpBehaviourFieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {

        }
    }
}
