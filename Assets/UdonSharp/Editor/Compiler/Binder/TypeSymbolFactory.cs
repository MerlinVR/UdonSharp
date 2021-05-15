

using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal static class TypeSymbolFactory
    {
        public static TypeSymbol CreateSymbol(ITypeSymbol type, BindContext context)
        {
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsExternType())
                    return new ExternTypeSymbol(namedType, context);
                else if (namedType.IsUdonSharpBehaviour())
                    return new UdonSharpBehaviourTypeSymbol(namedType, context);
                else
                    return new ImportedUdonSharpTypeSymbol(namedType, context);
            }
            else if (type is IArrayTypeSymbol arrayType)
            {
                return new ArrayTypeSymbol(arrayType, context);
            }

            throw new System.ArgumentException($"Could not construct type for type symbol {type}");
        }
    }
}
