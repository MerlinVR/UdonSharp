using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Symbols;
using UnityEngine;

namespace UdonSharp.Compiler
{
    /// <summary>
    /// An independent thread of work for binding a specific type, will collect all methods, fields, and properties in the given TypeSymbol and register them
    /// </summary>
    internal class TypeBindModule
    {
        public CompilationContext CompileContext { get; private set; }
        public TypeSymbol TypeToBind { get; private set; }

        public TypeBindModule(CompilationContext compileContext, TypeSymbol bindType)
        {
            CompileContext = compileContext;
            TypeToBind = bindType;
        }

        public void Bind(BindContext context)
        {
            TypeToBind.Bind(context);
        }
    }
}
