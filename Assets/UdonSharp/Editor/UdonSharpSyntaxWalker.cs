
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace UdonSharp.Compiler
{
    public class UdonSharpSyntaxWalker : CSharpSyntaxWalker
    {
        public enum UdonSharpSyntaxWalkerDepth
        {
            Class,
            ClassDefinitions,
            ClassMemberBodies,
        }

        public ASTVisitorContext visitorContext;

        protected Stack<string> namespaceStack = new Stack<string>();

        UdonSharpSyntaxWalkerDepth syntaxWalkerDepth;

        public UdonSharpSyntaxWalker(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, ClassDebugInfo classDebugInfo = null)
            : base(SyntaxWalkerDepth.Node)
        {
            syntaxWalkerDepth = UdonSharpSyntaxWalkerDepth.ClassMemberBodies;
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable, classDebugInfo);
        }

        public UdonSharpSyntaxWalker(UdonSharpSyntaxWalkerDepth depth, ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, ClassDebugInfo classDebugInfo = null)
            : base(SyntaxWalkerDepth.Node)
        {
            syntaxWalkerDepth = depth;
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable, classDebugInfo);
        }

        protected void UpdateSyntaxNode(SyntaxNode node)
        {
            visitorContext.currentNode = node;

            if (visitorContext.debugInfo != null && !visitorContext.pauseDebugInfoWrite)
                visitorContext.debugInfo.UpdateSyntaxNode(node);
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            UpdateSyntaxNode(node);
            base.DefaultVisit(node);
        }

        public override void VisitAttributeArgument(AttributeArgumentSyntax node)
        {
            UpdateSyntaxNode(node);
            Visit(node.Expression);
        }

        public override void VisitSimpleBaseType(SimpleBaseTypeSyntax node)
        {
            UpdateSyntaxNode(node);
            Visit(node.Type);
        }

        public override void VisitEmptyStatement(EmptyStatementSyntax node)
        {
            UpdateSyntaxNode(node);
        }

        public override void VisitNullableType(NullableTypeSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotImplementedException("Nullable types are not currently supported by UdonSharp");
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotSupportedException("UdonSharp does not yet support user defined enums");
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

        // Not really strictly needed since the compiler for the normal C# will yell at people for us if they attempt to access something not valid for `this`
        public override void VisitThisExpression(ThisExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken("this");
        }

        public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Expression);
        }

        public override void VisitQualifiedName(QualifiedNameSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Left);
            Visit(node.Right);
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

                    argumentTypes.Add(UdonSharpUtils.RemapBaseType(typeCaptureScope.captureType));
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

        public override void VisitArgument(ArgumentSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Expression);
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            List<string> namespaces = new List<string>();

            SyntaxToken lastToken = node.Name.GetLastToken();
            SyntaxToken currentToken = node.Name.GetFirstToken();

            while (currentToken != null)
            {
                if (currentToken.Text != ".")
                    namespaces.Add(currentToken.Text);

                if (currentToken == lastToken)
                    break;

                currentToken = currentToken.GetNextToken();
            }

            foreach (string currentNamespace in namespaces)
                namespaceStack.Push(currentNamespace);

            foreach (UsingDirectiveSyntax usingDirective in node.Usings)
                Visit(usingDirective);

            foreach (MemberDeclarationSyntax memberDeclaration in node.Members)
                Visit(memberDeclaration);

            for (int i = 0; i < namespaces.Count; ++i)
                namespaceStack.Pop();
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);
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
            }

            if (node.AttributeLists != null)
            {
                foreach (AttributeListSyntax attributeList in node.AttributeLists)
                {
                    foreach (AttributeSyntax attribute in attributeList.Attributes)
                    {
                        System.Type captureType = null;

                        using (ExpressionCaptureScope attributeTypeScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            attributeTypeScope.isAttributeCaptureScope = true;

                            Visit(attribute.Name);

                            captureType = attributeTypeScope.captureType;
                        }

                        if (captureType != null && captureType == typeof(UdonBehaviourSyncModeAttribute))
                        {
                            if (attribute.ArgumentList != null &&
                                attribute.ArgumentList.Arguments != null &&
                                attribute.ArgumentList.Arguments.Count == 1)
                            {
                                using (ExpressionCaptureScope attributeCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                                {
                                    Visit(attribute.ArgumentList.Arguments[0].Expression);

                                    if (!attributeCaptureScope.IsEnum())
                                        throw new System.Exception("Invalid attribute argument provided for behaviour sync");

                                    visitorContext.behaviourSyncMode = (BehaviourSyncMode)attributeCaptureScope.GetEnumValue();
                                }
                            }
                        }
                    }
                }
            }

            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassDefinitions || 
                syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitClassDeclaration(node);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitVariableDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (syntaxWalkerDepth == UdonSharpSyntaxWalkerDepth.ClassMemberBodies)
                base.VisitPropertyDeclaration(node);
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
        
        // Boilerplate to have resolution work correctly
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            UpdateSyntaxNode(node);
            using (ExpressionCaptureScope namespaceCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                if (node.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
                    throw new System.NotSupportedException("UdonSharp does not yet support static using directives");

                if (node.Alias != null)
                    throw new System.NotSupportedException("UdonSharp does not yet support namespace alias directives");

                Visit(node.Name);

                if (!namespaceCapture.IsNamespace())
                    throw new System.Exception("Did not capture a valid namespace");

                visitorContext.resolverContext.AddNamespace(namespaceCapture.captureNamespace);
            }
        }

        protected void HandleNameOfExpression(InvocationExpressionSyntax node)
        {
            SyntaxNode currentNode = node.ArgumentList.Arguments[0].Expression;
            string currentName = "";

            while (currentNode != null)
            {
                switch (currentNode.Kind())
                {
                    case SyntaxKind.SimpleMemberAccessExpression:
                        MemberAccessExpressionSyntax memberNode = (MemberAccessExpressionSyntax)currentNode;
                        currentName = memberNode.Name.ToString();
                        currentNode = memberNode.Name;
                        break;
                    case SyntaxKind.IdentifierName:
                        IdentifierNameSyntax identifierName = (IdentifierNameSyntax)currentNode;
                        currentName = identifierName.ToString();
                        currentNode = null;
                        break;
                    default:
                        currentNode = null;
                        break;
                }

                if (currentNode != null)
                    UpdateSyntaxNode(currentNode);
            }

            if (currentName == "")
                throw new System.ArgumentException("Expression does not have a name");

            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.SetToLocalSymbol(visitorContext.topTable.CreateConstSymbol(typeof(string), currentName));
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            if (node.Expression != null && node.Expression.ToString() == "nameof") // nameof is not a dedicated node and the Kind of the node isn't the nameof kind for whatever reason...
            {
                HandleNameOfExpression(node);
                return;
            }
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

        protected List<SymbolDefinition> HandleVariableDeclaration(VariableDeclarationSyntax node, SymbolDeclTypeFlags symbolType, UdonSyncMode syncMode)
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

            if (!visitorContext.resolverContext.IsValidUdonType(variableType))
                throw new System.NotSupportedException($"Udon does not support variables of type '{variableType.Name}' yet");

            return newSymbols;
        }

        protected void VerifySyncValidForType(System.Type typeToSync, UdonSyncMode syncMode)
        {
            if (syncMode == UdonSyncMode.NotSynced)
                return;

            if (visitorContext.behaviourSyncMode == BehaviourSyncMode.NoVariableSync)
                throw new System.Exception($"Cannot sync variable because behaviour is set to NoVariableSync, change the behaviour sync mode to sync variables");

            if (!VRC.Udon.UdonNetworkTypes.CanSync(typeToSync) && 
                typeToSync != typeof(uint) && typeToSync != typeof(uint[])) // Workaround for the uint types missing from the syncable type list >_>
                throw new System.NotSupportedException($"Udon does not currently support syncing of the type '{UdonSharpUtils.PrettifyTypeName(typeToSync)}'");
            else if (syncMode == UdonSyncMode.Linear && !VRC.Udon.UdonNetworkTypes.CanSyncLinear(typeToSync))
                throw new System.NotSupportedException($"Udon does not support linear interpolation of the synced type '{UdonSharpUtils.PrettifyTypeName(typeToSync)}'");
            else if (syncMode == UdonSyncMode.Smooth && !VRC.Udon.UdonNetworkTypes.CanSyncSmooth(typeToSync))
                throw new System.NotSupportedException($"Udon does not support smooth interpolation of the synced type '{UdonSharpUtils.PrettifyTypeName(typeToSync)}'");

            if (visitorContext.behaviourSyncMode == BehaviourSyncMode.Manual && syncMode != UdonSyncMode.None)
                    throw new System.NotSupportedException($"Udon does not support variable tweening when the behaviour is in Manual sync mode");
            else if (visitorContext.behaviourSyncMode == BehaviourSyncMode.Continuous && typeToSync.IsArray)
                throw new System.NotSupportedException($"Syncing of array type {UdonSharpUtils.PrettifyTypeName(typeToSync.GetElementType())}[] is only supported in manual sync mode");
        }
    }
}
