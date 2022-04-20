
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Symbols
{
    internal class ExternMethodSymbol : MethodSymbol, IExternSymbol
    {
        public ExternMethodSymbol(IMethodSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol != null)
                ExternSignature = CompilerUdonInterface.GetUdonMethodName(this, context);
        }

        public override bool IsExtern => true;

        public override bool IsBound => true;
        public virtual string ExternSignature { get; }

        public override string ToString()
        {
            return $"ExternMethodSymbol: {RoslynSymbol}";
        }
    }
}
