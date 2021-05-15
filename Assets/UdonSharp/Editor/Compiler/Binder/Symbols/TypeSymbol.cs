using Microsoft.CodeAnalysis;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal abstract class TypeSymbol : Symbol
    {
        private static readonly object dictionaryLazyInitLock = new object();
        private ConcurrentDictionary<ISymbol, Symbol> typeSymbols;

        public ITypeSymbol RoslynTypeSymbol { get { return (ITypeSymbol)RoslynSymbol; } }

        public TypeSymbol(ITypeSymbol sourceSymbol, BindContext bindContext)
            : base(sourceSymbol, bindContext)
        {
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

        public Symbol GetSymbol(ISymbol symbol, BindContext context)
        {
            InitSymbolDict();
            return typeSymbols.GetOrAdd(symbol, (key) => CreateSymbol(symbol, context));
        }

        public T GetSymbol<T>(ISymbol symbol, BindContext context) where T : Symbol
        {
            return (T)GetSymbol(symbol, context);
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

        /// <summary>
        /// Implemented by derived type symbols to create their own relevant symbol for the roslyn symbol
        /// </summary>
        /// <param name="roslynSymbol"></param>
        /// <returns></returns>
        protected abstract Symbol CreateSymbol(ISymbol roslynSymbol, BindContext context);
    }
}
