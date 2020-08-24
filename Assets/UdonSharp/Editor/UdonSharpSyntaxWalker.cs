
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp.Compiler
{
    public class UdonSharpSyntaxWalker : CSharpSyntaxWalker
    {
        public enum UdonSharpSyntaxWalkerDepth
        {
            Class,
            ClassDefinitions,
            ClassMemberBodies,
        }

        public ASTVisitorContext visitorContext;

        protected Stack<string> namespaceStack = new Stack<string>();

        UdonSharpSyntaxWalkerDepth syntaxWalkerDepth;

        public UdonSharpSyntaxWalker(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, ClassDebugInfo classDebugInfo = null)
            : base(SyntaxWalkerDepth.Node)
        {
            syntaxWalkerDepth = UdonSharpSyntaxWalkerDepth.ClassMemberBodies;
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable, classDebugInfo);
        }

        public UdonSharpSyntaxWalker(UdonSharpSyntaxWalkerDepth depth, ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, ClassDebugInfo classDebugInfo = null)
            : base(SyntaxWalkerDepth.Node)
        {
            syntaxWalkerDepth = depth;
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable, classDebugInfo);
        }

        protected void UpdateSyntaxNode(SyntaxNode node)
        {
            visitorContext.currentNode = node;

            if (visitorContext.debugInfo != null && !visitorContext.pauseDebugInfoWrite)
                visitorContext.debugInfo.UpdateSyntaxNode(node);
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            UpdateSyntaxNode(node);
            base.DefaultVisit(node);
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);
            string[] namespaces = node.Name.ToFullString().TrimEnd('\r', '\n', ' ').Split('.');

            foreach (string currentNamespace in namespaces)
                namespaceStack.Push(currentNamespace);

            foreach (UsingDirectiveSyntax usingDirective in node.Usings)
                Visit(usingDirective);

            foreach (MemberDeclarationSyntax memberDeclaration in node.Members)
                Visit(memberDeclaration);

            for (int i = 0; i < namespaces.Length; ++i)
                namespaceStack.Pop();
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);
            using (ExpressionCaptureScope classTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                foreach (string namespaceToken in namespaceStack.Reverse())
                {
                    classTypeCapture.ResolveAccessToken(namespaceToken);

                    if (classTypeCapture.IsNamespace())
                        visitorContext.resolverContext.AddNamespace(classTypeCapture.captureNamespace);
                }

                classTypeCapture.ResolveAccessToken(node.Identifier.ValueText);

                if (!classTypeCapture.IsType())
                    throw new System.Exception($"User type {node.Identifier.ValueText} could not be found");
            }

            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassDefinitions || 
                syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitClassDeclaration(node);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitVariableDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitPropertyDeclaration(node);
        }

        public override void VisitArrayType(ArrayTypeSyntax node)
        {
            UpdateSyntaxNode(node);
            using (ExpressionCaptureScope arrayTypeCaptureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                Visit(node.ElementType);

                for (int i = 0; i < node.RankSpecifiers.Count; ++i)
                    arrayTypeCaptureScope.MakeArrayType();
            }
        }

        public override void VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
        {
            UpdateSyntaxNode(node);
            foreach (ExpressionSyntax size in node.Sizes)
                Visit(size);
        }
        
        // Boilerplate to have resolution work correctly
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            UpdateSyntaxNode(node);
            using (ExpressionCaptureScope namespaceCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                if (node.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
                    throw new System.NotSupportedException("UdonSharp does not yet support static using statements");

                if (node.Alias.IsKind(SyntaxKind.AliasKeyword))
                    throw new System.NotSupportedException("UdonSharp does not yet support namespace aliases");

                Visit(node.Name);

                if (!namespaceCapture.IsNamespace())
                    throw new System.Exception("Did not capture a valid namespace");

                visitorContext.resolverContext.AddNamespace(namespaceCapture.captureNamespace);
            }
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            UpdateSyntaxNode(node);
            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken(node.Identifier.ValueText);
        }

        public override void VisitPredefinedType(PredefinedTypeSyntax node)
        {
            UpdateSyntaxNode(node);
            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken(node.Keyword.ValueText);
        }
    }
}
