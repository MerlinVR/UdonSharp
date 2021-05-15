using Microsoft.CodeAnalysis;
using System.Linq;
using UdonSharp.Compiler.Binder;
using UdonSharp.Core;
using UdonSharp.Localization;


namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourMethodSymbol : MethodSymbol
    {
        public UdonSharpBehaviourMethodSymbol(IMethodSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
            IMethodSymbol symbol = RoslynSymbol;

            if (symbol.MethodKind == MethodKind.Constructor && !symbol.IsImplicitlyDeclared)
                throw new NotSupportedException(LocStr.CE_UdonSharpBehaviourConstructorsNotSupported, symbol.Locations.FirstOrDefault());
            if (symbol.IsGenericMethod)
                throw new NotSupportedException(LocStr.CE_UdonSharpBehaviourGenericMethodsNotSupported, symbol.Locations.FirstOrDefault());

            base.Bind(context);
        }
    }
}
