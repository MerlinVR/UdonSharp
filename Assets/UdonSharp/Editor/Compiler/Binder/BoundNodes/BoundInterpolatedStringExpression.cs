
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

            // todo: use non-params versions of Format when possible
            StringFormatMethod = ValueType.GetMembers<ExternMethodSymbol>("Format", context).First(e =>
                e.Parameters[0].Type == ValueType && e.Parameters[1].Type == ObjectArr);
        }

        public override Value EmitValue(EmitContext context)
        {
            Value interpolationArr = context.CreateGlobalInternalValue(ObjectArr);

            interpolationArr.DefaultValue = new object[InterpolationExpressions.Length];

            BoundAccessExpression arrayAccess = BoundAccessExpression.BindAccess(interpolationArr);

            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);

            using (context.InterruptAssignmentScope())
            {
                // This is quite wasteful for allocations in the compile, todo: look at caching these safely
                for (int i = 0; i < InterpolationExpressions.Length; ++i)
                {
                    BoundAccessExpression elementAccess = BoundAccessExpression.BindElementAccess(context, SyntaxNode,
                        arrayAccess,
                        new BoundExpression[]
                            {new BoundConstantExpression(new ConstantValue<int>(i), intType, SyntaxNode)});
                    context.EmitSet(elementAccess, InterpolationExpressions[i]);
                }
            }

            BoundInvocationExpression formatInvoke = BoundInvocationExpression.CreateBoundInvocation(context, SyntaxNode, StringFormatMethod, null, new BoundExpression[]{ BuiltStr, arrayAccess });

            return context.EmitValue(formatInvoke);
        }
    }
}
