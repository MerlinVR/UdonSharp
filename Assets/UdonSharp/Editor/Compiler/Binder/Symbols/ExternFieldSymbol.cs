using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class ExternFieldSymbol : FieldSymbol
    {
        public ExternFieldSymbol(IFieldSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {

        }

        public override bool IsExtern => true;
    }
}
