using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;


namespace UdonSharp.Compiler.Symbols
{
    internal sealed class TypeSymbol : Symbol
    {
        private static readonly object dictionaryLazyInitLock = new object();
        private ConcurrentDictionary<ISymbol, Symbol> typeSymbols;

        public INamedTypeSymbol RoslynTypeSymbol { get { return (INamedTypeSymbol)RoslynSymbol; } }

        public TypeSymbol(INamedTypeSymbol sourceSymbol)
        {
            RoslynSymbol = sourceSymbol;
        }

        private void InitSymbolDict()
        {
            if (typeSymbols != null)
                return;

            lock (dictionaryLazyInitLock)
            {
                if (typeSymbols != null)
                    return;

                typeSymbols = new ConcurrentDictionary<ISymbol, Symbol>();
            }
        }

        public FieldSymbol GetFieldSymbol(IFieldSymbol sourceSymbol)
        {
            InitSymbolDict();

            Symbol fieldSymbol = typeSymbols.GetOrAdd(sourceSymbol, (key) => new FieldSymbol(this, sourceSymbol));

            return (FieldSymbol)fieldSymbol;
        }

        public MethodSymbol GetMethodSymbol(IMethodSymbol sourceSymbol)
        {
            InitSymbolDict();

            Symbol methodSymbol = typeSymbols.GetOrAdd(sourceSymbol, (key) => new MethodSymbol(this));

            return (MethodSymbol)methodSymbol;
        }

        public PropertySymbol GetPropertySymbol(IPropertySymbol sourceSymbol)
        {
            InitSymbolDict();

            Symbol propertySymbol = typeSymbols.GetOrAdd(sourceSymbol, (key) => new PropertySymbol(this));

            return (PropertySymbol)propertySymbol;
        }

        private static readonly object directDependencyLock = new object();
        ImmutableArray<Symbol> lazyDirectDependencies;

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            if (lazyDirectDependencies != null)
                return lazyDirectDependencies;

            lock (directDependencyLock)
            {
                if (lazyDirectDependencies != null)
                    return lazyDirectDependencies;

                List<Symbol> directDependencies = new List<Symbol>();

                lazyDirectDependencies = ImmutableArray.CreateRange<Symbol>(typeSymbols.Select(e => e.Value));
            }

            return lazyDirectDependencies;
        }

        public override ImmutableArray<Symbol> GetDirectDependencies<T>()
        {
            return ImmutableArray.CreateRange<Symbol>(GetDirectDependencies().OfType<T>());
        }
    }
}
