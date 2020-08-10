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
            : base(UdonSharpSyntaxWalkerDepth.ClassDefinitions, resolver, rootTable, labelTable)
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
                    methodDefinition.returnSymbol = visitorContext.topTable.CreateNamedSymbol("returnValSymbol", returnTypeCapture.captureType, SymbolDeclTypeFlags.Internal);

                    if (!visitorContext.resolverContext.IsValidUdonType(returnTypeCapture.captureType))
                        throw new System.NotSupportedException($"Udon does not support return values of type '{returnTypeCapture.captureType.Name}' yet");
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

                    if (!paramTypeCapture.IsType())
                        throw new System.TypeLoadException($"The type or namespace name '{parameter.Type}' could not be found (are you missing a using directive?)");

                    if (!visitorContext.resolverContext.IsValidUdonType(paramTypeCapture.captureType))
                        throw new System.NotSupportedException($"Udon does not support method parameters of type '{paramTypeCapture.captureType.Name}' yet");

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
