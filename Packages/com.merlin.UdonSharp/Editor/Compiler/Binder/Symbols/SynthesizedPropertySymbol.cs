
namespace UdonSharp.Compiler.Symbols
{
    internal sealed class SynthesizedPropertySymbol : PropertySymbol
    {
        public SynthesizedPropertySymbol(AbstractPhaseContext context, MethodSymbol getMethod, MethodSymbol setMethod) 
            :base(null, context)
        {
            GetMethod = getMethod;
            SetMethod = setMethod;
        }
    }
}
