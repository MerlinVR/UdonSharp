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
        readonly Symbol owningSymbol;
        readonly BindContext bindContext;
        readonly SemanticModel symbolLookupModel;

        public BinderSyntaxVisitor(Symbol owningSymbol, BindContext context)
        {
            this.owningSymbol = owningSymbol;
            bindContext = context;

            symbolLookupModel = context.CompileContext.GetSemanticModel(owningSymbol.RoslynSymbol.DeclaringSyntaxReferences.First().SyntaxTree);
        }

        private void UpdateSyntaxNode(SyntaxNode node)
        {

        }

        private BoundExpression VisitExpression(SyntaxNode node)
        {
            return (BoundExpression)Visit(node);
        }

        private Symbol GetSymbol(SyntaxNode node)
        {
            return bindContext.GetSymbol(symbolLookupModel.GetSymbolInfo(node).Symbol);
        }

        public override BoundNode DefaultVisit(SyntaxNode node)
        {
            throw new NotSupportedException(LocStr.CE_NodeNotSupported, node.GetLocation(), node.Kind());
        }

        // This will only be visited from within a method declaration so it means it only gets hit if there's a local method declaration which is not supported.
        public override BoundNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);
            throw new NotSupportedException(LocStr.CE_LocalMethodsNotSupported, node.GetLocation());
        }

        public override BoundNode VisitBlock(BlockSyntax node)
        {
            UpdateSyntaxNode(node);

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
            UpdateSyntaxNode(node);
            return new BoundExpressionStatement(node.Expression, (BoundExpression)Visit(node.Expression));
        }

        public override BoundNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            throw new System.NotImplementedException();
        }

        public override BoundNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            UpdateSyntaxNode(node);
            throw new System.NotImplementedException();
        }
    }
}
