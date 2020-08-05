using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp.Compiler
{
    public class MethodVisitor : UdonSharpSyntaxWalker
    {
        public List<MethodDefinition> definedMethods = new List<MethodDefinition>();

        public MethodVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable)
            : base(resolver, rootTable, labelTable)
        {
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
