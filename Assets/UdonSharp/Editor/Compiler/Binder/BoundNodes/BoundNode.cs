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
        public virtual ISymbol Symbol { get { return null; } }
        public virtual SyntaxNode SyntaxNode { get { return null; } }
    }
}
