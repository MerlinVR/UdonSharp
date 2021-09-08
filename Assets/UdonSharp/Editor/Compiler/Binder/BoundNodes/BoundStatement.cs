using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
