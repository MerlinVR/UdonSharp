
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp.Compiler
{
    public class ClassVisitor : UdonSharpSyntaxWalker
    {
        public ClassDefinition classDefinition { get; private set; }
        private MethodVisitor methodVisitor;

        private int classCount = 0;

        public ClassVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable)
            : base(resolver, rootTable, labelTable)
        {
            methodVisitor = new MethodVisitor(resolver, rootTable, labelTable);

            classDefinition = new ClassDefinition();
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            base.VisitCompilationUnit(node);

            methodVisitor.Visit(node);

            classDefinition.methodDefinitions = methodVisitor.definedMethods;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            base.VisitClassDeclaration(node);

            using (ExpressionCaptureScope classTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                foreach (string namespaceToken in namespaceStack.Reverse())
                {
                    classTypeCapture.ResolveAccessToken(namespaceToken);
                }

                classTypeCapture.ResolveAccessToken(node.Identifier.ValueText);

                if (!classTypeCapture.IsType())
                    throw new System.Exception($"User type {node.Identifier.ValueText} could not be found");

                classDefinition.userClassType = classTypeCapture.captureType;
            }
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
    }
}
