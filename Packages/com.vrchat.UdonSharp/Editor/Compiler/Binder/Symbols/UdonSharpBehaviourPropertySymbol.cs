
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourPropertySymbol : PropertySymbol
    {
        internal UdonSharpBehaviourFieldSymbol CallbackSymbol { get; private set; }
        
        public override void MarkFieldCallback(FieldSymbol symbol)
        {
            CallbackSymbol = (UdonSharpBehaviourFieldSymbol)symbol;
        }

        public UdonSharpBehaviourPropertySymbol(IPropertySymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {

        }
    }
}
