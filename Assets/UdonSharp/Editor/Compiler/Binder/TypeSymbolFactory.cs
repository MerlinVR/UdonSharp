

using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal static class TypeSymbolFactory
    {
        public static TypeSymbol CreateSymbol(ITypeSymbol type, AbstractPhaseContext context)
        {
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsExternType())
                    return new ExternTypeSymbol(namedType, context);
                if (namedType.IsUdonSharpBehaviour())
                    return new UdonSharpBehaviourTypeSymbol(namedType, context);
                
                return new ImportedUdonSharpTypeSymbol(namedType, context);
            }

            // This is just used to be able to query all symbols on system/unity types that use pointer types
            // Udon does not actually support pointer types
            if (type is IPointerTypeSymbol pointerType)
            {
                if (pointerType.PointedAtType.IsExternType())
                    return new ExternTypeSymbol((INamedTypeSymbol)pointerType.PointedAtType, context);
            }

            if (type is IArrayTypeSymbol arrayType)
            {
                IArrayTypeSymbol currentArrayType = arrayType;
                while (currentArrayType.ElementType is IArrayTypeSymbol)
                {
                    currentArrayType = currentArrayType.ElementType as IArrayTypeSymbol;
                }

                INamedTypeSymbol rootType = (INamedTypeSymbol)currentArrayType.ElementType;
                
                if (rootType.IsExternType())
                    return new ExternTypeSymbol(arrayType, context);

                if (rootType.IsUdonSharpBehaviour())
                    return new UdonSharpBehaviourTypeSymbol(arrayType, context);

                return new ImportedUdonSharpTypeSymbol(arrayType, context);
            }

            throw new System.ArgumentException($"Could not construct type for type symbol {type}");
        }
    }
}
