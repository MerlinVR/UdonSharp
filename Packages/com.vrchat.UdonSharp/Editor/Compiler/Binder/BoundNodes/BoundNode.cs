
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    /// <summary>
    /// Base for bound expressions and statements, largely mirroring the Roslyn internal layout for the compiler
    /// </summary>
    internal abstract class BoundNode
    {
        public SyntaxNode SyntaxNode { get; }

        protected BoundNode(SyntaxNode node)
        {
            SyntaxNode = node;
        }

        public virtual void Emit(EmitContext context)
        {
            throw new System.NotImplementedException($"Emit is not implemented on {GetType()}");
        }
    }
}
