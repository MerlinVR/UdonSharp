using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundBlock : BoundStatement
    {
        public List<BoundNode> BodyContents { get; } = new List<BoundNode>();
    }
}
