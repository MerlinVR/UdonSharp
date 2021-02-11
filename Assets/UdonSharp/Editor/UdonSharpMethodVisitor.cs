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

        readonly string[] builtinMethodNames = new string[]
        {
            "SendCustomEvent",
            "SendCustomNetworkEvent",
            "SetProgramVariable",
            "GetProgramVariable",
            "VRCInstantiate",
            "GetUdonTypeID",
            "GetUdonTypeName",
        };

        bool HasRecursiveMethodAttribute(MethodDeclarationSyntax node)
        {
            if (node.AttributeLists != null)
            {
                foreach (AttributeListSyntax attributeList in node.AttributeLists)
                {
                    foreach (AttributeSyntax attribute in attributeList.Attributes)
                    {
                        using (ExpressionCaptureScope attributeTypeCapture = new ExpressionCaptureScope(visitorContext, null))
                        {
                            attributeTypeCapture.isAttributeCaptureScope = true;
                            Visit(attribute.Name);

                            if (attributeTypeCapture.captureType == typeof(RecursiveMethodAttribute))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            MethodDefinition methodDefinition = new MethodDefinition();

            methodDefinition.declarationFlags = node.Modifiers.HasModifier("public") ? MethodDeclFlags.Public : MethodDeclFlags.Private;
            if (HasRecursiveMethodAttribute(node))
                methodDefinition.declarationFlags |= MethodDeclFlags.RecursiveMethod;

            methodDefinition.methodUdonEntryPoint = visitorContext.labelTable.GetNewJumpLabel("udonMethodEntryPoint");
            methodDefinition.methodUserCallStart = visitorContext.labelTable.GetNewJumpLabel("userMethodCallEntry");
            methodDefinition.methodReturnPoint = visitorContext.labelTable.GetNewJumpLabel("methodReturnPoint");

            string methodName = node.Identifier.ValueText;
            methodDefinition.originalMethodName = methodName;
            methodDefinition.uniqueMethodName = methodDefinition.originalMethodName;
            visitorContext.resolverContext.ReplaceInternalEventName(ref methodDefinition.uniqueMethodName);

            foreach (string builtinMethodName in builtinMethodNames)
            {
                if (methodName == builtinMethodName)
                    throw new System.Exception($"Cannot define method '{methodName}' with the same name as a built-in UdonSharpBehaviour method");
            }

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
                    paramDef.paramSymbol = visitorContext.topTable.CreateNamedSymbol(parameter.Identifier.ValueText, paramDef.type, SymbolDeclTypeFlags.Local | SymbolDeclTypeFlags.MethodParameter);
                }

                methodDefinition.parameters[i] = paramDef;
            }

            definedMethods.Add(methodDefinition);
        }
    }
}
