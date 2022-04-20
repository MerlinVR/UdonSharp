
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundInterpolatedStringExpression : BoundExpression
    {
        private BoundConstantExpression BuiltStr { get; }
        private BoundExpression[] InterpolationExpressions { get; }
        
        private ExternMethodSymbol StringFormatMethod { get; }
        
        public override TypeSymbol ValueType { get; }
        
        private TypeSymbol ObjectArr { get; }

        public BoundInterpolatedStringExpression(InterpolatedStringExpressionSyntax node, string builtStr, BoundExpression[] interpolatedExpressions, BindContext context)
            : base(node)
        {
            ValueType = context.GetTypeSymbol(SpecialType.System_String);
            
            BuiltStr = new BoundConstantExpression(new ConstantValue<string>(builtStr), ValueType, node);
            InterpolationExpressions = interpolatedExpressions;

            ObjectArr = context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context);

            if (interpolatedExpressions.Length > 3)
            {
                StringFormatMethod = ValueType.GetMembers<ExternMethodSymbol>("Format", context).First(e =>
                    e.Parameters[0].Type == ValueType && e.Parameters[1].Type == ObjectArr);
            }
            else
            {
                StringFormatMethod = ValueType.GetMembers<ExternMethodSymbol>("Format", context).First(e =>
                    e.Parameters[0].Type == ValueType && e.Parameters.Length == interpolatedExpressions.Length + 1);
            }
        }

        public override Value EmitValue(EmitContext context)
        {
            BoundInvocationExpression formatInvoke;

            if (InterpolationExpressions.Length > 3)
            {
                BoundConstArrayCreationExpression interpolationArray =
                    new BoundConstArrayCreationExpression(SyntaxNode, ObjectArr, InterpolationExpressions);

                BoundAccessExpression arrayAccess =
                    BoundAccessExpression.BindAccess(context.EmitValue(interpolationArray));

                formatInvoke = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, StringFormatMethod,
                    null, new BoundExpression[] { BuiltStr, arrayAccess });
            }
            else
            {
                formatInvoke = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, StringFormatMethod,
                    null, new BoundExpression[] {BuiltStr}.Concat(InterpolationExpressions).ToArray());
            }

            return context.EmitValue(formatInvoke);
        }
    }
}
