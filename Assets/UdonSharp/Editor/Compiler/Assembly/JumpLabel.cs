
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Assembly
{
    internal class JumpLabel
    {
        public MethodSymbol DebugMethod { get; set; }
        
        public uint Address { get; set; } = uint.MaxValue;
    }
}
