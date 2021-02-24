using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;

namespace UdonSharp.Compiler.Symbols
{
    internal class TypeSymbol : SymbolBase
    {
        public ImmutableList<SyntaxNode> DeclaringNodes { get; private set; }

        /// <summary>
        /// The interface types that this symbol implements
        /// </summary>
        public ImmutableList<TypeSymbol> Interfaces { get; private set; }

        /// <summary>
        /// The type this is declared in if this is a nested class
        /// </summary>
        public TypeSymbol ContainingType { get; private set; } = null;

        public ImmutableList<TypeSymbol> GenericTypeArguments { get; private set; }

        public string TypeName { get; set; }
        public string EnclosingNamespace { get; private set; } = "";

        public ImmutableList<TypeSymbol> NestedTypes { get; private set; }
        public ImmutableList<FieldSymbol> FieldSymbols { get; private set; }
        public ImmutableList<PropertySymbol> PropertySymbols { get; private set; }
        public ImmutableList<MethodSymbol> MethodSymbols { get; private set; }

        public bool IsVirtual { get; }
        public bool IsInterface { get; }
        public bool IsAbstract { get; }
        public bool IsPartial { get { return (DeclaringNodes?.Count ?? 1) > 1; } }
    }
}
