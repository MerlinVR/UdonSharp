
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using UdonSharp.Compiler.Binder;

namespace UdonSharp.Compiler.Symbols
{
    internal class UdonSharpBehaviourTypeSymbol : TypeSymbol
    {
        public UdonSharpBehaviourTypeSymbol(INamedTypeSymbol sourceSymbol, BindContext context)
            : base(sourceSymbol, context)
        {
        }

        bool _bound = false;

        public override bool IsBound => _bound;

        public override void Bind(BindContext context)
        {
            if (_bound)
                return;

            ITypeSymbol typeSymbol = RoslynTypeSymbol;

            IEnumerable<ISymbol> members = typeSymbol.GetMembers().Where(e => !e.IsImplicitlyDeclared);

            var fieldSymbols = members.OfType<IFieldSymbol>().Select(e => (FieldSymbol)context.GetSymbol(e));
            var propertySymbols = members.OfType<IPropertySymbol>().Select(e => (PropertySymbol)context.GetSymbol(e));
            var methodSymbols = members.OfType<IMethodSymbol>().Where(e => e.MethodKind != MethodKind.PropertyGet && e.MethodKind != MethodKind.PropertySet).Select(e => (MethodSymbol)context.GetSymbol(e));

            foreach (var fieldSymbol in fieldSymbols)
                fieldSymbol.Bind(context);

            foreach (var propertySymbol in propertySymbols)
                propertySymbol.Bind(context);

            foreach (var methodSymbol in methodSymbols)
                methodSymbol.Bind(context);

            _bound = true;
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, BindContext context)
        {
            switch (roslynSymbol)
            {
                case null:
                    throw new System.NullReferenceException("Source symbol cannot be null");
                case IMethodSymbol methodSymbol:
                    return new UdonSharpBehaviourMethodSymbol(methodSymbol, context);
                case IFieldSymbol fieldSymbol:
                    return new UdonSharpBehaviourFieldSymbol(fieldSymbol, context);
                case IPropertySymbol propertySymbol:
                    return new UdonSharpBehaviourPropertySymbol(propertySymbol, context);
            }

            throw new System.InvalidOperationException("Failed to construct symbol for type");
        }
    }
}
