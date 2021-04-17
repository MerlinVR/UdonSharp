using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;


namespace UdonSharp.Compiler.Symbols
{
    internal class MethodSymbol : Symbol
    {
        //private ConcurrentDictionary<IFieldSymbol, >

        public MethodSymbol(TypeSymbol declaringType)
        {

        }

        public bool IsConstructor { get; private set; }

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }

        public override ImmutableArray<Symbol> GetDirectDependencies<T>()
        {
            throw new System.NotImplementedException();
        }
    }
}
