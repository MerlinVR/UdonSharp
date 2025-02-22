
using Microsoft.CodeAnalysis;
using UdonSharp.Core;
using UnityEngine;
using VRC.Udon;
using System.Linq;

namespace UdonSharp.Compiler.Symbols
{
    internal sealed class UdonSharpBehaviourTypeSymbol : TypeSymbol
    {
        public UdonSharpBehaviourTypeSymbol(INamedTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            UdonType = (ExternTypeSymbol)context.GetTypeSymbol(typeof(UdonBehaviour));
        }
        
        public UdonSharpBehaviourTypeSymbol(IArrayTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol.ElementType.TypeKind == TypeKind.Array)
                UdonType = (ExternTypeSymbol)context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context);
            else
                UdonType = (ExternTypeSymbol)context.GetTypeSymbol(typeof(Component[]));
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context)
        {
            switch (roslynSymbol)
            {
                case null:
                    throw new System.NullReferenceException("Source symbol cannot be null");
                case IMethodSymbol methodSymbol:
                    if (methodSymbol.IsStatic)
                        return new ImportedUdonSharpMethodSymbol(methodSymbol, context);
                        
                    return new UdonSharpBehaviourMethodSymbol(methodSymbol, context);
                case IFieldSymbol fieldSymbol:
                    return new UdonSharpBehaviourFieldSymbol(fieldSymbol, context);
                case IPropertySymbol propertySymbol:
                    return new UdonSharpBehaviourPropertySymbol(propertySymbol, context);
                case ILocalSymbol localSymbol:
                    return new LocalSymbol(localSymbol, context);
                case IParameterSymbol parameterSymbol:
                    return new ParameterSymbol(parameterSymbol, context);
                case ITypeSymbol typeSymbol:
                    throw new NotSupportedException("Nested type declarations are not currently supported by U#", typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation());
                    // return context.GetTypeSymbol(typeSymbol);
            }

            throw new System.InvalidOperationException("Failed to construct symbol for type");
        }
    }
}
