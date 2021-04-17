using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;


namespace UdonSharp.Compiler.Symbols
{
    internal class PropertySymbol : Symbol
    {
        //private ConcurrentDictionary<IFieldSymbol, >

        public PropertySymbol(TypeSymbol declaringType)
        {

        }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }

        public override ImmutableArray<Symbol> GetDirectDependencies<T>()
        {
            throw new System.NotImplementedException();
        }

        public void Bind(BindModule module)
        {

        }
    }
}
