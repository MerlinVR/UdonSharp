
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UdonSharp
{
    public class NamespaceVisitor : CSharpSyntaxWalker
    {
        private ResolverContext resolver;
        private ASTVisitorContext visitorContext;

        public NamespaceVisitor(ResolverContext resolverIn)
        {
            resolver = resolverIn;
            visitorContext = new ASTVisitorContext(resolver, new SymbolTable(resolver, null), new LabelTable());
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken(node.Identifier.ValueText);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            using (ExpressionCaptureScope namespaceCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Name);

                if (!namespaceCapture.IsNamespace())
                    throw new System.Exception("Did not capture a valid namespace");

                resolver.AddNamespace(namespaceCapture.captureNamespace);
            }
        }
    }
}

