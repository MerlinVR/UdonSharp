
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class TypeParameterSymbol : TypeSymbol
    {
        public TypeParameterSymbol(ITypeParameterSymbol sourceSymbol, AbstractPhaseContext context) 
            :base(sourceSymbol, context)
        {
        }

        public override void Bind(BindContext context)
        {
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context)
        {
            throw new NotImplementedException();
        }
    }
}
