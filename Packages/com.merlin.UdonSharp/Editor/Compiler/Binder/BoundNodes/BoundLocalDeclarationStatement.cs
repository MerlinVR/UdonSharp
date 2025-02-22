
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundLocalDeclarationStatement : BoundStatement
    {
        private BoundVariableDeclarationStatement DeclarationStatement { get; }

        public BoundLocalDeclarationStatement(LocalDeclarationStatementSyntax node, BoundVariableDeclarationStatement declarationStatement)
            : base(node)
        {
            DeclarationStatement = declarationStatement;
        }

        public override void Emit(EmitContext context)
        {
            context.Emit(DeclarationStatement);
        }
    }
}
