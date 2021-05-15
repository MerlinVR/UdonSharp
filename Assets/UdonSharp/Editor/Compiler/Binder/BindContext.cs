
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal class BindContext
    {
        public BindContext(CompilationContext context)
        {
            CompileContext = context;
        }

        public CompilationContext CompileContext { get; private set; }

        private ConcurrentBag<Symbol> symbolsToBind = new ConcurrentBag<Symbol>();
        private ConcurrentDictionary<ITypeSymbol, TypeSymbol> typeSymbolLookup = new ConcurrentDictionary<ITypeSymbol, TypeSymbol>();

        void QueueBind(Symbol symbol)
        {
            if (!symbol.IsBound) // Something may resolve this later on so this check is not a guarantee that all symbols in the queue will not be bound
                symbolsToBind.Add(symbol);
        }

        public TypeSymbol GetTypeSymbol(ITypeSymbol type)
        {
            TypeSymbol typeSymbol = typeSymbolLookup.GetOrAdd(type, (key) => TypeSymbolFactory.CreateSymbol(type, this));

            QueueBind(typeSymbol);

            return typeSymbol;
        }

        public Symbol GetSymbol(ISymbol sourceSymbol)
        {
            if (sourceSymbol == null)
                throw new System.NullReferenceException("Source symbol cannot be null");
            
            if (sourceSymbol is INamedTypeSymbol typeSymbol)
                return GetTypeSymbol(typeSymbol);

            if (sourceSymbol.ContainingType != null)
                return GetTypeSymbol(sourceSymbol.ContainingType).GetSymbol(sourceSymbol, this);

            throw new System.InvalidOperationException($"Could not get symbol for {sourceSymbol}");
        }
    }
}
