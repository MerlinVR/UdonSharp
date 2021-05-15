using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundExpressionStatement : BoundStatement
    {
        public BoundExpression Expression { get; }

        public BoundExpressionStatement(SyntaxNode node, BoundExpression expression)
            : base(node)
        {
            Expression = expression;
        }
    }
}
