
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourPropertySymbol : PropertySymbol
    {
        public UdonSharpBehaviourPropertySymbol(IPropertySymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {

        }
    }
}
