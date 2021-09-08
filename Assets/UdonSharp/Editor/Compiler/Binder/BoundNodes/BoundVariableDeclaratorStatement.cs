
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;

namespace UdonSharp.Compiler.Binder
{
    internal sealed class BoundVariableDeclaratorStatement : BoundStatement
    {
        public Symbol UserSymbol { get; }
        public BoundExpression Initializer { get; }

        public BoundVariableDeclaratorStatement(VariableDeclaratorSyntax node, Symbol userSymbol, BoundExpression initializer)
            : base(node)
        {
            UserSymbol = userSymbol;
            Initializer = initializer;
        }

        public override void Emit(EmitContext context)
        {
            Value userValue = context.GetUserValue(UserSymbol);

            if (Initializer == null) return;
            
            context.EmitValueAssignment(userValue, Initializer);
        }
    }
}
