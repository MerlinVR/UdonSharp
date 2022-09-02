
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Core;

namespace UdonSharp.Compiler.Symbols
{
    internal sealed class ImportedUdonSharpTypeSymbol : TypeSymbol
    {
        public ImportedUdonSharpTypeSymbol(INamedTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol.TypeKind == TypeKind.Enum)
                UdonType = context.GetTypeSymbol(sourceSymbol.EnumUnderlyingType).UdonType;
            else
                UdonType = context.GetTypeSymbol(typeof(object[])).UdonType;
        }
        
        public ImportedUdonSharpTypeSymbol(IArrayTypeSymbol sourceSymbol, AbstractPhaseContext context)
            : base(sourceSymbol, context)
        {
            if (sourceSymbol.ElementType.TypeKind == TypeKind.Enum)
            {
                UdonType = (ExternTypeSymbol)context.GetTypeSymbol(((INamedTypeSymbol) sourceSymbol.ElementType).EnumUnderlyingType).UdonType.MakeArrayType(context);
            }
            else
            {
                UdonType = context.GetTypeSymbol(typeof(object[])).UdonType;
            }
        }

        protected override Symbol CreateSymbol(ISymbol roslynSymbol, AbstractPhaseContext context)
        {
            switch (roslynSymbol)
            {
                case null:
                    throw new System.NullReferenceException("Source symbol cannot be null");
                case IMethodSymbol methodSymbol:
                    return MakeMethodSymbol(methodSymbol, context);
                case IFieldSymbol fieldSymbol:
                    return new ImportedUdonSharpFieldSymbol(fieldSymbol, context);
                case ILocalSymbol localSymbol:
                    return new LocalSymbol(localSymbol, context);
                case IParameterSymbol parameterSymbol:
                    return new ParameterSymbol(parameterSymbol, context);
                case ITypeSymbol typeSymbol:
                    throw new NotSupportedException("Nested type declarations are not currently supported by U#", typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation());
                    // return context.GetTypeSymbol(typeSymbol);
            }
            
            throw new System.NotImplementedException($"Handling for Symbol {roslynSymbol} is not implemented");
        }

        private static Symbol MakeMethodSymbol(IMethodSymbol methodSymbol, AbstractPhaseContext context)
        {
            if (methodSymbol.MethodKind == MethodKind.BuiltinOperator &&
                methodSymbol.Parameters.Length == 2 && 
                methodSymbol.Parameters[0].Type.TypeKind == TypeKind.Enum &&
                context.GetTypeSymbol(methodSymbol.ReturnType) == context.GetTypeSymbol(SpecialType.System_Boolean))
            {
                TypeSymbol parameterType = context.GetTypeSymbol(methodSymbol.Parameters[0].Type);

                BuiltinOperatorType operatorType;
                if (methodSymbol.Name == "op_Equality")
                    operatorType = BuiltinOperatorType.Equality;
                else
                    operatorType = BuiltinOperatorType.Inequality;
                
                return new ExternSynthesizedOperatorSymbol(operatorType, parameterType.UdonType, context);
            }

            return new ImportedUdonSharpMethodSymbol(methodSymbol, context);
        }
    }
}
