using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler
{
    /// <summary>
    /// Base representation of a phase of compilation
    /// </summary>
    internal abstract class AbstractPhaseContext
    {
        public CompilationContext CompileContext { get; }
        public SyntaxNode CurrentNode { get; set; }

        protected AbstractPhaseContext(CompilationContext compileContext)
        {
            CompileContext = compileContext;
        }
        
        public TypeSymbol GetTypeSymbol(ITypeSymbol type)
        {
            TypeSymbol typeSymbol = CompileContext.GetTypeSymbol(type, this);
            typeSymbol = (TypeSymbol)RedirectTypeSymbol(typeSymbol);
            OnSymbolRetrieved(typeSymbol);

            return typeSymbol;
        }

        public TypeSymbol GetTypeSymbolWithoutRedirect(ITypeSymbol type)
        {
            TypeSymbol typeSymbol = CompileContext.GetTypeSymbol(type, this);
            OnSymbolRetrieved(typeSymbol);

            return typeSymbol;
        }

        public TypeSymbol GetUdonTypeSymbol(ITypeSymbol type)
        {
            TypeSymbol typeSymbol = CompileContext.GetUdonTypeSymbol(type, this);
            typeSymbol = (TypeSymbol)RedirectTypeSymbol(typeSymbol);
            OnSymbolRetrieved(typeSymbol);

            return typeSymbol;
        }

        public TypeSymbol GetTypeSymbol(Type systemType)
        {
            TypeSymbol typeSymbol = CompileContext.GetTypeSymbol(systemType, this);
            typeSymbol = (TypeSymbol)RedirectTypeSymbol(typeSymbol);
            OnSymbolRetrieved(typeSymbol);

            return typeSymbol;
        }

        public TypeSymbol GetTypeSymbol(SpecialType type)
        {
            TypeSymbol typeSymbol = CompileContext.GetTypeSymbol(type, this);
            typeSymbol = (TypeSymbol)RedirectTypeSymbol(typeSymbol);
            OnSymbolRetrieved(typeSymbol);

            return typeSymbol;
        }

        public Symbol GetSymbol(ISymbol sourceSymbol)
        {
            Symbol symbol = CompileContext.GetSymbol(sourceSymbol, this);
            symbol = RedirectTypeSymbol(symbol);
            symbol = RedirectParameterSymbol(symbol);
            symbol = RedirectMethodSymbol(symbol);
            OnSymbolRetrieved(symbol);

            return symbol;
        }
        
        public Symbol GetSymbolNoRedirect(ISymbol sourceSymbol)
        {
            Symbol symbol = CompileContext.GetSymbol(sourceSymbol, this);
            OnSymbolRetrieved(symbol);

            return symbol;
        }

        public void MarkSymbolReferenced(Symbol symbol)
        {
            OnSymbolRetrieved(symbol);
        }

        protected virtual void OnSymbolRetrieved(Symbol symbol)
        {
        }

        protected virtual Symbol RedirectTypeSymbol(Symbol symbol) => symbol;
        protected virtual Symbol RedirectMethodSymbol(Symbol symbol) => symbol;
        protected virtual Symbol RedirectParameterSymbol(Symbol symbol) => symbol;
    }
}
