using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundBlock : BoundStatement
    {
        public ImmutableArray<BoundStatement> Statements { get; }

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
    }
}
