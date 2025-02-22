

using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal static class TypeSymbolFactory
    {
        public static TypeSymbol CreateSymbol(ITypeSymbol type, AbstractPhaseContext context)
        {
            switch (type)
            {
                case INamedTypeSymbol namedType when namedType.IsExternType():
                    return new ExternTypeSymbol(namedType, context);
                case INamedTypeSymbol namedType when namedType.IsUdonSharpBehaviour():
                    return new UdonSharpBehaviourTypeSymbol(namedType, context);
                case INamedTypeSymbol namedType:
                    return new ImportedUdonSharpTypeSymbol(namedType, context);
                // This is just used to be able to query all symbols on system/unity types that use pointer types
                // Udon does not actually support pointer types
                case IPointerTypeSymbol pointerType when pointerType.PointedAtType.IsExternType():
                    return new ExternTypeSymbol((INamedTypeSymbol)pointerType.PointedAtType, context);
                case ITypeParameterSymbol typeParameter:
                    return new TypeParameterSymbol(typeParameter, context);
                case IArrayTypeSymbol arrayType:
                {
                    IArrayTypeSymbol currentArrayType = arrayType;
                    while (currentArrayType.ElementType is IArrayTypeSymbol)
                    {
                        currentArrayType = currentArrayType.ElementType as IArrayTypeSymbol;
                    }

                    INamedTypeSymbol rootType;

                    if (currentArrayType.ElementType is INamedTypeSymbol namedSymbol)
                    {
                        rootType = namedSymbol;
                
                        if (rootType.IsExternType())
                            return new ExternTypeSymbol(arrayType, context);

                        if (rootType.IsUdonSharpBehaviour())
                            return new UdonSharpBehaviourTypeSymbol(arrayType, context);
                    }
                    else if (currentArrayType.ElementType is ITypeParameterSymbol)
                    {
                        return new TypeParameterSymbol(arrayType, context);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    return new ImportedUdonSharpTypeSymbol(arrayType, context);
                }
                default:
                    throw new System.ArgumentException($"Could not construct type for type symbol {type}");
            }
        }
    }
}
