
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UdonSharp.Compiler
{
    public class UdonSharpFieldVisitor : UdonSharpSyntaxWalker
    {
        public HashSet<FieldDeclarationSyntax> fieldsWithInitializers;

        public UdonSharpFieldVisitor(HashSet<FieldDeclarationSyntax> fieldsWithInitializers, ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, List<ClassDefinition> classDefinitions, ClassDebugInfo classDebugInfo)
            : base(UdonSharpSyntaxWalkerDepth.ClassDefinitions, resolver, rootTable, labelTable, classDebugInfo)
        {
            this.fieldsWithInitializers = fieldsWithInitializers;
            visitorContext.externClassDefinitions = classDefinitions;
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            visitorContext.pauseDebugInfoWrite = true;

            base.VisitCompilationUnit(node);

            visitorContext.pauseDebugInfoWrite = false;
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            var variables = node.Declaration.Variables;
            for (int i = 0; i < variables.Count; ++i)
            {
                VariableDeclaratorSyntax variable = variables[i];

                if (variable.Initializer != null)
                {
                    fieldsWithInitializers.Add(node);
                }
            }

            if (node.Modifiers.HasModifier("static"))
                throw new System.NotSupportedException("Static fields are not yet supported by UdonSharp");

            UdonSyncMode fieldSyncMode = GetSyncAttributeValue(node);

            List<System.Attribute> fieldAttributes = GetFieldAttributes(node);


            bool isPublic = (node.Modifiers.Any(SyntaxKind.PublicKeyword) || fieldAttributes.Find(e => e is SerializeField) != null) && fieldAttributes.Find(e => e is System.NonSerializedAttribute) == null;
            bool isConst = (node.Modifiers.Any(SyntaxKind.ConstKeyword) || node.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));
            SymbolDeclTypeFlags flags = (isPublic ? SymbolDeclTypeFlags.Public : SymbolDeclTypeFlags.Private) |
                                        (isConst ? SymbolDeclTypeFlags.Readonly : 0);

            FieldChangeCallbackAttribute varChange = fieldAttributes.OfType<FieldChangeCallbackAttribute>().FirstOrDefault();

            List<SymbolDefinition> fieldSymbols = HandleVariableDeclaration(node.Declaration, flags, fieldSyncMode);
            foreach (SymbolDefinition fieldSymbol in fieldSymbols)
            {
                FieldDefinition fieldDefinition = new FieldDefinition(fieldSymbol);
                fieldDefinition.fieldAttributes = fieldAttributes;

                if (fieldSymbol.IsUserDefinedType())
                {
                    System.Type fieldType = fieldSymbol.userCsType;
                    while (fieldType.IsArray)
                        fieldType = fieldType.GetElementType();

                    foreach (ClassDefinition classDefinition in visitorContext.externClassDefinitions)
                    {
                        if (classDefinition.userClassType == fieldType)
                        {
                            fieldDefinition.userBehaviourSource = classDefinition.classScript;
                            break;
                        }
                    }
                }

                visitorContext.localFieldDefinitions.Add(fieldSymbol.symbolUniqueName, fieldDefinition);

                if (varChange != null)
                {
                    string targetProperty = varChange.CallbackPropertyName;

                    if (variables.Count > 1 || visitorContext.onModifyCallbackFields.ContainsKey(targetProperty))
                        throw new System.Exception($"Only one field may target property '{targetProperty}'");

                    PropertyDefinition foundProperty = visitorContext.definedProperties.FirstOrDefault(e => e.originalPropertyName == targetProperty);

                    if (foundProperty == null)
                        throw new System.ArgumentException($"Invalid target property for {nameof(FieldChangeCallbackAttribute)} on {node.Declaration}");

                    PropertyDefinition property = visitorContext.definedProperties.FirstOrDefault(e => e.originalPropertyName == targetProperty);
                    if (property == null)
                        throw new System.ArgumentException($"Property not found for '{targetProperty}'");

                    if (property.type != fieldDefinition.fieldSymbol.userCsType)
                        throw new System.Exception($"Types must match between property and variable change field");

                    visitorContext.onModifyCallbackFields.Add(targetProperty, fieldDefinition);
                }
            }
        }

        private UdonSyncMode GetSyncAttributeValue(FieldDeclarationSyntax node)
        {
            UdonSyncMode syncMode = UdonSyncMode.NotSynced;

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

                            if (attributeTypeCapture.captureType != typeof(UdonSyncedAttribute))
                                continue;

                            if (attribute.ArgumentList == null ||
                                attribute.ArgumentList.Arguments == null ||
                                attribute.ArgumentList.Arguments.Count == 0)
                            {
                                syncMode = UdonSyncMode.None;
                            }
                            else
                            {
                                using (ExpressionCaptureScope attributeCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                                {
                                    Visit(attribute.ArgumentList.Arguments[0].Expression);

                                    if (!attributeCaptureScope.IsEnum())
                                        throw new System.Exception("Invalid attribute argument provided for sync");

                                    syncMode = (UdonSyncMode)attributeCaptureScope.GetEnumValue();
                                }
                            }

                            break;
                        }
                    }

                    if (syncMode != UdonSyncMode.NotSynced)
                        break;
                }
            }

            return syncMode;
        }

        private List<System.Attribute> GetFieldAttributes(FieldDeclarationSyntax node)
        {
            List<System.Attribute> attributes = new List<System.Attribute>();

            if (node.AttributeLists != null)
            {
                foreach (AttributeListSyntax attributeList in node.AttributeLists)
                {
                    UpdateSyntaxNode(attributeList);

                    foreach (AttributeSyntax attribute in attributeList.Attributes)
                    {
                        using (ExpressionCaptureScope attributeTypeCapture = new ExpressionCaptureScope(visitorContext, null))
                        {
                            attributeTypeCapture.isAttributeCaptureScope = true;
                            Visit(attribute.Name);

                            System.Type captureType = attributeTypeCapture.captureType;

                            if (captureType == typeof(UdonSyncedAttribute))
                            {
                                UdonSyncMode syncMode = UdonSyncMode.NotSynced;

                                if (attribute.ArgumentList == null ||
                                    attribute.ArgumentList.Arguments == null ||
                                    attribute.ArgumentList.Arguments.Count == 0)
                                {
                                    syncMode = UdonSyncMode.None;
                                }
                                else
                                {
                                    using (ExpressionCaptureScope attributeCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                                    {
                                        Visit(attribute.ArgumentList.Arguments[0].Expression);

                                        if (!attributeCaptureScope.IsEnum())
                                            throw new System.Exception("Invalid attribute argument provided for sync");

                                        syncMode = (UdonSyncMode)attributeCaptureScope.GetEnumValue();
                                    }
                                }
                                attributes.Add(new UdonSyncedAttribute(syncMode));
                            }
                            else if (captureType != null)
                            {
                                try
                                {
                                    object attributeObject = null;

                                    if (attribute.ArgumentList == null ||
                                        attribute.ArgumentList.Arguments == null ||
                                        attribute.ArgumentList.Arguments.Count == 0)
                                    {
                                        attributeObject = System.Activator.CreateInstance(captureType);
                                    }
                                    else
                                    {
                                        // todo: requires constant folding to support decently
                                        object[] attributeArgs = new object[attribute.ArgumentList.Arguments.Count];

                                        for (int i = 0; i < attributeArgs.Length; ++i)
                                        {
                                            AttributeArgumentSyntax attributeArg = attribute.ArgumentList.Arguments[i];

                                            using (ExpressionCaptureScope attributeCapture = new ExpressionCaptureScope(visitorContext, null))
                                            {
                                                Visit(attributeArg);

                                                SymbolDefinition attrSymbol = attributeCapture.ExecuteGet();

                                                if (!attrSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant))
                                                {
                                                    throw new System.ArgumentException("Attributes do not support non-constant expressions");
                                                }

                                                attributeArgs[i] = attrSymbol.symbolDefaultValue;
                                            }
                                        }

                                        attributeObject = System.Activator.CreateInstance(captureType, attributeArgs);
                                    }

                                    if (attributeObject != null)
                                        attributes.Add((System.Attribute)attributeObject);
                                }
                                catch (System.Reflection.TargetInvocationException constructionException)
                                {
                                    throw constructionException.InnerException;
                                }
                            }
                        }
                    }
                }
            }

            return attributes;
        }
    }
}
