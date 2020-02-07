using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdonSharp
{
    public class MethodVisitor : CSharpSyntaxWalker
    {
        public List<MethodDefinition> definedMethods = new List<MethodDefinition>();
        private ASTVisitorContext visitorContext;
        
        public MethodVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable)
            : base(SyntaxWalkerDepth.Node)
        {
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable);
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

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            MethodDefinition methodDefinition = new MethodDefinition();

            methodDefinition.declarationFlags = node.Modifiers.HasModifier("public") ? MethodDeclFlags.Public : MethodDeclFlags.Private;
            methodDefinition.methodUdonEntryPoint = visitorContext.labelTable.GetNewJumpLabel("udonMethodEntryPoint");
            methodDefinition.methodUserCallStart = visitorContext.labelTable.GetNewJumpLabel("userMethodCallEntry");
            methodDefinition.methodReturnPoint = visitorContext.labelTable.GetNewJumpLabel("methodReturnPoint");
            
            methodDefinition.originalMethodName = node.Identifier.ValueText;
            methodDefinition.uniqueMethodName = methodDefinition.originalMethodName;
            visitorContext.resolverContext.ReplaceInternalEventName(ref methodDefinition.uniqueMethodName);

            // Resolve the type arguments
            using (ExpressionCaptureScope returnTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.ReturnType);

                if (returnTypeCapture.captureType != typeof(void))
                {
                    //methodDefinition.returnType = returnTypeCapture.captureType;
                    methodDefinition.returnSymbol = visitorContext.topTable.CreateNamedSymbol("returnValSymbol", returnTypeCapture.captureType, SymbolDeclTypeFlags.Internal);
                }
            }

            methodDefinition.parameters = new ParameterDefinition[node.ParameterList.Parameters.Count];

            for (int i = 0; i < node.ParameterList.Parameters.Count; ++i)
            {
                ParameterSyntax parameter = node.ParameterList.Parameters[i];

                ParameterDefinition paramDef = new ParameterDefinition();

                using (ExpressionCaptureScope paramTypeCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(parameter.Type);
                    paramDef.type = paramTypeCapture.captureType;
                    paramDef.symbolName = parameter.Identifier.ValueText;
                    paramDef.paramSymbol = visitorContext.topTable.CreateNamedSymbol(parameter.Identifier.ValueText, paramDef.type, SymbolDeclTypeFlags.Local);
                }

                methodDefinition.parameters[i] = paramDef;
            }

            definedMethods.Add(methodDefinition);
        }
    }
}
