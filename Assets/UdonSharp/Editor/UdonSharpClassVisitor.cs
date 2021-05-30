
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
        private PropertyVisitor propertyVisitor;

        private int classCount = 0;

        public ClassVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable)
            : base(UdonSharpSyntaxWalkerDepth.ClassDefinitions, resolver, rootTable, labelTable)
        {
            methodVisitor = new MethodVisitor(resolver, rootTable, labelTable);
            propertyVisitor = new PropertyVisitor(resolver, rootTable, labelTable);

            classDefinition = new ClassDefinition();
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            base.VisitCompilationUnit(node);

            try
            {
                methodVisitor.Visit(node);
            }
            catch (System.Exception e)
            {
                visitorContext.currentNode = methodVisitor.visitorContext.currentNode;

                throw e;
            }

            classDefinition.methodDefinitions = methodVisitor.definedMethods;

            try
            {
                propertyVisitor.Visit(node);
            }
            catch (System.Exception e)
            {
                visitorContext.currentNode = propertyVisitor.visitorContext.currentNode;

                throw e;
            }

            classDefinition.propertyDefinitions = propertyVisitor.definedProperties;

            if (classCount == 0)
                throw new System.Exception($"No UdonSharpBehaviour class found in script file, you must define an UdonSharpBehaviour class in a script referenced by an UdonSharpProgramAsset");
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            if (++classCount > 1)
                throw new System.NotSupportedException("Only one class declaration per file is currently supported by UdonSharp");

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

            base.VisitClassDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            bool isPublic = node.Modifiers.HasModifier("public");

            System.Type fieldType = null;

            using (ExpressionCaptureScope fieldTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                UpdateSyntaxNode(node.Declaration.Type);
                Visit(node.Declaration.Type);

                fieldType = fieldTypeCapture.captureType;
            }

            if (fieldType == null)
                throw new System.Exception($"The type or namespace name '{node.Declaration.Type}' could not be found (are you missing a using directive?)");

            foreach (VariableDeclaratorSyntax variableDeclarator in node.Declaration.Variables)
            {
                SymbolDefinition newSymbol = visitorContext.topTable.CreateNamedSymbol(variableDeclarator.Identifier.ValueText, fieldType, isPublic ? SymbolDeclTypeFlags.Public : SymbolDeclTypeFlags.Private);

                classDefinition.fieldDefinitions.Add(new FieldDefinition(newSymbol));
            }
        }
    }
}
