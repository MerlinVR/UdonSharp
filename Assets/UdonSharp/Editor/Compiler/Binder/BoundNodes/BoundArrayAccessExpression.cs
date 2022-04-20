
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Compiler.Udon;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundArrayAccessExpression : BoundPropertyAccessExpression
    {
        public BoundArrayAccessExpression(SyntaxNode node, AbstractPhaseContext context, BoundExpression sourceExpression, BoundExpression[] indexerExpressions)
            : base(context, node, BuildProperty(context, sourceExpression), sourceExpression, indexerExpressions)
        {
        }

        public override TypeSymbol ValueType => SourceExpression.ValueType.ElementType;

        private static PropertySymbol BuildProperty(AbstractPhaseContext context, BoundExpression sourceExpression)
        {
            TypeSymbol arrayType = sourceExpression.ValueType;
            TypeSymbol elementType = arrayType.ElementType;
            
            Type systemType = arrayType.ElementType.UdonType.SystemType;
            if (systemType == typeof(UnityEngine.Object) ||
                systemType.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                arrayType = context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context);
            }

            string arrayTypeName = CompilerUdonInterface.GetMethodTypeName(arrayType.UdonType);
            string arrayElementTypeName = CompilerUdonInterface.GetUdonTypeName(arrayType.UdonType.ElementType);
            
            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);
            MethodSymbol setMethod = new ExternSynthesizedMethodSymbol(context, $"{arrayTypeName}.__Set__SystemInt32_{arrayElementTypeName}__SystemVoid",
                new[] {intType, elementType}, null, false);
            MethodSymbol getMethod = new ExternSynthesizedMethodSymbol(context, $"{arrayTypeName}.__Get__SystemInt32__{arrayElementTypeName}",
                new[] {intType}, elementType, false);

            return new SynthesizedPropertySymbol(context, getMethod, setMethod);
        }
    }
}
