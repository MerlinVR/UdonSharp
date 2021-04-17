using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;


namespace UdonSharp.Compiler.Symbols
{
    internal class FieldSymbol : Symbol
    {
        readonly TypeSymbol sourceType;

        public TypeSymbol Type { get; private set; }
        public ExpressionSyntax InitializerSyntax { get; private set; }

        public FieldSymbol(TypeSymbol sourceType, IFieldSymbol sourceSymbol)
        {
            base.RoslynSymbol = sourceSymbol;
            this.sourceType = sourceType;
        }

        public new IFieldSymbol RoslynSymbol { get { return (IFieldSymbol)base.RoslynSymbol; } }
        public override Symbol DeclaringSymbol => sourceType;

        public override ImmutableArray<Symbol> GetDirectDependencies()
        {
            throw new System.NotImplementedException();
        }

        public override ImmutableArray<Symbol> GetDirectDependencies<T>()
        {
            throw new System.NotImplementedException();
        }

        bool _resolved = false;
        public override bool IsResolved => _resolved;

        public void Bind(BindModule module)
        {
            Type = module.CompileContext.GetTypeSymbol((INamedTypeSymbol)RoslynSymbol.Type);
            InitializerSyntax = (RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax() as VariableDeclaratorSyntax)?.Initializer?.Value;

            _resolved = true;
        }
    }
}
