
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    /// <summary>
    /// The context for the binding phase, this is local to a single type that is being bound
    /// </summary>
    internal class BindContext : AbstractPhaseContext
    {
        private ITypeSymbol BindSymbol { get; }

        private Symbol _currentBindSymbol;
        private HashSet<Symbol> _currentReferencedSymbols;
        
        public BindContext(CompilationContext context, ITypeSymbol bindSymbol)
        :base(context)
        {
            BindSymbol = bindSymbol;
        }

        public void Bind()
        { 
            GetTypeSymbol(BindSymbol).Bind(this);
        }

        protected override void OnSymbolRetrieved(Symbol symbol)
        {
            if (_currentBindSymbol != null && !symbol.IsExtern)
                _currentReferencedSymbols.Add(symbol);
        }

        public IDisposable OpenMemberBindScope(Symbol boundSymbol)
        {
            return new MemberBindScope(this, boundSymbol);
        }

        private void FinishSymbolBind()
        {
            _currentBindSymbol.SetDependencies(_currentReferencedSymbols);
            _currentBindSymbol = null;
            _currentReferencedSymbols = null;
        }

        public TypeSymbol GetCurrentReturnType()
        {
            return (_currentBindSymbol as MethodSymbol)?.ReturnType;
        }

        private class MemberBindScope : IDisposable
        {
            private BindContext context;
            
            public MemberBindScope(BindContext context, Symbol bindSymbol)
            {
                this.context = context;
                context._currentBindSymbol = bindSymbol;
                context._currentReferencedSymbols = new HashSet<Symbol>();
            }
            
            public void Dispose()
            {
                context.FinishSymbolBind();
            }
        }
    }
}
