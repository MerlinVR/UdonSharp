using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal class ExternMethodSymbol : MethodSymbol
    {
        public ExternMethodSymbol(IMethodSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {

        }

        public override bool IsExtern => true;
    }
}
