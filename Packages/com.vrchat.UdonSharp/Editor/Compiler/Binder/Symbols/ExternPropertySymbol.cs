
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Symbols
{
    internal class ExternPropertySymbol : PropertySymbol, IExternAccessor
    {
        public ExternPropertySymbol(IPropertySymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (GetMethod != null)
                ExternGetSignature = ((ExternMethodSymbol) GetMethod).ExternSignature;
            
            if (SetMethod != null)
                ExternSetSignature = ((ExternMethodSymbol) SetMethod).ExternSignature;
        }

        public override bool IsExtern => true;

        public override bool IsBound => true;
        public string ExternGetSignature { get; }
        public string ExternSetSignature { get; }
    }
}
