using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using UdonSharp.Compiler.Binder;
using UnityEngine;


namespace UdonSharp.Compiler.Symbols
{
    internal class PropertySymbol : Symbol
    {
        public PropertySymbol(IPropertySymbol sourceSymbol, BindContext context)
            :base(sourceSymbol, context)
        {

        }

        public override void Bind(BindContext context)
        {
            throw new System.NotImplementedException();
        }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }
    }
}
