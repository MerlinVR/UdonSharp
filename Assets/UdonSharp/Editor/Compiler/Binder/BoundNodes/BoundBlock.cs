using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal class BoundBlock : BoundExpression
    {
        public List<BoundExpression> BodyExpressions { get; } = new List<BoundExpression>();
    }
}
