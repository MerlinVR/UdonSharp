
using Microsoft.CodeAnalysis;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundStatement : BoundNode
    {
        protected BoundStatement(SyntaxNode node)
            :base(node)
        {
            
        }
    }
}
