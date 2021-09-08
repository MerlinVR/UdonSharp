
using System;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Symbols;

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
            
            Type systemType = arrayType.ElementType.UdonType.SystemType;
            if (systemType == typeof(UnityEngine.Object) ||
                systemType.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                arrayType = context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context);
            }

            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);
            MethodSymbol setMethod = new ExternSynthesizedMethodSymbol(context, "Set", arrayType,
                new[] {intType, arrayType.UdonType.ElementType}, null, false);
            MethodSymbol getMethod = new ExternSynthesizedMethodSymbol(context, "Get", arrayType,
                new[] {intType}, arrayType.UdonType.ElementType, false);

            return new SynthesizedPropertySymbol(context, getMethod, setMethod);
        }
    }
}
