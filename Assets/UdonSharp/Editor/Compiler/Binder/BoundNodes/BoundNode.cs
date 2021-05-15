using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    /// <summary>
    /// Base for bound expressions and statements, largely mirroring the Roslyn internal layout for the compiler
    /// </summary>
    internal abstract class BoundNode
    {
        public virtual SyntaxNode SyntaxNode { get; private set; }

        private BoundNode() { }

        protected BoundNode(SyntaxNode node)
        {
            SyntaxNode = node;
        }
    }
}
