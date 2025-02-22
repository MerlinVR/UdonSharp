
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Binder;


namespace UdonSharp.Compiler.Symbols
{
    internal sealed class LocalSymbol : Symbol
    {
        public TypeSymbol Type { get; }
        
        public LocalSymbol(ILocalSymbol sourceSymbol, AbstractPhaseContext bindContext)
            :base(sourceSymbol, bindContext)
        {
            if (!(bindContext is BindContext))
                throw new InvalidOperationException("Local Symbols can only be created during the bind phase");
            
            ContainingType = bindContext.GetTypeSymbol(sourceSymbol.ContainingType);
            Type = bindContext.GetTypeSymbol(sourceSymbol.Type);
        }

        public new ILocalSymbol RoslynSymbol => (ILocalSymbol)base.RoslynSymbol;

        public bool IsConst => RoslynSymbol.IsConst;
        public object ConstantValue => RoslynSymbol.ConstantValue;

        public override void Bind(BindContext context)
        {
            throw new NotSupportedException("Local symbols cannot be bound");
        }

        public override bool IsBound => true;
    }
}
