using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Core;
using UdonSharp.Localization;

namespace UdonSharp.Compiler.Binder
{
    internal class BinderSyntaxVisitor : CSharpSyntaxVisitor<BoundNode>
    {
        readonly Symbol _owningSymbol;
        readonly BindContext _bindContext;
        readonly SemanticModel _symbolLookupModel;

        public BinderSyntaxVisitor(Symbol owningSymbol, BindContext context)
        {
            this._owningSymbol = owningSymbol;
            _bindContext = context;

            _symbolLookupModel = context.CompileContext.GetSemanticModel(owningSymbol.RoslynSymbol.DeclaringSyntaxReferences.First().SyntaxTree);
        }

        private void UpdateSyntaxNode(SyntaxNode node)
        {
        }

        public override BoundNode Visit(SyntaxNode node)
        {
            UpdateSyntaxNode(node);
            return base.Visit(node);
        }

        private BoundExpression VisitExpression(SyntaxNode node)
        {
            return (BoundExpression)Visit(node);
        }

        private Symbol GetSymbol(SyntaxNode node)
        {
            return _bindContext.GetSymbol(_symbolLookupModel.GetSymbolInfo(node).Symbol);
        }

        public override BoundNode DefaultVisit(SyntaxNode node)
        {
            throw new NotSupportedException(LocStr.CE_NodeNotSupported, node.GetLocation(), node.Kind());
        }

        // This will only be visited from within a method declaration so it means it only gets hit if there's a local method declaration which is not supported.
        public override BoundNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            throw new NotSupportedException(LocStr.CE_LocalMethodsNotSupported, node.GetLocation());
        }

        public override BoundNode VisitBlock(BlockSyntax node)
        {
            if (node.Statements == null || node.Statements.Count == 0)
                return new BoundBlock(node);

            BoundStatement[] boundStatements = new BoundStatement[node.Statements.Count];

            int statementCount = node.Statements.Count;
            for (int i = 0; i < statementCount; ++i)
            {
                BoundNode statement = Visit(node.Statements[i]);
                boundStatements[i] = (BoundStatement)statement;
            }

            return new BoundBlock(node, boundStatements);
        }

        public override BoundNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            return new BoundExpressionStatement(node.Expression, (BoundExpression)Visit(node.Expression));
        }

        public override BoundNode VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            IConstantValue constantValue = null;

            switch (node.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                    constantValue = (IConstantValue)System.Activator.CreateInstance(typeof(ConstantValue<>).MakeGenericType(node.Token.Value.GetType()), node.Token.Value);
                    break;
                case SyntaxKind.StringLiteralExpression:
                    constantValue = new ConstantValue<string>((string)node.Token.Value);
                    break;
                case SyntaxKind.CharacterLiteralExpression:
                    constantValue = new ConstantValue<char>((char)node.Token.Value);
                    break;
                case SyntaxKind.TrueLiteralExpression:
                    constantValue = new ConstantValue<bool>(true);
                    break;
                case SyntaxKind.FalseLiteralExpression:
                    constantValue = new ConstantValue<bool>(false);
                    break;
                case SyntaxKind.NullLiteralExpression:
                    constantValue = new ConstantValue<object>(null);
                    break;
                default:
                    return base.VisitLiteralExpression(node);
            }

            return new BoundConstantExpression(constantValue, node);
        }

        public override BoundNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            MethodSymbol methodSymbol = (MethodSymbol)GetSymbol(node);

            BoundExpression[] boundArguments = new BoundExpression[node.ArgumentList.Arguments.Count];

            for (int i = 0; i < boundArguments.Length; ++i)
            {
                boundArguments[i] = VisitExpression(node.ArgumentList.Arguments[i].Expression);
            }

            return BoundInvocationExpression.CreateBoundInvocation(node, methodSymbol, boundArguments);
        }

        public override BoundNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            throw new System.NotImplementedException();
        }
    }
}
