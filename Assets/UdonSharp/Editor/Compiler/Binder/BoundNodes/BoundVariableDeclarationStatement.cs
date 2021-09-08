
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundVariableDeclarationStatement : BoundStatement
    {
        private BoundVariableDeclaratorStatement[] Declarations { get; }

        public BoundVariableDeclarationStatement(VariableDeclarationSyntax node, BoundVariableDeclaratorStatement[] declarations)
            : base(node)
        {
            Declarations = declarations;
        }

        public override void Emit(EmitContext context)
        {
            foreach (BoundVariableDeclaratorStatement declaration in Declarations)
                context.Emit(declaration);
        }
    }
}
