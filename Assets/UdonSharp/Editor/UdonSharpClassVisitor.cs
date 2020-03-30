
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp
{
    public class ClassVisitor : CSharpSyntaxWalker
    {
        public ClassDefinition classDefinition { get; private set; }
        private MethodVisitor methodVisitor;

        private ASTVisitorContext visitorContext;

        private int classCount = 0;

        private Stack<string> namespaceStack = new Stack<string>();

        public ClassVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable)
            : base(SyntaxWalkerDepth.Node)
        {
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable);
            methodVisitor = new MethodVisitor(resolver, rootTable, labelTable);

            classDefinition = new ClassDefinition();
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            base.VisitCompilationUnit(node);

            methodVisitor.Visit(node);

            classDefinition.methodDefinitions = methodVisitor.definedMethods;
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            string[] namespaces = node.Name.ToFullString().TrimEnd('\r', '\n', ' ').Split('.');

            foreach (string currentNamespace in namespaces)
                namespaceStack.Push(currentNamespace);

            base.VisitNamespaceDeclaration(node);

            for (int i = 0; i < namespaces.Length; ++i)
                namespaceStack.Pop();
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (++classCount > 1)
                throw new System.NotSupportedException("Only one class declaration per file is currently supported by UdonSharp");

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

                classDefinition.userClassType = classTypeCapture.captureType;
            }

            base.VisitClassDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            bool isPublic = node.Modifiers.HasModifier("public");

            System.Type fieldType = null;

            using (ExpressionCaptureScope fieldTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Declaration.Type);

                fieldType = fieldTypeCapture.captureType;
            }

            foreach (VariableDeclaratorSyntax variableDeclarator in node.Declaration.Variables)
            {
                SymbolDefinition newSymbol = visitorContext.topTable.CreateNamedSymbol(variableDeclarator.Identifier.ValueText, fieldType, isPublic ? SymbolDeclTypeFlags.Public : SymbolDeclTypeFlags.Private);

                classDefinition.fieldDefinitions.Add(new FieldDefinition(newSymbol));
            }
        }

        public override void VisitArrayType(ArrayTypeSyntax node)
        {
            using (ExpressionCaptureScope arrayTypeCaptureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                Visit(node.ElementType);

                for (int i = 0; i < node.RankSpecifiers.Count; ++i)
                    arrayTypeCaptureScope.MakeArrayType();
            }
        }

        public override void VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
        {
            foreach (ExpressionSyntax size in node.Sizes)
                Visit(size);
        }

        #region Resolution boilerplate
        // Boilerplate to have resolution work correctly
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            using (ExpressionCaptureScope namespaceCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Name);

                if (!namespaceCapture.IsNamespace())
                    throw new System.Exception("Did not capture a valid namespace");

                visitorContext.resolverContext.AddNamespace(namespaceCapture.captureNamespace);
            }
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken(node.Identifier.ValueText);
        }

        public override void VisitPredefinedType(PredefinedTypeSyntax node)
        {
            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken(node.Keyword.ValueText);
        }
        #endregion
    }
}
