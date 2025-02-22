
using System;
using System.Collections.Generic;
using System.Linq;
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
        private IEnumerable<Symbol> TypeSymbolsToBind { get; }

        private Symbol _currentBindSymbol;
        public MethodSymbol CurrentBindMethod => _currentBindSymbol as MethodSymbol;
        
        private HashSet<Symbol> _currentReferencedSymbols;
        
        public BindContext(CompilationContext context, ITypeSymbol bindSymbol, IEnumerable<Symbol> referencedTypeSymbolsToBind)
            :base(context)
        {
            BindSymbol = bindSymbol;
            TypeSymbolsToBind = referencedTypeSymbolsToBind;
        }

        public void Bind()
        {
            TypeSymbol containingType = GetTypeSymbol(BindSymbol);
            
            if (!containingType.IsBound)
                containingType.Bind(this);

            foreach (Symbol symbol in TypeSymbolsToBind)
            {
                if (symbol.IsBound)
                    continue;
                
                using (OpenMemberBindScope(symbol))
                    symbol.Bind(this);
            }
        }

        protected override void OnSymbolRetrieved(Symbol symbol)
        {
            if (_currentBindSymbol != null && !symbol.IsExtern)
                _currentReferencedSymbols.Add(symbol);
        }

        private TypeSymbol MakeArraySymbol(TypeSymbol element, int depth)
        {
            if (element is TypeParameterSymbol)
                return element;
            
            TypeSymbol currentSymbol = element;

            while (depth-- > 0)
                currentSymbol = currentSymbol.MakeArrayType(this);

            return currentSymbol;
        }

        protected override Symbol RedirectTypeSymbol(Symbol symbol)
        {
            if (_currentBindSymbol == null) 
                return symbol;

            TypeSymbol typeSymbol = symbol as TypeSymbol;
                
            if (typeSymbol == null)
                return symbol;
            
            int arrayDepth = 0;

            while (typeSymbol.IsArray)
            {
                arrayDepth++;
                typeSymbol = typeSymbol.ElementType;
            }

            if (arrayDepth > 0)
                return MakeArraySymbol((TypeSymbol)RedirectTypeSymbol(typeSymbol), arrayDepth);

            if (symbol is TypeParameterSymbol parameterSymbol)
            {
                int typeIdx = -1;

                TypeSymbol containingType = _currentBindSymbol.ContainingType;

                if (containingType.TypeArguments.Length > 0 && containingType.OriginalSymbol != null)
                {
                    TypeSymbol originalType = (TypeSymbol)containingType.OriginalSymbol;

                    for (int i = 0; i < originalType.TypeArguments.Length; ++i)
                    {
                        if (parameterSymbol.Equals(originalType.TypeArguments[i]))
                        {
                            typeIdx = i;
                            break;
                        }
                    }

                    if (typeIdx != -1)
                    {
                        return containingType.TypeArguments[typeIdx];
                    }
                }

                if (_currentBindSymbol is MethodSymbol currentMethodSymbol)
                {
                    if (currentMethodSymbol.TypeArguments.Length > 0 && currentMethodSymbol.OriginalSymbol != null)
                    {
                        MethodSymbol originalMethod = (MethodSymbol)currentMethodSymbol.OriginalSymbol;

                        for (int i = 0; i < originalMethod.TypeArguments.Length; ++i)
                        {
                            if (!parameterSymbol.Equals(originalMethod.TypeArguments[i])) continue;
                            typeIdx = i;
                            break;
                        }

                        if (typeIdx != -1)
                        {
                            return currentMethodSymbol.TypeArguments[typeIdx];
                        }
                    }
                }
            }
            else if (typeSymbol.IsGenericType && !typeSymbol.IsFullyConstructedGeneric)
            {
                TypeSymbol[] typeArguments = typeSymbol.TypeArguments.Select(e => (TypeSymbol)RedirectTypeSymbol(e)).ToArray();
            
                if (!typeArguments.Any(e => e is TypeParameterSymbol))
                {
                    return ((TypeSymbol)typeSymbol.OriginalSymbol).ConstructGenericType(this, typeArguments);
                }
            }

            return symbol;
        }

        protected override Symbol RedirectParameterSymbol(Symbol symbol)
        {
            if (_currentBindSymbol == null || !(symbol is ParameterSymbol))
                return symbol;

            if (_currentBindSymbol is MethodSymbol methodSymbol && methodSymbol.RoslynSymbol != methodSymbol.RoslynSymbol.OriginalDefinition)
            {
                foreach (ParameterSymbol methodParam in methodSymbol.Parameters)
                {
                    if (methodParam.OriginalSymbol.Equals(symbol))
                        return methodParam;
                }
            }
            
            return symbol;
        }

        protected override Symbol RedirectMethodSymbol(Symbol symbol)
        {
            if (symbol is MethodSymbol methodSymbol)
            {
                // If the containing type is a generic type, we need to redirect the method type to the current type
                if (methodSymbol.ContainingType != null && methodSymbol.ContainingType.IsGenericType && _currentBindSymbol != null)
                {
                    TypeSymbol containingType = (TypeSymbol)RedirectTypeSymbol(methodSymbol.ContainingType);
                    
                    // Compare parameter types to see if we can find a matching method
                    MethodSymbol foundSymbol = containingType.GetMembers<MethodSymbol>(methodSymbol.Name, this).FirstOrDefault(e => e.OriginalSymbol != null && (e.OriginalSymbol.Equals(methodSymbol) || e.OriginalSymbol.Equals(methodSymbol.OriginalSymbol)));
                    
                    if (foundSymbol != null)
                    {
                        methodSymbol = foundSymbol;
                    }
                }
                
                if (methodSymbol.IsGenericMethod && methodSymbol.IsUntypedGenericMethod)
                {
                    TypeSymbol[] typeSymbols = methodSymbol.TypeArguments.Select(e => (TypeSymbol)RedirectTypeSymbol(e)).ToArray();
                    
                    if (!typeSymbols.Any(e => e is TypeParameterSymbol))
                    {
                        return methodSymbol.ConstructGenericMethod(this, typeSymbols);
                    }
                }

                return methodSymbol;
            }

            return symbol;
        }

        protected override Symbol RedirectFieldSymbol(Symbol symbol)
        {
            if (_currentBindSymbol != null &&
                symbol is FieldSymbol fieldSymbol &&
                fieldSymbol.ContainingType.IsGenericType)
            {
                TypeSymbol containingType = (TypeSymbol)RedirectTypeSymbol(fieldSymbol.ContainingType);
                FieldSymbol foundSymbol = containingType.GetMember<FieldSymbol>(fieldSymbol.Name, this);
                
                if (foundSymbol != null)
                    return foundSymbol;
            }

            return symbol;
        }
        
        protected override Symbol RedirectPropertySymbol(Symbol symbol)
        {
            if (_currentBindSymbol != null &&
                symbol is PropertySymbol propertySymbol &&
                propertySymbol.ContainingType.IsGenericType)
            {
                TypeSymbol containingType = (TypeSymbol)RedirectTypeSymbol(propertySymbol.ContainingType);
                PropertySymbol foundSymbol = containingType.GetMember<PropertySymbol>(propertySymbol.Name, this);
                
                if (foundSymbol != null)
                    return foundSymbol;
            }

            return symbol;
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
                context.CurrentNode = context._currentBindSymbol.RoslynSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            }
            
            public void Dispose()
            {
                context.FinishSymbolBind();
            }
        }
    }
}
