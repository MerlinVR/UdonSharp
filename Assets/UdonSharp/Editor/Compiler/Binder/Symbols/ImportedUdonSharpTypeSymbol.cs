
using System.Linq;
using Microsoft.CodeAnalysis;

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
            }
            
            throw new System.NotImplementedException();
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

            if (methodSymbol.IsGenericMethod)
            {
                if (methodSymbol.TypeArguments.Any(e => e is ITypeParameterSymbol))
                {
                    var typeArguments = methodSymbol.TypeArguments.Select(context.GetTypeSymbol).ToArray();
                    if (typeArguments.All(e => !(e is TypeParameterSymbol)))
                    {
                        var newMethod = new ImportedUdonSharpMethodSymbol(
                            methodSymbol.OriginalDefinition.Construct(typeArguments.Select(e => e.RoslynSymbol)
                                .ToArray()), context);

                        return newMethod;
                    }
                }
            }

            return new ImportedUdonSharpMethodSymbol(methodSymbol, context);
        }
    }
}
