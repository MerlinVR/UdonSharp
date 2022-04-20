
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal class ExternFieldSymbol : FieldSymbol, IExternAccessor
    {
        public ExternFieldSymbol(IFieldSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            Type = context.GetTypeSymbol(sourceSymbol.Type);
        }

        public override bool IsExtern => true;

        public override bool IsBound => true;

        private string _externSetSignature;
        public string ExternSetSignature => _externSetSignature ?? (_externSetSignature = CompilerUdonInterface.GetUdonAccessorName(this, CompilerUdonInterface.FieldAccessorType.Set));

        private string _externGetSignature;

        public string ExternGetSignature => _externGetSignature ?? (_externGetSignature = CompilerUdonInterface.GetUdonAccessorName(this, CompilerUdonInterface.FieldAccessorType.Get));
    }
}
