using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using UdonSharp.Compiler.Binder;
using UnityEngine;


namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourFieldSymbol : FieldSymbol
    {
        public UdonSharpBehaviourFieldSymbol(IFieldSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {

        }
    }
}
