
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Emit;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundBlock : BoundStatement
    {
        private ImmutableArray<BoundStatement> Statements { get; }

        public BoundBlock(SyntaxNode node)
            :base(node)
        {
            Statements = ImmutableArray<BoundStatement>.Empty;
        }

        public BoundBlock(SyntaxNode node, IEnumerable<BoundStatement> statements)
            : base(node)
        {
            Statements = ImmutableArray.CreateRange(statements);
        }

        public override void Emit(EmitContext context)
        {
            using (context.OpenBlockScope())
            {
                foreach (BoundStatement statement in Statements)
                    context.Emit(statement);
            }
        }
    }
}
