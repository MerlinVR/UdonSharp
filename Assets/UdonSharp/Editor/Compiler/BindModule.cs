using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UdonSharp.Compiler.Symbols;
using UnityEngine;

namespace UdonSharp.Compiler
{
    /// <summary>
    /// An independent thread of work for binding a specific type, will collect all methods, fields, and properties in the given TypeSymbol and register them
    /// </summary>
    internal class BindModule
    {
        public CompilationContext CompileContext { get; private set; }
        public TypeSymbol TypeToBind { get; private set; }

        public BindModule(CompilationContext compileContext, TypeSymbol bindType)
        {
            CompileContext = compileContext;
            TypeToBind = bindType;
        }

        public void Bind()
        {
            Binder.BinderSyntaxVisitor syntaxVisitor = new Binder.BinderSyntaxVisitor(this);

            IEnumerable<ISymbol> symbols = TypeToBind.RoslynTypeSymbol.GetMembers();
            IEnumerable<IFieldSymbol> fieldSymbols = symbols.OfType<IFieldSymbol>();
            IEnumerable<IPropertySymbol> propertySymbols = symbols.OfType<IPropertySymbol>();
            IEnumerable<IMethodSymbol> methodSymbols = symbols.OfType<IMethodSymbol>();

            HashSet<INamedTypeSymbol> fieldTypes = new HashSet<INamedTypeSymbol>();

            foreach (IFieldSymbol symbol in fieldSymbols)
            {
                FieldSymbol boundSymbol = TypeToBind.GetFieldSymbol(symbol);
                boundSymbol.Bind(this);
            }

            foreach (IPropertySymbol symbol in propertySymbols)
            {
                PropertySymbol boundSymbol = TypeToBind.GetPropertySymbol(symbol);
                boundSymbol.Bind(this);
            }

            foreach (IMethodSymbol symbol in methodSymbols)
            {

            }
        }
    }
}
