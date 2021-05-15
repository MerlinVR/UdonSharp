using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class ExternPropertySymbol : PropertySymbol
    {
        public ExternPropertySymbol(IPropertySymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {

        }

        public override bool IsExtern => true;
    }
}
