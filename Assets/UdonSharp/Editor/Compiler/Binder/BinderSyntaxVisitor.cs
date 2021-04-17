using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp.Compiler.Binder
{
    internal class BinderSyntaxVisitor : CSharpSyntaxVisitor<BoundNode>
    {
        BindModule owningModule;

        public BinderSyntaxVisitor(BindModule owningModule)
        {
            this.owningModule = owningModule;
        }

        public override BoundNode DefaultVisit(SyntaxNode node)
        {
            throw new System.NotSupportedException($"UdonSharp does not currently support node type {node.Kind()}");
        }
        

    }
}
