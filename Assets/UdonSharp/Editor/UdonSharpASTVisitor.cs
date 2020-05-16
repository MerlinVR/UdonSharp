using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace UdonSharp
{
    public class ASTVisitorContext
    {
        public ResolverContext resolverContext;
        private Stack<SymbolTable> symbolTableStack;
        public LabelTable labelTable;
        public AssemblyBuilder uasmBuilder;
        public System.Type behaviourUserType;
        public List<ClassDefinition> externClassDefinitions;
        public Dictionary<string, FieldDefinition> localFieldDefinitions;

        public Stack<ExpressionCaptureScope> expressionCaptureStack = new Stack<ExpressionCaptureScope>();
        
        public List<MethodDefinition> definedMethods;

        // Tracking labels for the current function and flow control
        public JumpLabel returnLabel = null;
        public SymbolDefinition returnJumpTarget = null;
        public SymbolDefinition returnSymbol = null;
        public Stack<JumpLabel> continueLabelStack = new Stack<JumpLabel>();
        public Stack<JumpLabel> breakLabelStack = new Stack<JumpLabel>();

        public SymbolTable topTable { get { return symbolTableStack.Peek(); } }

        public ExpressionCaptureScope topCaptureScope { get { return expressionCaptureStack.Count > 0 ? expressionCaptureStack.Peek() : null; } }

        // Debugging info
        public SyntaxNode currentNode = null;
        public ClassDebugInfo debugInfo = null;

        public ASTVisitorContext(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTableIn, ClassDebugInfo debugInfoIn = null)
        {
            resolverContext = resolver;

            localFieldDefinitions = new Dictionary<string, FieldDefinition>();
            symbolTableStack = new Stack<SymbolTable>();
            symbolTableStack.Push(rootTable);

            //labelTable = new LabelTable();
            labelTable = labelTableIn;

            uasmBuilder = new AssemblyBuilder();

            if (debugInfoIn != null)
            {
                debugInfo = debugInfoIn;
                debugInfo.assemblyBuilder = uasmBuilder;
            }
        }

        public void PopTable()
        {
            if (symbolTableStack.Count == 1)
                throw new System.Exception("Cannot pop root table, mismatched scope entry and exit!");

            symbolTableStack.Pop();
        }

        public void PushTable(SymbolTable newTable)
        {
            if (newTable.parentSymbolTable != topTable)
                throw new System.ArgumentException("Parent symbol table is not valid for given context.");

            symbolTableStack.Push(newTable);
        }

        public void PushCaptureScope(ExpressionCaptureScope captureScope)
        {
            expressionCaptureStack.Push(captureScope);
        }

        public ExpressionCaptureScope PopCaptureScope()
        {
            if (expressionCaptureStack.Count == 0)
                return null;

            return expressionCaptureStack.Pop();
        }

        public SymbolDefinition requestedDestination
        {
            get
            {
                if (expressionCaptureStack.Count == 0)
                    return null;
                return topCaptureScope.requestedDestination;
            }
        }
    }

    /// <summary>
    /// This is where most of the work is done to convert a C# AST into intermediate UAsm
    /// </summary>
    public class ASTVisitor : CSharpSyntaxWalker
    {
        public ASTVisitorContext visitorContext { get; private set; }
        private Stack<string> namespaceStack = new Stack<string>();

        public ASTVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, List<MethodDefinition> methodDefinitions, List<ClassDefinition> externUserClassDefinitions, ClassDebugInfo debugInfo)
            : base(SyntaxWalkerDepth.Node)
        {
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable, debugInfo);
            visitorContext.returnJumpTarget = rootTable.CreateNamedSymbol("returnTarget", typeof(uint), SymbolDeclTypeFlags.Internal);
            visitorContext.definedMethods = methodDefinitions;
            visitorContext.externClassDefinitions = externUserClassDefinitions;
        }

        /// <summary>
        /// Called after running visit on the AST.
        /// Verifies that everything closed correctly
        /// </summary>
        public void VerifyIntegrity()
        {
            // Right now just check that the capture scopes are empty and no one failed to close a scope.
            Debug.Assert(visitorContext.topCaptureScope == null, "AST visitor capture scope state invalid!");
        }

        public string GetCompiledUasm()
        {
            return visitorContext.uasmBuilder.GetAssemblyStr(visitorContext.labelTable);
        }

        public string GetIDHeapVarName()
        {
            return visitorContext.topTable.CreateReflectionSymbol("udonTypeID", typeof(long), Internal.UdonSharpInternalUtility.GetTypeID(visitorContext.behaviourUserType)).symbolUniqueName;
        }

        public int GetExternStrCount()
        {
            return visitorContext.uasmBuilder.GetExternStrCount();
        }

        private void UpdateSyntaxNode(SyntaxNode node)
        {
            visitorContext.currentNode = node;

            if (visitorContext.debugInfo != null)
                visitorContext.debugInfo.UpdateSyntaxNode(node);
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            UpdateSyntaxNode(node);

            //Debug.Log(node.Kind().ToString());
            //base.DefaultVisit(node);

            throw new System.NotSupportedException($"UdonSharp does not currently support node type {node.Kind().ToString()}");
        }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            using (ExpressionCaptureScope scope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Expression);
            }
        }

        public override void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Expression);
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            UpdateSyntaxNode(node);

            foreach (UsingDirectiveSyntax usingDirective in node.Usings)
            {
                Visit(usingDirective);
            }

            foreach (MemberDeclarationSyntax member in node.Members)
            {
                Visit(member);
            }
        }

        // We don't care about namespaces at the moment. This may change in the future if we allow users to call custom behaviours.
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

        public override void VisitSimpleBaseType(SimpleBaseTypeSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Type);
        }

        public override void VisitBaseList(BaseListSyntax node)
        {
            UpdateSyntaxNode(node);

            foreach (BaseTypeSyntax type in node.Types)
            {
                using (ExpressionCaptureScope typeCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(type);

                    if (typeCaptureScope.captureType.IsInterface)
                    {
                        throw new System.NotSupportedException("UdonSharp does not yet support inheriting from interfaces");
                    }
                    else if (typeCaptureScope.captureType != typeof(UdonSharpBehaviour))
                    {
                        if (typeCaptureScope.captureType == typeof(MonoBehaviour))
                            throw new System.NotSupportedException("UdonSharp behaviours must inherit from 'UdonSharpBehaviour' instead of 'MonoBehaviour'");

                        throw new System.NotSupportedException("UdonSharp does not yet support inheriting from classes other than 'UdonSharpBehaviour'");
                    }
                }
            }
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);
            
            if (node.BaseList == null)
                throw new System.NotSupportedException("UdonSharp only supports classes that inherit from 'UdonSharpBehaviour' at the moment");
            
            using (ExpressionCaptureScope selfTypeCaptureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                foreach (string namespaceToken in namespaceStack.Reverse())
                {
                    selfTypeCaptureScope.ResolveAccessToken(namespaceToken);

                    if (selfTypeCaptureScope.IsNamespace())
                        visitorContext.resolverContext.AddNamespace(selfTypeCaptureScope.captureNamespace);
                }

                selfTypeCaptureScope.ResolveAccessToken(node.Identifier.ValueText);

                if (!selfTypeCaptureScope.IsType())
                    throw new System.Exception($"Could not get type of class {node.Identifier.ValueText}");

                visitorContext.behaviourUserType = selfTypeCaptureScope.captureType;
            }

            Visit(node.BaseList);

            visitorContext.topTable.CreateReflectionSymbol("udonTypeID", typeof(long), Internal.UdonSharpInternalUtility.GetTypeID(visitorContext.behaviourUserType));
            visitorContext.topTable.CreateReflectionSymbol("udonTypeName", typeof(string), Internal.UdonSharpInternalUtility.GetTypeName(visitorContext.behaviourUserType));

            visitorContext.uasmBuilder.AppendLine(".code_start", 0);

            foreach (MemberDeclarationSyntax member in node.Members)
            {
                Visit(member);
            }

            visitorContext.uasmBuilder.AppendLine(".code_end", 0);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            UpdateSyntaxNode(node);

            using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Name);

                if (!captureScope.IsNamespace())
                    throw new System.Exception("Captured scope is not a namespace!");

                //Debug.Log($"Added namespace: {captureScope.captureNamespace}");
                visitorContext.resolverContext.AddNamespace(captureScope.captureNamespace);
            }
        }

        public override void VisitEmptyStatement(EmptyStatementSyntax node)
        {
            UpdateSyntaxNode(node);
        }

        public override void VisitBlock(BlockSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolTable functionSymbolTable = new SymbolTable(visitorContext.resolverContext, visitorContext.topTable);
            visitorContext.PushTable(functionSymbolTable);

            foreach (StatementSyntax statement in node.Statements)
            {
                Visit(statement);
            }

            visitorContext.PopTable();
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("UdonSharp does not currently support constructors on UdonSharpBehaviours, use the Start() event to initialize instead.");
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("User property declarations are not yet supported by UdonSharp");
        }

        public override void VisitBaseExpression(BaseExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("Base type calling is not yet supported by UdonSharp");
        }

        public override void VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("Default expressions are not yet supported by UdonSharp");
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("Try/Catch/Finally is not supported by UdonSharp since Udon does not have a way to handle exceptions");
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("Try/Catch/Finally is not supported by UdonSharp since Udon does not have a way to handle exceptions");
        }

        public override void VisitFinallyClause(FinallyClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("Try/Catch/Finally is not supported by UdonSharp since Udon does not have a way to handle exceptions");
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("UdonSharp does not support throwing exceptions since Udon does not have support for exception throwing at the moment");
        }

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("UdonSharp does not support throwing exceptions since Udon does not have support for exception throwing at the moment");
        }

        public override void VisitIncompleteMember(IncompleteMemberSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.Exception("Incomplete member definition");
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Declaration);
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

        public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            System.Type arrayType = null;
            
            bool hasInitializer = node.Initializer != null;

            SymbolDefinition arraySymbol = visitorContext.requestedDestination;

            using (ExpressionCaptureScope arrayTypeScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);
                arrayType = arrayTypeScope.captureType;
            }

            using (ExpressionCaptureScope varCaptureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                if (arraySymbol == null)
                {
                    arraySymbol = visitorContext.topTable.CreateUnnamedSymbol(arrayType, SymbolDeclTypeFlags.Internal);
                }

                varCaptureScope.SetToLocalSymbol(arraySymbol);

                foreach (ArrayRankSpecifierSyntax rankSpecifierSyntax in node.Type.RankSpecifiers)
                {
                    if (rankSpecifierSyntax.Sizes.Count != 1)
                        throw new System.NotSupportedException("UdonSharp does not support multidimensional arrays at the moment");
                }

                SymbolDefinition arrayRankSymbol = null;

                ArrayRankSpecifierSyntax arrayRankSpecifier = node.Type.RankSpecifiers[0];

                if (arrayRankSpecifier.Sizes[0] is OmittedArraySizeExpressionSyntax) // Automatically deduce array size from the number of initialization expressions
                {
                    arrayRankSymbol = visitorContext.topTable.CreateConstSymbol(typeof(int), node.Initializer.Expressions.Count);
                }
                else
                {
                    using (ExpressionCaptureScope rankCapture = new ExpressionCaptureScope(visitorContext, null))
                    {
                        Visit(node.Type.RankSpecifiers[0]);
                        arrayRankSymbol = rankCapture.ExecuteGet();
                    }
                }

                if (hasInitializer && arrayRankSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) && ((int)arrayRankSymbol.symbolDefaultValue) != node.Initializer.Expressions.Count)
                {
                    UpdateSyntaxNode(node.Initializer);
                    throw new System.ArgumentException($"An array initializer of length '{(int)arrayRankSymbol.symbolDefaultValue}' is expected");
                }
                else if (hasInitializer && !arrayRankSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant))
                {
                    throw new System.ArgumentException("A constant value is expected");
                }

                using (ExpressionCaptureScope constructorCaptureScope = new ExpressionCaptureScope(visitorContext, null, arraySymbol))
                {
                    constructorCaptureScope.SetToMethods(arraySymbol.symbolCsType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

                    SymbolDefinition newArraySymbol = constructorCaptureScope.Invoke(new SymbolDefinition[] { arrayRankSymbol });
                    if (arraySymbol.IsUserDefinedType())
                        newArraySymbol.symbolCsType = arraySymbol.userCsType;

                    varCaptureScope.ExecuteSet(newArraySymbol);
                }

                if (hasInitializer)
                {
                    for (int i = 0; i < node.Initializer.Expressions.Count; ++i)
                    {
                        using (ExpressionCaptureScope arraySetIdxScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            arraySetIdxScope.SetToLocalSymbol(arraySymbol);
                            using (SymbolDefinition.COWValue arrayIndex = visitorContext.topTable.CreateConstSymbol(typeof(int), i).GetCOWValue(visitorContext.uasmBuilder, visitorContext.topTable))
                            {
                                arraySetIdxScope.HandleArrayIndexerAccess(arrayIndex);
                            }

                            using (ExpressionCaptureScope initializerExpressionCapture = new ExpressionCaptureScope(visitorContext, null))
                            {
                                Visit(node.Initializer.Expressions[i]);
                                arraySetIdxScope.ExecuteSetDirect(initializerExpressionCapture);
                            }
                        }
                    }
                }
            }
        }

        // Arrays that are created using only an initializer list `new [] { value, value, value }`
        public override void VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            var expressions = node.Initializer.Expressions;

            SymbolDefinition[] initializerSymbols = new SymbolDefinition[expressions.Count];

            for (int i = 0; i < expressions.Count; ++i)
            {
                ExpressionSyntax expression = expressions[i];

                using (ExpressionCaptureScope initializerExpressionScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(expression);
                    initializerSymbols[i] = initializerExpressionScope.ExecuteGet();
                }
            }

            HashSet<System.Type> symbolTypes = new HashSet<System.Type>();

            foreach (SymbolDefinition symbolDefinition in initializerSymbols)
            {
                symbolTypes.Add(symbolDefinition.userCsType);
            }

            System.Type arrayType = null;

            if (symbolTypes.Count == 1)
            {
                arrayType = symbolTypes.First();
            }
            else
            {
                HashSet<System.Type> validTypeSet = new HashSet<System.Type>();

                foreach (System.Type initializerType in symbolTypes)
                {
                    if (validTypeSet.Contains(initializerType))
                        continue;

                    bool isImplicitMatch = true;
                    foreach (System.Type otherType in symbolTypes) // Make sure all other symbols can be implicitly assigned to this type
                    {
                        isImplicitMatch &= initializerType.IsImplicitlyAssignableFrom(otherType);
                    }

                    if (isImplicitMatch)
                        validTypeSet.Add(initializerType);
                }

                if (validTypeSet.Count != 1)
                    throw new System.Exception("No best type found for implicitly-typed array");

                arrayType = validTypeSet.First();
            }

            SymbolDefinition arraySymbol = visitorContext.topTable.CreateUnnamedSymbol(arrayType.MakeArrayType(), SymbolDeclTypeFlags.Internal);

            using (ExpressionCaptureScope arraySetScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                arraySetScope.SetToLocalSymbol(arraySymbol);

                using (ExpressionCaptureScope constructorCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    constructorCaptureScope.SetToMethods(arraySymbol.symbolCsType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

                    SymbolDefinition newArraySymbol = constructorCaptureScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(int), initializerSymbols.Length) });
                    if (arraySymbol.IsUserDefinedType())
                        newArraySymbol.symbolCsType = arraySymbol.userCsType;

                    arraySetScope.ExecuteSet(newArraySymbol);
                }
            }

            for (int i = 0; i < initializerSymbols.Length; ++i)
            {
                using (ExpressionCaptureScope arrayIdxSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayIdxSetScope.SetToLocalSymbol(arraySymbol);
                    using (SymbolDefinition.COWValue arrayIndex = visitorContext.topTable.CreateConstSymbol(typeof(int), i).GetCOWValue(visitorContext.uasmBuilder, visitorContext.topTable))
                    {
                        arrayIdxSetScope.HandleArrayIndexerAccess(arrayIndex);
                    }
                    arrayIdxSetScope.ExecuteSet(initializerSymbols[i]);
                }
            }
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition requestedDestination = visitorContext.requestedDestination;

            using (ExpressionCaptureScope elementAccessExpression = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                Visit(node.Expression);

                if (node.ArgumentList.Arguments.Count != 1)
                    throw new System.NotSupportedException("UdonSharp does not support multidimensional accesses yet");

                using (ExpressionCaptureScope indexerCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.ArgumentList.Arguments[0]);
                    elementAccessExpression.HandleArrayIndexerAccess(indexerCaptureScope.ExecuteGetCOW(), requestedDestination);
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

        public override void VisitAttributeArgument(AttributeArgumentSyntax node)
        {
            UpdateSyntaxNode(node);
            Visit(node.Expression);
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
                        }
                    }
                }
            }

            return attributes;
        }

        void VerifySyncValidForType(System.Type typeToSync, UdonSyncMode syncMode)
        {
            if (syncMode == UdonSyncMode.NotSynced)
                return;

            if (!UdonSharpUtils.IsUdonSyncedType(typeToSync))
                throw new System.NotSupportedException($"Udon does not currently support syncing of the type '{UdonSharpUtils.PrettifyTypeName(typeToSync)}'");

            if (syncMode != UdonSyncMode.None && (typeToSync == typeof(string) || typeToSync == typeof(char)))
                throw new System.NotSupportedException($"Udon does not support tweening the synced type '{UdonSharpUtils.PrettifyTypeName(typeToSync)}'");
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            if (node.Modifiers.HasModifier("static"))
                throw new System.NotSupportedException("Static fields are not yet supported by UdonSharp");

            UdonSyncMode fieldSyncMode = GetSyncAttributeValue(node);

            List<System.Attribute> fieldAttributes = GetFieldAttributes(node);

            bool isPublic = (node.Modifiers.HasModifier("public") || fieldAttributes.Find(e => e is SerializeField) != null) && fieldAttributes.Find(e => e is System.NonSerializedAttribute) == null;

            List<SymbolDefinition> fieldSymbols = HandleVariableDeclaration(node.Declaration, isPublic ? SymbolDeclTypeFlags.Public : SymbolDeclTypeFlags.Private, fieldSyncMode);
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
            }
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            HandleVariableDeclaration(node, SymbolDeclTypeFlags.Local, UdonSyncMode.NotSynced);
        }

        public List<SymbolDefinition> HandleVariableDeclaration(VariableDeclarationSyntax node, SymbolDeclTypeFlags symbolType, UdonSyncMode syncMode)
        {
            UpdateSyntaxNode(node);

            bool isVar = node.Type.IsVar;

            System.Type variableType = null;

            if (!isVar)
            {
                using (ExpressionCaptureScope typeCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.Type);

                    if (!typeCapture.IsType())
                        throw new System.Exception($"The type or namespace name '{typeCapture.unresolvedAccessChain}' could not be found (are you missing a using directive?)");

                    variableType = typeCapture.captureType;
                }
            }

            List<SymbolDefinition> newSymbols = new List<SymbolDefinition>();

            foreach (VariableDeclaratorSyntax variableDeclarator in node.Variables)
            {
                SymbolDefinition newSymbol = null;

                string variableName = variableDeclarator.Identifier.ValueText;

                using (ExpressionCaptureScope symbolCreationScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    if (!isVar)
                    {
                        newSymbol = visitorContext.topTable.CreateNamedSymbol(variableDeclarator.Identifier.ValueText, variableType, symbolType);
                    }

                    // Run the initializer if it exists
                    // Todo: Run the set on the new symbol scope from within the initializer scope for direct setting
                    if (variableDeclarator.Initializer != null && symbolType.HasFlag(SymbolDeclTypeFlags.Local))
                    {
                        using (ExpressionCaptureScope initializerCapture = new ExpressionCaptureScope(visitorContext, null, newSymbol))
                        {
                            Visit(variableDeclarator.Initializer);

                            if (newSymbol == null)
                            {
                                // TODO: Find a way to determine the return type before generating initializer code, to avoid a copy on 'var' local initializers
                                variableType = initializerCapture.GetReturnType(true);
                                newSymbol = visitorContext.topTable.CreateNamedSymbol(variableDeclarator.Identifier.ValueText, variableType, symbolType);
                            }

                            symbolCreationScope.SetToLocalSymbol(newSymbol);
                            symbolCreationScope.ExecuteSet(initializerCapture.ExecuteGet());
                        }
                    }

                    
                    newSymbol.syncMode = syncMode;
                }

                VerifySyncValidForType(newSymbol.symbolCsType, syncMode);
                newSymbols.Add(newSymbol);
            }

            string udonTypeName = visitorContext.resolverContext.GetUdonTypeName(variableType);

            bool isUserDefinedType = UdonSharpUtils.IsUserDefinedType(variableType);

            if (!visitorContext.resolverContext.ValidateUdonTypeName(udonTypeName, UdonReferenceType.Variable) &&
                !visitorContext.resolverContext.ValidateUdonTypeName(udonTypeName, UdonReferenceType.Type) &&
                //VRC.Udon.Editor.UdonEditorManager.Instance.GetTypeFromTypeString(udonTypeName) != null && // I'd assume that this should work instead of the ValidateUdonTypeName calls, but it doesn't pick up a bunch of types
                !isUserDefinedType)
                throw new System.NotSupportedException($"Udon does not support variables of type '{variableType.Name}' yet");

            return newSymbols;
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

        // Not really strictly needed since the compiler for the normal C# will yell at people for us if they attempt to access something not valid for `this`
        public override void VisitThisExpression(ThisExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken("this");
        }

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("UdonSharp does not currently support null conditional operators");

            // Todo: actually handle if we add support for nullable types
            //using (ExpressionCaptureScope conditionalExpressionScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            //{
            //    Visit(node.Expression);

            //    SymbolDefinition expressionReturnValue = conditionalExpressionScope.ExecuteGet();

            //    JumpLabel notNullEndLabel = visitorContext.labelTable.GetNewJumpLabel("conditionNotNullEnd");

            //    using (ExpressionCaptureScope whenNotNullScope = new ExpressionCaptureScope(visitorContext, conditionalExpressionScope))
            //    {
            //        Visit(node.WhenNotNull);
            //    }

            //    visitorContext.uasmBuilder.AddJumpLabel(notNullEndLabel);
            //}

        }

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            System.Type capturedType = null;

            using (ExpressionCaptureScope typeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);

                capturedType = typeCapture.captureType;

                // Just throw a compile error for now instead of letting people get the typeof a type that won't exist in game
                if (capturedType == typeof(UdonSharpBehaviour) || capturedType.IsSubclassOf(typeof(UdonSharpBehaviour)))
                    throw new System.NotSupportedException("UdonSharp does not currently support using `typeof` on user defined types");
            }

            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.SetToLocalSymbol(visitorContext.topTable.CreateConstSymbol(typeof(System.Type), capturedType));
        }

        public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope, visitorContext.requestedDestination))
            {
                Visit(node.Value);
            }
        }

        public override void VisitConstructorInitializer(ConstructorInitializerSyntax node)
        {
            UpdateSyntaxNode(node);

            base.VisitConstructorInitializer(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            bool isSimpleAssignment = node.OperatorToken.Kind() == SyntaxKind.SimpleAssignmentExpression || node.OperatorToken.Kind() == SyntaxKind.EqualsToken;
            ExpressionCaptureScope topScope = visitorContext.topCaptureScope;

            SymbolDefinition rhsValue = null;

            // Set parent to allow capture propagation for stuff like x = y = z;
            using (ExpressionCaptureScope lhsCapture = new ExpressionCaptureScope(visitorContext, isSimpleAssignment ? topScope : null))
            {
                Visit(node.Left);

                // Done before anything modifies the state of the lhsCapture which will make this turn false
                bool needsCopy = lhsCapture.NeedsArrayCopySet();

                using (ExpressionCaptureScope rhsCapture = new ExpressionCaptureScope(visitorContext, null, isSimpleAssignment ? lhsCapture.destinationSymbolForSet : null))
                {
                    Visit(node.Right);

                    rhsValue = rhsCapture.ExecuteGet();
                }

                if (isSimpleAssignment)
                {
                    lhsCapture.ExecuteSet(rhsValue);
                }
                else
                {
                    List<MethodInfo> operatorMethods = new List<MethodInfo>();

                    switch (node.OperatorToken.Kind())
                    {
                        case SyntaxKind.AddAssignmentExpression:
                        case SyntaxKind.SubtractAssignmentExpression:
                        case SyntaxKind.MultiplyAssignmentExpression:
                        case SyntaxKind.DivideAssignmentExpression:
                        case SyntaxKind.ModuloAssignmentExpression:
                        case SyntaxKind.LeftShiftAssignmentExpression:
                        case SyntaxKind.RightShiftAssignmentExpression:
                        case SyntaxKind.AndAssignmentExpression:
                        case SyntaxKind.OrAssignmentExpression:
                        case SyntaxKind.ExclusiveOrAssignmentExpression:
                        case SyntaxKind.PlusEqualsToken:
                        case SyntaxKind.MinusEqualsToken:
                        case SyntaxKind.AsteriskEqualsToken:
                        case SyntaxKind.SlashEqualsToken:
                        case SyntaxKind.GreaterThanGreaterThanEqualsToken:
                        case SyntaxKind.LessThanLessThanEqualsToken:
                        case SyntaxKind.AmpersandEqualsToken:
                        case SyntaxKind.PercentEqualsToken:
                        case SyntaxKind.BarEqualsToken:
                        case SyntaxKind.CaretEqualsToken:
                            operatorMethods.AddRange(GetOperators(lhsCapture.GetReturnType(), node.Kind()));
                            //operatorMethods.AddRange(GetOperators(rhsValue.symbolCsType, node.Kind()));
                            operatorMethods.AddRange(GetImplicitHigherPrecisionOperator(lhsCapture.GetReturnType(), rhsValue.symbolCsType, SyntaxKindToBuiltinOperator(node.OperatorToken.Kind()), true));
                            operatorMethods = operatorMethods.Distinct().ToList();
                            break;
                        default:
                            throw new System.NotImplementedException($"Assignment operator {node.OperatorToken.Kind()} does not have handling");
                    }

                    // Handle implicit ToString()
                    if (lhsCapture.GetReturnType() == typeof(string) && 
                        rhsValue.GetType() != typeof(string) && 
                        visitorContext.resolverContext.FindBestOverloadFunction(operatorMethods.ToArray(), new List<System.Type> { lhsCapture.GetReturnType(), rhsValue.GetType() }) == null)
                    {
                        using (ExpressionCaptureScope stringConversionScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            stringConversionScope.SetToLocalSymbol(rhsValue);
                            stringConversionScope.ResolveAccessToken("ToString");

                            rhsValue = stringConversionScope.Invoke(new SymbolDefinition[] { });
                        }
                    }

                    using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null))
                    {
                        operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                        SymbolDefinition resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { lhsCapture.ExecuteGet(), rhsValue });

                        using (ExpressionCaptureScope resultPropagationScope = new ExpressionCaptureScope(visitorContext, topScope))
                        {
                            resultPropagationScope.SetToLocalSymbol(resultSymbol);

                            if (needsCopy)
                            {
                                // Create a new set scope to maintain array setter handling for structs
                                using (ExpressionCaptureScope lhsSetScope = new ExpressionCaptureScope(visitorContext, null))
                                {
                                    Visit(node.Left);

                                    // In place arithmetic operators for lower precision types will return int, but C# will normally cast the result back to the target type, so do a force cast here
                                    lhsSetScope.ExecuteSet(resultSymbol, true);
                                }
                            }
                            else
                            {
                                // In place arithmetic operators for lower precision types will return int, but C# will normally cast the result back to the target type, so do a force cast here
                                lhsCapture.ExecuteSet(resultSymbol, true);
                            }
                        }
                    }
                }
            }
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            ExpressionCaptureScope topScope = visitorContext.topCaptureScope;
            SymbolDefinition requestedDestination = visitorContext.requestedDestination;

            using (ExpressionCaptureScope operandCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Operand);

                if (node.OperatorToken.Kind() == SyntaxKind.PlusToken || node.OperatorToken.Kind() == SyntaxKind.UnaryPlusExpression)
                {
                    if (topScope != null)
                        topScope.SetToLocalSymbol(operandCapture.ExecuteGet());
                    return;
                }

                List<MethodInfo> operatorMethods = new List<MethodInfo>();

                switch (node.OperatorToken.Kind())
                {
                    // Technically the increment/decrement operator is a separately defined thing in C# and there can be user defined ones.
                    // So using addition/subtraction here isn't strictly valid, but Udon does not expose any increment/decrement overrides so it's fine for the moment.
                    case SyntaxKind.PlusPlusToken:
                    case SyntaxKind.PreIncrementExpression:
                    case SyntaxKind.MinusMinusToken:
                    case SyntaxKind.PreDecrementExpression:
                        // Write back the result of the change directly to the original symbol.
                        requestedDestination = operandCapture.destinationSymbolForSet;
                        operatorMethods.AddRange(GetOperators(operandCapture.GetReturnType(), node.OperatorToken.Kind()));
                        break;
                    case SyntaxKind.LogicalNotExpression:
                    case SyntaxKind.ExclamationToken:
                        operatorMethods.AddRange(GetOperators(operandCapture.GetReturnType(), node.OperatorToken.Kind()));

                        if (operandCapture.GetReturnType() != typeof(bool))
                            operatorMethods.AddRange(GetOperators(typeof(bool), node.OperatorToken.Kind()));
                        break;
                    case SyntaxKind.MinusToken:
                        operatorMethods.AddRange(GetOperators(operandCapture.GetReturnType(), node.OperatorToken.Kind()));
                        operatorMethods.AddRange(GetImplicitHigherPrecisionOperator(operandCapture.GetReturnType(), null, SyntaxKindToBuiltinOperator(node.OperatorToken.Kind()), true));
                        break;
                    case SyntaxKind.BitwiseNotExpression:
                    case SyntaxKind.TildeToken:
                        throw new System.NotSupportedException("Udon does not support BitwiseNot at the moment (https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/bitwisenot-for-integer-built-in-types)");
                    default:
                        throw new System.NotImplementedException($"Handling for prefix token {node.OperatorToken.Kind()} is not implemented");
                }
                
                using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null, requestedDestination))
                {
                    operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                    BuiltinOperatorType operatorType = SyntaxKindToBuiltinOperator(node.OperatorToken.Kind());

                    SymbolDefinition resultSymbol = null;

                    if (operatorType == BuiltinOperatorType.UnaryNegation ||
                        operatorType == BuiltinOperatorType.UnaryMinus || 
                        operatorType == BuiltinOperatorType.BitwiseNot)
                    {
                        SymbolDefinition operandResult = operandCapture.ExecuteGet();

                        if (operatorType == BuiltinOperatorType.UnaryNegation &&
                            operandResult.symbolCsType != typeof(bool) &&
                            operatorMethods.Count == 1) // If the count isn't 1 it means we found an override for `!` for the specific type so we skip attempting the implicit cast
                            operandResult = HandleImplicitBoolCast(operandResult);

                        try
                        {
                            resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { operandResult });
                        }
                        catch (System.Exception)
                        {
                            throw new System.ArgumentException($"Operator '{node.OperatorToken.Text}' cannot be applied to operand of type '{UdonSharpUtils.PrettifyTypeName(operandCapture.GetReturnType())}'");
                        }

                        if (topScope != null)
                            topScope.SetToLocalSymbol(resultSymbol);
                    }
                    else
                    {
                        SymbolDefinition valueConstant = visitorContext.topTable.CreateConstSymbol(operandCapture.GetReturnType(), System.Convert.ChangeType(1, operandCapture.GetReturnType()));

                        try
                        {
                            resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { operandCapture.ExecuteGet(), valueConstant });

                            operandCapture.ExecuteSet(resultSymbol, true);
                        }
                        catch (System.Exception)
                        {
                            throw new System.ArgumentException($"Operator '{node.OperatorToken.Text}' cannot be applied to operand of type '{UdonSharpUtils.PrettifyTypeName(operandCapture.GetReturnType())}'");
                        }

                        if (topScope != null)
                            topScope.SetToLocalSymbol(operandCapture.ExecuteGet());
                    }
                }
            }
        }

        public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            ExpressionCaptureScope topScope = visitorContext.topCaptureScope;
            SymbolDefinition preIncrementStore = visitorContext.requestedDestination;

            using (ExpressionCaptureScope operandCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Operand);

                List<MethodInfo> operatorMethods = new List<MethodInfo>();

                switch (node.OperatorToken.Kind())
                {
                    // Technically the increment/decrement operator is a separately defined thing in C# and there can be user defined ones.
                    // So using addition/subtraction here isn't strictly valid, but Udon does not expose any increment/decrement overrides so it's fine for the moment.
                    case SyntaxKind.PlusPlusToken:
                    case SyntaxKind.PreIncrementExpression:
                    case SyntaxKind.MinusMinusToken:
                    case SyntaxKind.PreDecrementExpression:
                        operatorMethods.AddRange(GetOperators(operandCapture.GetReturnType(), node.OperatorToken.Kind()));
                        break;
                    default:
                        throw new System.NotImplementedException($"Handling for prefix token {node.OperatorToken.Kind()} is not implemented");
                }

                try
                {
                    using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null, operandCapture.destinationSymbolForSet))
                    {
                        operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                        using (ExpressionCaptureScope preIncrementValueReturn = new ExpressionCaptureScope(visitorContext, topScope))
                        {
                            if (preIncrementStore == null) {
                                preIncrementStore = visitorContext.topTable.CreateUnnamedSymbol(operandCapture.GetReturnType(), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);
                            }
                            preIncrementValueReturn.SetToLocalSymbol(preIncrementStore);

                            preIncrementValueReturn.ExecuteSet(operandCapture.ExecuteGet());
                        }

                        SymbolDefinition valueConstant = visitorContext.topTable.CreateConstSymbol(operandCapture.GetReturnType(), System.Convert.ChangeType(1, operandCapture.GetReturnType()));

                        SymbolDefinition resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { operandCapture.ExecuteGet(), valueConstant });

                        operandCapture.ExecuteSet(resultSymbol, true);
                    }
                }
                catch (System.Exception e)
                {
                    throw new System.ArgumentException($"Operator '{node.OperatorToken.Text}' cannot be applied to operand of type '{UdonSharpUtils.PrettifyTypeName(operandCapture.GetReturnType())}'");
                }
            }
        }

        // Where we handle creating constants and such
        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition expressionConstant = null;

            switch (node.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                    // The Roslyn AST figures out the type automagically for you based on how the token is declared :D 
                    // Can probably flatten out the other ones into this too
                    expressionConstant = visitorContext.topTable.CreateConstSymbol(node.Token.Value.GetType(), node.Token.Value);
                    break;
                case SyntaxKind.StringLiteralExpression:
                    expressionConstant = visitorContext.topTable.CreateConstSymbol(typeof(string), node.Token.Value);
                    break;
                case SyntaxKind.CharacterLiteralExpression:
                    expressionConstant = visitorContext.topTable.CreateConstSymbol(typeof(char), node.Token.Value);
                    break;
                case SyntaxKind.TrueLiteralExpression:
                    expressionConstant = visitorContext.topTable.CreateConstSymbol(typeof(bool), true);
                    break;
                case SyntaxKind.FalseLiteralExpression:
                    expressionConstant = visitorContext.topTable.CreateConstSymbol(typeof(bool), false);
                    break;
                case SyntaxKind.NullLiteralExpression:
                    expressionConstant = visitorContext.topTable.CreateConstSymbol(typeof(object), null);
                    break;
                default:
                    base.VisitLiteralExpression(node);
                    return;
            }

            if (expressionConstant != null && visitorContext.topCaptureScope != null)
            {
                visitorContext.topCaptureScope.SetToLocalSymbol(expressionConstant);
            }
        }

        public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Expression);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            MethodDefinition definition = visitorContext.definedMethods.Where(e => e.originalMethodName == node.Identifier.ValueText).First();

            string functionName = node.Identifier.ValueText;
            bool isBuiltinEvent = visitorContext.resolverContext.ReplaceInternalEventName(ref functionName);

            if (functionName == "Awake")
                throw new System.NotSupportedException("Udon does not support the 'Awake' event, use 'Start' instead");

            if (node.Modifiers.HasModifier("static"))
                throw new System.NotSupportedException("UdonSharp does not currently support static method declarations");

            foreach (ParameterSyntax param in node.ParameterList.Parameters)
            {
                UpdateSyntaxNode(param);

                if (param.Modifiers.Any(SyntaxKind.OutKeyword))
                    throw new System.NotSupportedException("UdonSharp does not yet support 'out' parameters on user-defined methods.");
                if (param.Modifiers.Any(SyntaxKind.InKeyword))
                    throw new System.NotSupportedException("UdonSharp does not yet support 'in' parameters on user-defined methods.");
                if (param.Modifiers.Any(SyntaxKind.RefKeyword))
                    throw new System.NotSupportedException("UdonSharp does not yet support 'ref' parameters on user-defined methods.");
            }

            // Export the method if it's public or builtin
            if (isBuiltinEvent || node.Modifiers.HasModifier("public"))
            {
                visitorContext.uasmBuilder.AppendLine($".export {functionName}", 1);
                visitorContext.uasmBuilder.AppendLine("");
            }

            visitorContext.uasmBuilder.AppendLine($"{functionName}:", 1);
            visitorContext.uasmBuilder.AppendLine("");

            Debug.Assert(visitorContext.returnLabel == null, "Return label must be null");
            JumpLabel returnLabel = visitorContext.labelTable.GetNewJumpLabel("return");
            visitorContext.returnLabel = returnLabel;
            visitorContext.returnSymbol = definition.returnSymbol;

            visitorContext.uasmBuilder.AddJumpLabel(definition.methodUdonEntryPoint);
            
            SymbolDefinition constEndAddrVal = visitorContext.topTable.CreateConstSymbol(typeof(uint), 0xFFFFFFFF);
            visitorContext.uasmBuilder.AddPush(constEndAddrVal);

            if (isBuiltinEvent)
            {
                System.Tuple<System.Type, string>[] customEventArgs = visitorContext.resolverContext.GetMethodCustomArgs(functionName);
                if (customEventArgs != null)
                {
                    if (customEventArgs.Length != definition.parameters.Length)
                        throw new System.Exception($"Event {functionName} must have the correct argument types for the Unity event");

                    for (int i = 0; i < customEventArgs.Length; ++i)
                    {
                        SymbolDefinition autoAssignedEventSymbol = visitorContext.topTable.GetGlobalSymbolTable().CreateNamedSymbol(customEventArgs[i].Item2, customEventArgs[i].Item1, SymbolDeclTypeFlags.Private);

                        using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            argAssignmentScope.SetToLocalSymbol(definition.parameters[i].paramSymbol);
                            argAssignmentScope.ExecuteSet(autoAssignedEventSymbol);
                        }
                    }
                }
            }

            visitorContext.uasmBuilder.AddJumpLabel(definition.methodUserCallStart);

            if (!visitorContext.topTable.IsGlobalSymbolTable)
                throw new System.Exception("Parent symbol table for method table must be the global symbol table.");

            SymbolTable functionSymbolTable = new SymbolTable(visitorContext.resolverContext, visitorContext.topTable);

            // Setup local symbols for the user to read from, this prevents potential conflicts with other methods that have the same argument names
            foreach (ParameterDefinition paramDef in definition.parameters)
                functionSymbolTable.symbolDefinitions.Add(paramDef.paramSymbol);

            visitorContext.PushTable(functionSymbolTable);

            if (node.Body != null && node.ExpressionBody != null)
                throw new System.Exception("Block bodies and expression bodies cannot both be provided.");

            if (node.Body != null)
            {
                Visit(node.Body);
            }
            else if (node.ExpressionBody != null)
            {
                using (ExpressionCaptureScope expressionBodyCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.ExpressionBody);

                    if (visitorContext.returnSymbol != null)
                    {
                        using (ExpressionCaptureScope returnSetterScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            returnSetterScope.SetToLocalSymbol(visitorContext.returnSymbol);
                            returnSetterScope.ExecuteSetDirect(expressionBodyCapture);
                        }
                    }
                }
            }
            else
            {
                throw new System.Exception($"Method {functionName} must declare a body");
            }

            visitorContext.topTable.FlattenTableCountersToGlobal();
            visitorContext.PopTable();

            visitorContext.uasmBuilder.AddJumpLabel(returnLabel);
            visitorContext.uasmBuilder.AddJumpLabel(definition.methodReturnPoint);
            visitorContext.uasmBuilder.AddReturnSequence(visitorContext.returnJumpTarget, "Function epilogue");
            //visitorContext.uasmBuilder.AddJumpToExit();
            visitorContext.uasmBuilder.AppendLine("");

            visitorContext.returnLabel = null;
        }

        public override void VisitQualifiedName(QualifiedNameSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Left);
            Visit(node.Right);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Expression);
            Visit(node.Name);
        }
        
        private static MethodInfo[] GetOperators(System.Type type, BuiltinOperatorType builtinOperatorType)
        {
            return UdonSharpUtils.GetOperators(type, builtinOperatorType);
        }

        private MethodInfo[] GetImplicitHigherPrecisionOperator(System.Type lhsType, System.Type rhsType, BuiltinOperatorType builtinOperatorType, bool isAssignment = false)
        {
            if (lhsType == rhsType)
                return new MethodInfo[] { };

            // If both are not numeric types then there will be no higher precision operator to use
            // Implicit casts on the operands to higher precision types happen elsewhere
            if (!UdonSharpUtils.IsNumericType(lhsType) || (rhsType != null && !UdonSharpUtils.IsNumericType(rhsType)))
                return new MethodInfo[] { };

            // There is an implcit cast already so the other type's operator should be included in operator finding already
            if (!isAssignment && (UdonSharpUtils.IsNumericImplicitCastValid(lhsType, rhsType) || UdonSharpUtils.IsNumericImplicitCastValid(rhsType, lhsType)))
                return new MethodInfo[] { };

            System.Type nextPrecisionLhs = UdonSharpUtils.GetNextHighestNumericPrecision(lhsType);
            System.Type nextPrecisionRhs = UdonSharpUtils.GetNextHighestNumericPrecision(rhsType);

            if (nextPrecisionLhs == null && nextPrecisionRhs == null)
                return new MethodInfo[] { };

            System.Type nextPrecision = nextPrecisionLhs;

            if (nextPrecision == null || (nextPrecisionRhs == typeof(long)))
                nextPrecision = nextPrecisionRhs;

            return new MethodInfo[] { new OperatorMethodInfo(nextPrecision, builtinOperatorType) };
        }

        private BuiltinOperatorType SyntaxKindToBuiltinOperator(SyntaxKind syntaxKind)
        {
            switch (syntaxKind)
            {
                case SyntaxKind.AddExpression:
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.PlusEqualsToken:
                case SyntaxKind.PlusPlusToken:
                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PostIncrementExpression:
                    return BuiltinOperatorType.Addition;
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.SubtractAssignmentExpression:
                case SyntaxKind.MinusEqualsToken:
                case SyntaxKind.MinusMinusToken:
                case SyntaxKind.PreDecrementExpression:
                case SyntaxKind.PostDecrementExpression:
                    return BuiltinOperatorType.Subtraction;
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.AsteriskEqualsToken:
                    return BuiltinOperatorType.Multiplication;
                case SyntaxKind.DivideExpression:
                case SyntaxKind.DivideAssignmentExpression:
                case SyntaxKind.SlashEqualsToken:
                    return BuiltinOperatorType.Division;
                case SyntaxKind.ModuloExpression:
                case SyntaxKind.ModuloAssignmentExpression:
                case SyntaxKind.PercentEqualsToken:
                    return BuiltinOperatorType.Remainder;
                case SyntaxKind.UnaryMinusExpression:
                case SyntaxKind.MinusToken:
                    return BuiltinOperatorType.UnaryMinus;
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.LeftShiftAssignmentExpression:
                case SyntaxKind.LessThanLessThanEqualsToken:
                    return BuiltinOperatorType.LeftShift;
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                case SyntaxKind.GreaterThanGreaterThanEqualsToken:
                    return BuiltinOperatorType.RightShift;
                case SyntaxKind.BitwiseAndExpression:
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.AmpersandEqualsToken:
                    return BuiltinOperatorType.LogicalAnd;
                case SyntaxKind.BitwiseOrExpression:
                case SyntaxKind.OrAssignmentExpression:
                case SyntaxKind.BarEqualsToken:
                    return BuiltinOperatorType.LogicalOr;
                case SyntaxKind.BitwiseNotExpression:
                    return BuiltinOperatorType.BitwiseNot;
                case SyntaxKind.ExclusiveOrExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                case SyntaxKind.CaretEqualsToken:
                    return BuiltinOperatorType.LogicalXor;
                case SyntaxKind.LogicalOrExpression:
                    return BuiltinOperatorType.ConditionalOr;
                case SyntaxKind.LogicalAndExpression:
                    return BuiltinOperatorType.ConditionalAnd;
                case SyntaxKind.LogicalNotExpression:
                case SyntaxKind.ExclamationToken:
                    return BuiltinOperatorType.UnaryNegation;
                case SyntaxKind.EqualsExpression:
                    return BuiltinOperatorType.Equality;
                case SyntaxKind.GreaterThanExpression:
                    return BuiltinOperatorType.GreaterThan;
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return BuiltinOperatorType.GreaterThanOrEqual;
                case SyntaxKind.LessThanExpression:
                    return BuiltinOperatorType.LessThan;
                case SyntaxKind.LessThanOrEqualExpression:
                    return BuiltinOperatorType.LessThanOrEqual;
                case SyntaxKind.NotEqualsExpression:
                    return BuiltinOperatorType.Inequality;
                default:
                    throw new System.NotImplementedException($"Builtin operator handling doesn't exist for syntax kind {syntaxKind}");
            }
        }

        private MethodInfo[] GetOperators(System.Type type, SyntaxKind syntaxKind)
        {
            return GetOperators(type, SyntaxKindToBuiltinOperator(syntaxKind));
        }

        private void HandleBinaryShortCircuitConditional(BinaryExpressionSyntax node)
        {
            // Assume we're dealing with bools so it's a lot easier here
            MethodInfo[] methods = GetOperators(typeof(bool), node.Kind());

            JumpLabel rhsEnd = visitorContext.labelTable.GetNewJumpLabel("conditionalShortCircuitEnd");

            SymbolDefinition resultValue = visitorContext.topTable.CreateUnnamedSymbol(typeof(bool), SymbolDeclTypeFlags.Internal);

            using (ExpressionCaptureScope lhsCaptureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Left);

                using (ExpressionCaptureScope resultSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    resultSetScope.SetToLocalSymbol(resultValue);
                    resultSetScope.ExecuteSet(lhsCaptureScope.ExecuteGet());
                }
            }

            if (node.Kind() == SyntaxKind.LogicalAndExpression)
            {
                visitorContext.uasmBuilder.AddPush(resultValue);
                visitorContext.uasmBuilder.AddJumpIfFalse(rhsEnd);
            }
            else // OR
            {
                using (ExpressionCaptureScope negationOpScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    negationOpScope.SetToMethods(GetOperators(typeof(bool), BuiltinOperatorType.UnaryNegation));
                    SymbolDefinition negatedResult = negationOpScope.Invoke(new SymbolDefinition[] { resultValue });

                    visitorContext.uasmBuilder.AddPush(negatedResult);
                    visitorContext.uasmBuilder.AddJumpIfFalse(rhsEnd);
                }
            }

            SymbolDefinition rhsValue = null;

            using (ExpressionCaptureScope rhsCaptureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Right);
                rhsValue = rhsCaptureScope.ExecuteGet();
            }

            using (ExpressionCaptureScope conditionComparisonScope = new ExpressionCaptureScope(visitorContext, null))
            {
                conditionComparisonScope.SetToMethods(GetOperators(typeof(bool), node.Kind()));

                using (ExpressionCaptureScope resultSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    resultSetScope.SetToLocalSymbol(resultValue);
                    resultSetScope.ExecuteSet(conditionComparisonScope.Invoke(new SymbolDefinition[] { resultValue, rhsValue }));
                }
            }

            visitorContext.uasmBuilder.AddJumpLabel(rhsEnd);

            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.SetToLocalSymbol(resultValue);
        }

        // This doesn't yet support type handling for A ?? B that is conformant to the C# spec. At the moment the output type will always be A's type, which isn't right.
        private void HandleCoalesceExpression(BinaryExpressionSyntax node)
        {
            JumpLabel rhsEnd = visitorContext.labelTable.GetNewJumpLabel("coalesceExpressionEnd");

            SymbolDefinition resultValue = null;

            using (ExpressionCaptureScope lhsScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Left);

                resultValue = visitorContext.topTable.CreateUnnamedSymbol(lhsScope.GetReturnType(), SymbolDeclTypeFlags.Internal);

                using (ExpressionCaptureScope lhsSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    lhsSetScope.SetToLocalSymbol(resultValue);
                    lhsSetScope.ExecuteSet(lhsScope.ExecuteGet());
                }

                using (ExpressionCaptureScope conditonMethodScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    conditonMethodScope.SetToMethods(GetOperators(typeof(object), BuiltinOperatorType.Equality));

                    SymbolDefinition lhsIsNotNullCondition = conditonMethodScope.Invoke(new SymbolDefinition[] { resultValue, visitorContext.topTable.CreateConstSymbol(typeof(object), null) });

                    visitorContext.uasmBuilder.AddPush(lhsIsNotNullCondition);
                    visitorContext.uasmBuilder.AddJumpIfFalse(rhsEnd);
                }
            }

            using (ExpressionCaptureScope rhsScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Right);

                using (ExpressionCaptureScope rhsSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    rhsSetScope.SetToLocalSymbol(resultValue);
                    rhsSetScope.ExecuteSet(rhsScope.ExecuteGet());
                }
            }

            visitorContext.uasmBuilder.AddJumpLabel(rhsEnd);

            using (ExpressionCaptureScope resultCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                resultCapture.SetToLocalSymbol(resultValue);
            }
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            SymbolDefinition requestedDestination = visitorContext.topCaptureScope.requestedDestination;

            UpdateSyntaxNode(node);

            if (node.Kind() == SyntaxKind.IsExpression)
                throw new System.NotSupportedException("The `is` keyword is not yet supported by UdonSharp since Udon does not expose what is necessary (https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/expose-systemtypeissubclassof-isinstanceoftype-issubclassof-and-basetype)");

            if (node.Kind() == SyntaxKind.AsExpression)
                throw new System.NotSupportedException("The `as` keyword is not yet supported by UdonSharp since Udon does not expose what is necessary (https://vrchat.canny.io/vrchat-udon-closed-alpha-feedback/p/expose-systemtypeissubclassof-isinstanceoftype-issubclassof-and-basetype)");

            if (node.Kind() == SyntaxKind.LogicalAndExpression || node.Kind() == SyntaxKind.LogicalOrExpression)
            {
                HandleBinaryShortCircuitConditional(node);
                return;
            }

            if (node.Kind() == SyntaxKind.CoalesceExpression || node.Kind() == SyntaxKind.QuestionQuestionToken)
            {
                HandleCoalesceExpression(node);
                return;
            }

            SymbolDefinition rhsValue = null;
            SymbolDefinition.COWValue lhsValueCOW = null;

            ExpressionCaptureScope outerScope = visitorContext.topCaptureScope;

            using (ExpressionCaptureScope lhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Left);

                lhsValueCOW = lhsCapture.ExecuteGetCOW();

                using (ExpressionCaptureScope rhsCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    //visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));
                    Visit(node.Right);
                    //visitorContext.PopTable();

                    rhsValue = rhsCapture.ExecuteGet();
                }

                SymbolDefinition lhsValue = lhsValueCOW.symbol;

                System.Type lhsType = lhsValue.symbolCsType;
                System.Type rhsType = rhsValue.symbolCsType;

                List<MethodInfo> operatorMethods = new List<MethodInfo>();

                switch (node.Kind())
                {
                    case SyntaxKind.AddExpression:
                    case SyntaxKind.SubtractExpression:
                    case SyntaxKind.MultiplyExpression:
                    case SyntaxKind.DivideExpression:
                    case SyntaxKind.ModuloExpression:
                    case SyntaxKind.UnaryMinusExpression:
                    case SyntaxKind.LeftShiftExpression:
                    case SyntaxKind.RightShiftExpression:
                    case SyntaxKind.BitwiseAndExpression:
                    case SyntaxKind.BitwiseOrExpression:
                    case SyntaxKind.BitwiseNotExpression:
                    case SyntaxKind.ExclusiveOrExpression:
                    //case SyntaxKind.LogicalOrExpression: // Handled by HandleBinaryShortCircuitConditional
                    //case SyntaxKind.LogicalAndExpression:
                    case SyntaxKind.LogicalNotExpression:
                    case SyntaxKind.EqualsExpression:
                    case SyntaxKind.GreaterThanExpression:
                    case SyntaxKind.GreaterThanOrEqualExpression:
                    case SyntaxKind.LessThanExpression:
                    case SyntaxKind.LessThanOrEqualExpression:
                    case SyntaxKind.NotEqualsExpression:
                        operatorMethods.AddRange(GetOperators(lhsType, node.Kind()));
                        operatorMethods.AddRange(GetOperators(rhsType, node.Kind()));
                        operatorMethods.AddRange(GetImplicitHigherPrecisionOperator(lhsType, rhsType, SyntaxKindToBuiltinOperator(node.Kind())));
                        operatorMethods = operatorMethods.Distinct().ToList();
                        break;
                    default:
                        throw new System.NotImplementedException($"Binary expression {node.Kind()} is not implemented");
                }

                if (operatorMethods.Count == 0)
                    throw new System.ArgumentException($"Operator '{node.OperatorToken.Text}' cannot be applied to operands of type '{UdonSharpUtils.PrettifyTypeName(lhsType)}' and '{UdonSharpUtils.PrettifyTypeName(rhsType)}'");

                using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null, requestedDestination))
                {
                    operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                    SymbolDefinition resultSymbol = null;

                    BuiltinOperatorType operatorType = SyntaxKindToBuiltinOperator(node.Kind());

                    // Basic handling for handling null equality/inequality on derived types since Unity has special behavior for comparing UnityEngine.Object types to null
                    if (operatorType == BuiltinOperatorType.Equality ||
                        operatorType == BuiltinOperatorType.Inequality)
                    {
                        bool lhsNull = lhsValue.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) && lhsValue.symbolDefaultValue == null;
                        bool rhsNull = rhsValue.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) && rhsValue.symbolDefaultValue == null;

                        if (lhsNull && !rhsNull)
                        {
                            lhsValue = visitorContext.topTable.CreateConstSymbol(rhsType, null);
                        }
                        else if (rhsNull && !lhsNull)
                        {
                            rhsValue = visitorContext.topTable.CreateConstSymbol(lhsType, null);
                        }
                    }

                    try
                    {
                        resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { lhsValue, rhsValue });
                    }
                    catch (System.Exception)
                    {
                        // If the left or right hand side are string types then we have a special exception where we can call ToString() on the operands
                        if (SyntaxKindToBuiltinOperator(node.Kind()) == BuiltinOperatorType.Addition &&
                            (lhsValue.symbolCsType == typeof(string) || rhsValue.symbolCsType == typeof(string)))
                        {
                            if (lhsValue.symbolCsType != typeof(string))
                            {
                                using (ExpressionCaptureScope symbolCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                                {
                                    symbolCaptureScope.SetToLocalSymbol(lhsValue);
                                    symbolCaptureScope.ResolveAccessToken("ToString");
                                    lhsValue = symbolCaptureScope.Invoke(new SymbolDefinition[] { });
                                }
                            }
                            else if (rhsValue.symbolCsType != typeof(string))
                            {
                                using (ExpressionCaptureScope symbolCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                                {
                                    symbolCaptureScope.SetToLocalSymbol(rhsValue);
                                    symbolCaptureScope.ResolveAccessToken("ToString");
                                    rhsValue = symbolCaptureScope.Invoke(new SymbolDefinition[] { });
                                }
                            }

                            resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { lhsValue, rhsValue });
                        }
                        else
                        {
                            throw new System.ArgumentException($"Operator '{node.OperatorToken.Text}' cannot be applied to operands of type '{UdonSharpUtils.PrettifyTypeName(lhsType)}' and '{UdonSharpUtils.PrettifyTypeName(rhsType)}'");
                        }
                    }
                    
                    MethodBase invokedMethod = operatorMethodCapture.GetInvokeMethod(new SymbolDefinition[] { lhsValue, rhsValue });

                    // This is a special case for enums only at the moment where we need to use Object.Equals to compare them since Udon does not currently expose equality operators for enums
                    if (invokedMethod.DeclaringType == typeof(object) && invokedMethod.Name == "Equals")
                    {
                        if (lhsType != rhsType) // Only allow exact enum comparisons
                            throw new System.ArgumentException($"Operator '{node.OperatorToken.Text}' cannot be applied to operands of type '{UdonSharpUtils.PrettifyTypeName(lhsType)}' and '{UdonSharpUtils.PrettifyTypeName(rhsType)}'");

                        BuiltinOperatorType equalityOperatorType = SyntaxKindToBuiltinOperator(node.Kind());
                        if (equalityOperatorType == BuiltinOperatorType.Inequality) // We need to invert the result manually
                        {
                            using (ExpressionCaptureScope negationScope = new ExpressionCaptureScope(visitorContext, null))
                            {
                                negationScope.SetToMethods(GetOperators(typeof(bool), BuiltinOperatorType.UnaryNegation));
                                resultSymbol = negationScope.Invoke(new SymbolDefinition[] { resultSymbol });
                            }
                        }
                    }

                    if (outerScope != null)
                        outerScope.SetToLocalSymbol(resultSymbol);
                }
            }
        }

        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            System.Type targetType = null;
            SymbolDefinition expressionSymbol = null;

            using (ExpressionCaptureScope castExpressionCapture = new ExpressionCaptureScope(visitorContext, null, visitorContext.requestedDestination))
            {
                Visit(node.Expression);

                expressionSymbol = castExpressionCapture.ExecuteGet();
            }

            using (ExpressionCaptureScope castTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);

                if (!castTypeCapture.IsType())
                    throw new System.ArgumentException("Cast target type must be a Type");

                targetType = castTypeCapture.captureType;
            }


            SymbolDefinition castOutSymbol = visitorContext.requestedDestination;

            using (ExpressionCaptureScope castOutCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                if (castOutSymbol == null)
                {
                    castOutSymbol = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);
                }

                castOutCapture.SetToLocalSymbol(castOutSymbol);

                castOutCapture.ExecuteSet(expressionSymbol, true);
            }
        }

        public override void VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("UdonSharp does not currently support type checking with the \"is\" keyword since Udon does not yet expose the proper functionality for type checking.");
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            if (visitorContext.returnSymbol != null)
            {
                using (ExpressionCaptureScope returnCaptureScope = new ExpressionCaptureScope(visitorContext, null, visitorContext.returnSymbol))
                {
                    Visit(node.Expression);

                    using (ExpressionCaptureScope returnOutSetter = new ExpressionCaptureScope(visitorContext, null))
                    {
                        returnOutSetter.SetToLocalSymbol(visitorContext.returnSymbol);
                        returnOutSetter.ExecuteSet(returnCaptureScope.ExecuteGet());
                    }
                }
            }

            visitorContext.uasmBuilder.AddReturnSequence(visitorContext.returnJumpTarget, "Explicit return sequence");
            //visitorContext.uasmBuilder.AddJumpToExit();
        }

        public override void VisitBreakStatement(BreakStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            visitorContext.uasmBuilder.AddJump(visitorContext.breakLabelStack.Peek());
        }

        public override void VisitContinueStatement(ContinueStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            visitorContext.uasmBuilder.AddJump(visitorContext.continueLabelStack.Peek());
        }

        private SymbolDefinition HandleImplicitBoolCast(SymbolDefinition symbol)
        {
            if (symbol == null)
                throw new System.ArgumentException("Cannot implicitly convert type 'void' to 'bool'");

            if (symbol.symbolCsType != typeof(bool))
            {
                SymbolDefinition conditionBoolCast = visitorContext.topTable.CreateUnnamedSymbol(typeof(bool), SymbolDeclTypeFlags.Internal);
                using (ExpressionCaptureScope conditionSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    conditionSetScope.SetToLocalSymbol(conditionBoolCast);
                    conditionSetScope.ExecuteSet(symbol);
                }

                return conditionBoolCast;
            }

            return symbol;
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition conditionSymbol = null;

            using (ExpressionCaptureScope conditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = HandleImplicitBoolCast(conditionScope.ExecuteGet());
            }
            
            JumpLabel failLabel = visitorContext.labelTable.GetNewJumpLabel("ifStatmentFalse");
            JumpLabel exitStatementLabel = visitorContext.labelTable.GetNewJumpLabel("ifStatmentBodyExit");

            visitorContext.uasmBuilder.AddPush(conditionSymbol);
            visitorContext.uasmBuilder.AddJumpIfFalse(failLabel);

            Visit(node.Statement);

            if (node.Else != null)
                visitorContext.uasmBuilder.AddJump(exitStatementLabel);

            visitorContext.uasmBuilder.AddJumpLabel(failLabel);

            Visit(node.Else);

            visitorContext.uasmBuilder.AddJumpLabel(exitStatementLabel);
        }

        public override void VisitElseClause(ElseClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Statement); 
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            JumpLabel whileLoopStart = visitorContext.labelTable.GetNewJumpLabel("whileLoopStart");
            visitorContext.uasmBuilder.AddJumpLabel(whileLoopStart);

            JumpLabel whileLoopEnd = visitorContext.labelTable.GetNewJumpLabel("whileLoopEnd");

            SymbolDefinition conditionSymbol = null; 

            using (ExpressionCaptureScope conditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = HandleImplicitBoolCast(conditionScope.ExecuteGet());
            }

            visitorContext.uasmBuilder.AddPush(conditionSymbol);
            visitorContext.uasmBuilder.AddJumpIfFalse(whileLoopEnd);

            visitorContext.continueLabelStack.Push(whileLoopStart);
            visitorContext.breakLabelStack.Push(whileLoopEnd);

            Visit(node.Statement);

            visitorContext.continueLabelStack.Pop();
            visitorContext.breakLabelStack.Pop();

            visitorContext.uasmBuilder.AddJump(whileLoopStart);
            visitorContext.uasmBuilder.AddJumpLabel(whileLoopEnd);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            JumpLabel doLoopStart = visitorContext.labelTable.GetNewJumpLabel("doLoopStart");
            visitorContext.uasmBuilder.AddJumpLabel(doLoopStart);

            JumpLabel doLoopConditionalStart = visitorContext.labelTable.GetNewJumpLabel("doLoopCondition");
            JumpLabel doLoopEnd = visitorContext.labelTable.GetNewJumpLabel("doLoopEnd");

            visitorContext.continueLabelStack.Push(doLoopConditionalStart);
            visitorContext.breakLabelStack.Push(doLoopEnd);

            Visit(node.Statement);

            visitorContext.continueLabelStack.Pop();
            visitorContext.breakLabelStack.Pop();

            visitorContext.uasmBuilder.AddJumpLabel(doLoopConditionalStart);

            SymbolDefinition conditionSymbol = null;
            using (ExpressionCaptureScope conditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = HandleImplicitBoolCast(conditionScope.ExecuteGet());
            }

            visitorContext.uasmBuilder.AddPush(conditionSymbol);
            visitorContext.uasmBuilder.AddJumpIfFalse(doLoopEnd);

            visitorContext.uasmBuilder.AddJump(doLoopStart);

            visitorContext.uasmBuilder.AddJumpLabel(doLoopEnd);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolTable forLoopSymbolTable = new SymbolTable(visitorContext.resolverContext, visitorContext.topTable);
            visitorContext.PushTable(forLoopSymbolTable);

            Visit(node.Declaration);

            foreach (ExpressionSyntax initializer in node.Initializers)
            {
                using (ExpressionCaptureScope voidReturnScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(initializer);
                }
            }

            JumpLabel forLoopStart = visitorContext.labelTable.GetNewJumpLabel("forLoopStart");
            visitorContext.uasmBuilder.AddJumpLabel(forLoopStart);

            JumpLabel forLoopContinue = visitorContext.labelTable.GetNewJumpLabel("forLoopContinue");

            JumpLabel forLoopEnd = visitorContext.labelTable.GetNewJumpLabel("forLoopEnd");

            if (node.Condition != null)
            {
                SymbolDefinition conditionSymbol = null;
                using (ExpressionCaptureScope conditionScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.Condition);
                    conditionSymbol = HandleImplicitBoolCast(conditionScope.ExecuteGet());
                }

                visitorContext.uasmBuilder.AddPush(conditionSymbol);
                visitorContext.uasmBuilder.AddJumpIfFalse(forLoopEnd);
            }

            visitorContext.continueLabelStack.Push(forLoopContinue);
            visitorContext.breakLabelStack.Push(forLoopEnd);

            Visit(node.Statement);

            visitorContext.continueLabelStack.Pop();
            visitorContext.breakLabelStack.Pop();

            visitorContext.uasmBuilder.AddJumpLabel(forLoopContinue);

            foreach (ExpressionSyntax incrementor in node.Incrementors)
            {
                using (ExpressionCaptureScope voidReturnScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(incrementor);
                }
            }

            visitorContext.uasmBuilder.AddJump(forLoopStart);

            visitorContext.uasmBuilder.AddJumpLabel(forLoopEnd);

            visitorContext.PopTable();
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolTable forEachSymbolTable = new SymbolTable(visitorContext.resolverContext, visitorContext.topTable);
            visitorContext.PushTable(forEachSymbolTable);

            System.Type valueSymbolType = null;

            using (ExpressionCaptureScope symbolTypeCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);
                valueSymbolType = symbolTypeCapture.captureType;
            }

            SymbolDefinition valueSymbol = null;

            SymbolDefinition indexSymbol = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);

            SymbolDefinition arraySymbol = null;

            using (ExpressionCaptureScope arrayCaptureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Expression);
                arraySymbol = arrayCaptureScope.ExecuteGet();

                if (arraySymbol.symbolCsType == typeof(string))
                {
                    using (ExpressionCaptureScope charArrayMethodCapture = new ExpressionCaptureScope(visitorContext, null))
                    {
                        charArrayMethodCapture.SetToLocalSymbol(arraySymbol);
                        charArrayMethodCapture.ResolveAccessToken("ToCharArray");
                        arraySymbol = charArrayMethodCapture.Invoke(new SymbolDefinition[] { });
                    }
                }

                if (!arraySymbol.symbolCsType.IsArray)
                    throw new System.Exception("foreach loop must iterate an array type");
            }

            if (node.Type.IsVar)
                valueSymbol = visitorContext.topTable.CreateNamedSymbol(node.Identifier.Text, arraySymbol.userCsType.GetElementType(), SymbolDeclTypeFlags.Local);
            else
                valueSymbol = visitorContext.topTable.CreateNamedSymbol(node.Identifier.Text, valueSymbolType, SymbolDeclTypeFlags.Local);

            using (ExpressionCaptureScope indexResetterScope = new ExpressionCaptureScope(visitorContext, null))
            {
                indexResetterScope.SetToLocalSymbol(indexSymbol);
                SymbolDefinition constIntSet0 = visitorContext.topTable.CreateConstSymbol(typeof(int), 0);
                indexResetterScope.ExecuteSet(constIntSet0);
            }

            SymbolDefinition arrayLengthSymbol = null;
            using (ExpressionCaptureScope lengthGetterScope = new ExpressionCaptureScope(visitorContext, null))
            {
                lengthGetterScope.SetToLocalSymbol(arraySymbol);
                lengthGetterScope.ResolveAccessToken("Length");
                arrayLengthSymbol = lengthGetterScope.ExecuteGet();
            }

            JumpLabel loopExitLabel = visitorContext.labelTable.GetNewJumpLabel("foreachLoopExit");
            JumpLabel loopStartLabel = visitorContext.labelTable.GetNewJumpLabel("foreachLoopStart");
            JumpLabel loopContinueLabel = visitorContext.labelTable.GetNewJumpLabel("foreachLoopContinue");
            visitorContext.uasmBuilder.AddJumpLabel(loopStartLabel);

            SymbolDefinition conditionSymbol = null;
            using (ExpressionCaptureScope conditionExecuteScope = new ExpressionCaptureScope(visitorContext, null))
            {
                conditionExecuteScope.SetToMethods(GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                conditionSymbol = conditionExecuteScope.Invoke(new SymbolDefinition[] { indexSymbol, arrayLengthSymbol });
            }

            visitorContext.uasmBuilder.AddPush(conditionSymbol);
            visitorContext.uasmBuilder.AddJumpIfFalse(loopExitLabel);

            using (ExpressionCaptureScope indexAccessExecuteScope = new ExpressionCaptureScope(visitorContext, null))
            {
                indexAccessExecuteScope.SetToLocalSymbol(arraySymbol);
                using (SymbolDefinition.COWValue arrayIndex = indexSymbol.GetCOWValue(visitorContext.uasmBuilder, visitorContext.topTable))
                {
                    indexAccessExecuteScope.HandleArrayIndexerAccess(arrayIndex, valueSymbol);
                }

                // Copy elision should make this a no-op unless conversion is required
                using (ExpressionCaptureScope valueSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    valueSetScope.SetToLocalSymbol(valueSymbol);
                    valueSetScope.ExecuteSet(indexAccessExecuteScope.ExecuteGet());
                }
            }

            visitorContext.continueLabelStack.Push(loopContinueLabel);
            visitorContext.breakLabelStack.Push(loopExitLabel);

            Visit(node.Statement);

            visitorContext.continueLabelStack.Pop();
            visitorContext.breakLabelStack.Pop();

            visitorContext.uasmBuilder.AddJumpLabel(loopContinueLabel);

            using (ExpressionCaptureScope incrementExecuteScope = new ExpressionCaptureScope(visitorContext, null, indexSymbol))
            {
                incrementExecuteScope.SetToMethods(GetOperators(typeof(int), BuiltinOperatorType.Addition));
                SymbolDefinition constIntIncrement = visitorContext.topTable.CreateConstSymbol(typeof(int), 1);
                
                SymbolDefinition incrementResultSymbol = incrementExecuteScope.Invoke(new SymbolDefinition[] { indexSymbol, constIntIncrement });

                using (ExpressionCaptureScope indexSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    indexSetScope.SetToLocalSymbol(indexSymbol);
                    indexSetScope.ExecuteSet(incrementResultSymbol);
                }
            }

            visitorContext.uasmBuilder.AddJump(loopStartLabel);
            visitorContext.uasmBuilder.AddJumpLabel(loopExitLabel);

            visitorContext.PopTable();
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition requestedResultSymbol = visitorContext.topCaptureScope.requestedDestination;
            SymbolDefinition conditionSymbol = null;

            using (ExpressionCaptureScope conditionCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = HandleImplicitBoolCast(conditionCapture.ExecuteGet());
            }

            JumpLabel conditionExpressionEnd = visitorContext.labelTable.GetNewJumpLabel("conditionExpressionEnd");

            JumpLabel falseConditionStart = visitorContext.labelTable.GetNewJumpLabel("conditionFailStart");

            using (ExpressionCaptureScope outputScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope, requestedResultSymbol))
            {
                visitorContext.uasmBuilder.AddPush(conditionSymbol);
                visitorContext.uasmBuilder.AddJumpIfFalse(falseConditionStart);

                SymbolDefinition resultSymbol = requestedResultSymbol;

                using (ExpressionCaptureScope lhsScope = new ExpressionCaptureScope(visitorContext, null, resultSymbol))
                {
                    Visit(node.WhenTrue);

                    if (resultSymbol == null)
                    {
                        // We didn't have a requested output symbol, so allocate one now.
                        resultSymbol = outputScope.AllocateOutputSymbol(lhsScope.GetReturnType());
                    }
                    
                    outputScope.SetToLocalSymbol(resultSymbol);
                    outputScope.ExecuteSet(lhsScope.ExecuteGet());
                }

                visitorContext.uasmBuilder.AddJump(conditionExpressionEnd);
                visitorContext.uasmBuilder.AddJumpLabel(falseConditionStart);

                using (ExpressionCaptureScope rhsScope = new ExpressionCaptureScope(visitorContext, null, resultSymbol))
                {
                    Visit(node.WhenFalse);

                    outputScope.ExecuteSet(rhsScope.ExecuteGet());
                }

                visitorContext.uasmBuilder.AddJumpLabel(conditionExpressionEnd);
            }
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            JumpLabel switchExitLabel = visitorContext.labelTable.GetNewJumpLabel("switchStatementExit");

            visitorContext.breakLabelStack.Push(switchExitLabel);

            SymbolDefinition switchExpressionSymbol = null;
            using (ExpressionCaptureScope switchExpressionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Expression);
                switchExpressionSymbol = switchExpressionScope.ExecuteGet();
            }

            JumpLabel[] sectionJumps = new JumpLabel[node.Sections.Count];

            JumpLabel defaultJump = null;

            JumpLabel nextLabelJump = visitorContext.labelTable.GetNewJumpLabel("nextSwitchLabelJump");

            // Iterate all the sections and build the condition jumps first
            for (int i = 0; i < node.Sections.Count; ++i)
            {
                SwitchSectionSyntax switchSection = node.Sections[i];
                JumpLabel sectionJump = visitorContext.labelTable.GetNewJumpLabel("switchStatmentSectionJump");
                sectionJumps[i] = sectionJump;

                for (int j = 0; j < switchSection.Labels.Count; ++j)
                {
                    SwitchLabelSyntax switchLabel = switchSection.Labels[j];
                    SymbolDefinition switchLabelValue = null;

                    if (switchLabel is DefaultSwitchLabelSyntax)
                    {
                        UpdateSyntaxNode(switchLabel);
                        defaultJump = sectionJump;
                        continue;
                    }

                    visitorContext.uasmBuilder.AddJumpLabel(nextLabelJump);
                    nextLabelJump = visitorContext.labelTable.GetNewJumpLabel("nextSwitchLabelJump");

                    SymbolDefinition conditionEqualitySymbol = null;

                    using (ExpressionCaptureScope conditionValueCapture = new ExpressionCaptureScope(visitorContext, null))
                    {
                        Visit(switchLabel);

                        using (ExpressionCaptureScope equalityCheckScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            List<MethodInfo> operatorMethods = new List<MethodInfo>();
                            operatorMethods.AddRange(UdonSharpUtils.GetOperators(switchExpressionSymbol.symbolCsType, BuiltinOperatorType.Equality));
                            operatorMethods.AddRange(GetImplicitHigherPrecisionOperator(switchExpressionSymbol.symbolCsType, conditionValueCapture.GetReturnType(), BuiltinOperatorType.Equality));

                            // The condition has a numeric value that needs to be converted for the condition
                            // This is done on the condition symbol because once constant folding is implemented, this will turn into a nop at runtime
                            if (visitorContext.resolverContext.FindBestOverloadFunction(operatorMethods.ToArray(), new List<System.Type> { switchExpressionSymbol.symbolCsType, conditionValueCapture.GetReturnType() }) == null && 
                                UdonSharpUtils.IsNumericExplicitCastValid(conditionValueCapture.GetReturnType(), switchExpressionSymbol.symbolCsType))
                            {
                                SymbolDefinition convertedNumericType = visitorContext.topTable.CreateUnnamedSymbol(conditionValueCapture.GetReturnType(), SymbolDeclTypeFlags.Internal);

                                using (ExpressionCaptureScope numericConversionScope = new ExpressionCaptureScope(visitorContext, null))
                                {
                                    numericConversionScope.SetToLocalSymbol(convertedNumericType);
                                    numericConversionScope.ExecuteSetDirect(conditionValueCapture, true);
                                }

                                switchLabelValue = convertedNumericType;
                                operatorMethods.AddRange(UdonSharpUtils.GetOperators(switchLabelValue.symbolCsType, BuiltinOperatorType.Equality));
                                operatorMethods.AddRange(GetImplicitHigherPrecisionOperator(switchExpressionSymbol.symbolCsType, switchLabelValue.symbolCsType, BuiltinOperatorType.Equality));
                            }
                            else
                            {
                                switchLabelValue = conditionValueCapture.ExecuteGet();
                            }

                            equalityCheckScope.SetToMethods(operatorMethods.ToArray());
                            conditionEqualitySymbol = equalityCheckScope.Invoke(new SymbolDefinition[] { switchExpressionSymbol, switchLabelValue });
                        }
                    }

                    // Jump past the jump to the section if false
                    visitorContext.uasmBuilder.AddJumpIfFalse(nextLabelJump, conditionEqualitySymbol);
                    visitorContext.uasmBuilder.AddJump(sectionJump);
                }
            }

            visitorContext.uasmBuilder.AddJumpLabel(nextLabelJump);

            if (defaultJump != null)
                visitorContext.uasmBuilder.AddJump(defaultJump);
            else
                visitorContext.uasmBuilder.AddJump(switchExitLabel); 

            // Now fill out the code sections for each condition and resolve the jump labels for each section
            for (int i = 0; i < node.Sections.Count; ++i)
            {
                visitorContext.uasmBuilder.AddJumpLabel(sectionJumps[i]);

                visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));

                foreach (StatementSyntax statment in node.Sections[i].Statements)
                {
                    Visit(statment);
                }

                visitorContext.PopTable();
            }

            visitorContext.uasmBuilder.AddJumpLabel(switchExitLabel);
            visitorContext.breakLabelStack.Pop();
        }

        public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Value);
        }

        public override void VisitGotoStatement(GotoStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotImplementedException("UdonSharp does not yet support goto");
        }

        public override void VisitLabeledStatement(LabeledStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotImplementedException("UdonSharp does not yet support labeled statements");
        }

        private List<System.Type> GetTypeArgumentList(TypeArgumentListSyntax typeArgumentList)
        {
            UpdateSyntaxNode(typeArgumentList);

            List<System.Type> argumentTypes = new List<System.Type>();

            foreach (TypeSyntax typeSyntax in typeArgumentList.Arguments)
            {
                using (ExpressionCaptureScope typeCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(typeSyntax);

                    if (!typeCaptureScope.IsType())
                        throw new System.ArgumentException("Generic argument must be a valid type");

                    argumentTypes.Add(visitorContext.resolverContext.RemapBaseType(typeCaptureScope.captureType));
                }
            }

            return argumentTypes;
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            UpdateSyntaxNode(node);

            if (visitorContext.topCaptureScope != null)
            {
                visitorContext.topCaptureScope.ResolveAccessToken(node.Identifier.ValueText);
                visitorContext.topCaptureScope.HandleGenericAccess(GetTypeArgumentList(node.TypeArgumentList));
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition requestedDestination = visitorContext.requestedDestination;

            // Grab the external scope so that the method call can propagate its output upwards
            ExpressionCaptureScope externalScope = visitorContext.PopCaptureScope();

            if (externalScope != null)
                visitorContext.PushCaptureScope(externalScope);

            using (ExpressionCaptureScope methodCaptureScope = new ExpressionCaptureScope(visitorContext, null, requestedDestination))
            {
                Visit(node.Expression);
                
                if (!methodCaptureScope.IsMethod())
                    throw new System.Exception("Invocation requires method expression!");
                
                List<SymbolDefinition.COWValue> invocationArgs = new List<SymbolDefinition.COWValue>();

                //visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));

                SymbolDefinition[] argDestinations = methodCaptureScope.GetLocalMethodArgumentSymbols();

                for (int i = 0; i < node.ArgumentList.Arguments.Count; i++)
                {
                    ArgumentSyntax argument = node.ArgumentList.Arguments[i];
                    SymbolDefinition argDestination = argDestinations != null ? argDestinations[i] : null;

                    using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, null, argDestination))
                    {
                        Visit(argument.Expression);

                        invocationArgs.Add(captureScope.ExecuteGetCOW().AddRef());
                    }
                }

                SymbolDefinition functionReturnValue = methodCaptureScope.Invoke(
                    invocationArgs.Select((arg) => arg.symbol).ToArray()
                );

                using (ExpressionCaptureScope returnValPropagationScope = new ExpressionCaptureScope(visitorContext, externalScope))
                {
                    returnValPropagationScope.SetToLocalSymbol(functionReturnValue);
                }

                invocationArgs.ForEach((arg) => arg.Dispose());
            }
        }

        public override void VisitNullableType(NullableTypeSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotImplementedException("Nullable types are not currently supported by UdonSharp");
        }

        // Constructors
        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition requestedDestination = visitorContext.requestedDestination;

            System.Type newType = null;

            using (ExpressionCaptureScope constructorTypeScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);
                newType = constructorTypeScope.captureType;
            }

            if (node.Initializer != null)
                throw new System.NotImplementedException("Object initializers are not yet supported by UdonSharp");

            using (ExpressionCaptureScope creationCaptureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                // Use the default constructor by just making a constant of the correct type in Udon
                
                SymbolDefinition.COWValue[] argValues = new SymbolDefinition.COWValue[node.ArgumentList.Arguments.Count];

                for (int i = 0; i < argValues.Length; ++i)
                {
                    using (ExpressionCaptureScope argCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        Visit(node.ArgumentList.Arguments[i]);
                        argValues[i] = argCaptureScope.ExecuteGetCOW().AddRef();
                    }
                }

                using (ExpressionCaptureScope constructorMethodScope = new ExpressionCaptureScope(visitorContext, null, requestedDestination))
                {
                    MethodBase[] constructors = newType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                    try
                    {
                        constructorMethodScope.SetToMethods(constructors);
                        SymbolDefinition[] argSymbols = argValues.Select((v) => v.symbol).ToArray();
                        creationCaptureScope.SetToLocalSymbol(constructorMethodScope.Invoke(argSymbols));
                    }
                    catch (System.Exception e)
                    {
                        // Udon will default initialize structs and such so it doesn't expose default constructors for stuff like Vector3
                        // This is a weird case, we could technically check if the type exists in Udon here, 
                        //   but it's totally valid to store a type that's undefined by Udon on the heap since they are all object.
                        if (argValues.Length > 0 || !newType.IsValueType)
                            throw e;

                        creationCaptureScope.SetToLocalSymbol(visitorContext.topTable.CreateConstSymbol(newType, null));
                    }
                }

                foreach (SymbolDefinition.COWValue val in argValues)
                {
                    val.Dispose();
                }
            }
        }

        public override void VisitArgument(ArgumentSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Expression);
        }

        public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition interpolatedString = visitorContext.topTable.CreateNamedSymbol("interpolatedStr", typeof(string), SymbolDeclTypeFlags.Internal);

            using (ExpressionCaptureScope stringConcatMethodScope = new ExpressionCaptureScope(visitorContext, null))
            {
                stringConcatMethodScope.SetToMethods(GetOperators(typeof(string), BuiltinOperatorType.Addition));

                for (int i = 0; i < node.Contents.Count; ++i)
                {
                    var interpolatedContents = node.Contents[i];

                    using (ExpressionCaptureScope stringExpressionCapture = new ExpressionCaptureScope(visitorContext, null))
                    {
                        Visit(interpolatedContents);

                        using (ExpressionCaptureScope setInterpolatedStringScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            setInterpolatedStringScope.SetToLocalSymbol(interpolatedString);

                            // This needs to be moved to direct set as well when we have support

                            if (i == 0)
                                setInterpolatedStringScope.ExecuteSet(stringExpressionCapture.ExecuteGet());
                            else
                                setInterpolatedStringScope.ExecuteSet(stringConcatMethodScope.Invoke(new SymbolDefinition[] { interpolatedString, stringExpressionCapture.ExecuteGet() }));
                        }
                    }
                }
            }

            using (ExpressionCaptureScope interpolatedStringCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                interpolatedStringCapture.SetToLocalSymbol(interpolatedString);
            }
        }

        public override void VisitInterpolation(InterpolationSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition interpolationResultSymbol = null;
            
            using (ExpressionCaptureScope interpolatedExpressionCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Expression);

                interpolationResultSymbol = interpolatedExpressionCapture.ExecuteGet();
            }

            // We can evaluate the statement like usual and just return a string
            if (node.FormatClause == null && node.AlignmentClause == null)
            {
                if (interpolationResultSymbol.symbolCsType != typeof(string))
                {
                    using (ExpressionCaptureScope toStringScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        toStringScope.SetToLocalSymbol(interpolationResultSymbol);
                        toStringScope.ResolveAccessToken("ToString");

                        interpolationResultSymbol = toStringScope.Invoke(new SymbolDefinition[] { });
                    }
                }
            }
            else
            {
                SymbolDefinition stringFormatSymbol = null;
                
                if (node.AlignmentClause == null) // If the alignment clause is null then we can just construct the format string in place
                {
                    stringFormatSymbol = visitorContext.topTable.CreateConstSymbol(typeof(string), "{0:" + node.FormatClause.FormatStringToken.ValueText + "}");
                }
                else // Otherwise, we need to concat the strings together which will have a decent cost until constant expressions are handled
                {
                    stringFormatSymbol = visitorContext.topTable.CreateNamedSymbol("formatStr", typeof(string), SymbolDeclTypeFlags.Internal);

                    using (ExpressionCaptureScope alignmentCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        Visit(node.AlignmentClause);

                        if (alignmentCaptureScope.GetReturnType() != typeof(int))
                            throw new System.ArgumentException("String interpolation alignment must be a signed integer");

                        SymbolDefinition alignmentStringSymbol = visitorContext.topTable.CreateUnnamedSymbol(typeof(string), SymbolDeclTypeFlags.Internal);

                        using (ExpressionCaptureScope alignmentToStringScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            alignmentToStringScope.SetToLocalSymbol(alignmentCaptureScope.ExecuteGet());
                            alignmentToStringScope.ResolveAccessToken("ToString");

                            using (ExpressionCaptureScope alignmentSetterScope = new ExpressionCaptureScope(visitorContext, null))
                            {
                                alignmentSetterScope.SetToLocalSymbol(alignmentStringSymbol);
                                alignmentSetterScope.ExecuteSet(alignmentToStringScope.Invoke(new SymbolDefinition[] { }));
                            }
                        }

                        using (ExpressionCaptureScope stringFormatSetScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            stringFormatSetScope.SetToLocalSymbol(stringFormatSymbol);
                            stringFormatSetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(string), "{0,"));

                            using (ExpressionCaptureScope stringConcatMethodScope = new ExpressionCaptureScope(visitorContext, null))
                            {
                                stringConcatMethodScope.SetToMethods(GetOperators(typeof(string), BuiltinOperatorType.Addition));

                                stringFormatSetScope.ExecuteSet(stringConcatMethodScope.Invoke(new SymbolDefinition[] { stringFormatSymbol, alignmentStringSymbol }));

                                if (node.FormatClause != null)
                                {
                                    stringFormatSetScope.ExecuteSet(stringConcatMethodScope.Invoke(new SymbolDefinition[] {
                                        stringFormatSymbol,
                                        visitorContext.topTable.CreateConstSymbol(typeof(string), ":" + node.FormatClause.FormatStringToken.ValueText) }));
                                }

                                stringFormatSetScope.ExecuteSet(stringConcatMethodScope.Invoke(new SymbolDefinition[] {
                                    stringFormatSymbol,
                                    visitorContext.topTable.CreateConstSymbol(typeof(string), "}") }));
                            }
                        }
                    }
                }

                using (ExpressionCaptureScope stringFormatExpression = new ExpressionCaptureScope(visitorContext, null))
                {
                    stringFormatExpression.SetToType(typeof(string));
                    stringFormatExpression.ResolveAccessToken("Format");

                    interpolationResultSymbol = stringFormatExpression.Invoke(new SymbolDefinition[] { stringFormatSymbol, interpolationResultSymbol });
                }
            }

            using (ExpressionCaptureScope interpolationResultCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                interpolationResultCapture.SetToLocalSymbol(interpolationResultSymbol);
            }
        }

        public override void VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
        {
            UpdateSyntaxNode(node);

            using (ExpressionCaptureScope stringGenScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                stringGenScope.SetToLocalSymbol(visitorContext.topTable.CreateConstSymbol(typeof(string), node.TextToken.ValueText));
            }
        }
    }
}
