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

        public ASTVisitorContext(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTableIn)
        {
            resolverContext = resolver;

            symbolTableStack = new Stack<SymbolTable>();
            symbolTableStack.Push(rootTable);

            //labelTable = new LabelTable();
            labelTable = labelTableIn;

            uasmBuilder = new AssemblyBuilder();

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
    }

    /// <summary>
    /// This is where most of the work is done to convert a C# AST into intermediate UAsm
    /// </summary>
    public class ASTVisitor : CSharpSyntaxWalker
    {
        public ASTVisitorContext visitorContext { get; private set; }

        public ASTVisitor(ResolverContext resolver, SymbolTable rootTable, LabelTable labelTable, List<MethodDefinition> methodDefinitions)
            :base(SyntaxWalkerDepth.Node)
        {
            visitorContext = new ASTVisitorContext(resolver, rootTable, labelTable);
            visitorContext.returnJumpTarget = rootTable.CreateNamedSymbol("returnTarget", typeof(uint), SymbolDeclTypeFlags.Internal);
            visitorContext.definedMethods = methodDefinitions;
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

        private void UpdateSyntaxNode(SyntaxNode node)
        {
            visitorContext.currentNode = node;
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

            Visit(node.Expression);
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

            foreach (UsingDirectiveSyntax usingDirective in node.Usings)
                Visit(usingDirective);

            foreach (MemberDeclarationSyntax memberDeclaration in node.Members)
                Visit(memberDeclaration);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

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

            Visit(node.ElementType);
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

            if (node.Initializer != null)
                throw new System.NotSupportedException("UdonSharp does not yet support initializer lists for arrays");

            using (ExpressionCaptureScope arrayTypeScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);
                arrayType = arrayTypeScope.captureType.MakeArrayType();
            }

            using (ExpressionCaptureScope varCaptureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                varCaptureScope.SetToLocalSymbol(visitorContext.topTable.CreateUnnamedSymbol(arrayType, SymbolDeclTypeFlags.Internal));
                
                if (node.Type.RankSpecifiers.Count != 1)
                    throw new System.NotSupportedException("UdonSharp does not support multidimensional or jagged arrays at the moment");

                SymbolDefinition arrayRankSymbol = null;

                using (ExpressionCaptureScope rankCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.Type.RankSpecifiers[0]);
                    arrayRankSymbol = rankCapture.ExecuteGet();
                }

                using (ExpressionCaptureScope constructorCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    constructorCaptureScope.SetToMethods(arrayType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
                    varCaptureScope.ExecuteSet(constructorCaptureScope.Invoke(new SymbolDefinition[] { arrayRankSymbol }));
                }
            }
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            using (ExpressionCaptureScope elementAccessExpression = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                Visit(node.Expression);

                if (node.ArgumentList.Arguments.Count != 1)
                    throw new System.NotSupportedException("UdonSharp does not support multidimensional or jagged array accesses yet");

                SymbolDefinition indexerSymbol = null;

                using (ExpressionCaptureScope indexerCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.ArgumentList.Arguments[0]);
                    indexerSymbol = indexerCaptureScope.ExecuteGet();
                }

                elementAccessExpression.HandleArrayIndexerAccess(indexerSymbol);
            }
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            // todo: make attributes for syncing and handle them here
            bool isPublic = node.Modifiers.HasModifier("public");

            HandleVariableDeclaration(node.Declaration, isPublic ? SymbolDeclTypeFlags.Public : SymbolDeclTypeFlags.Private);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            HandleVariableDeclaration(node, SymbolDeclTypeFlags.Local);
        }

        public void HandleVariableDeclaration(VariableDeclarationSyntax node, SymbolDeclTypeFlags symbolType)
        {
            UpdateSyntaxNode(node);

            bool isVar = node.Type.IsVar;
            bool isArray = node.Type is ArrayTypeSyntax;

            System.Type variableType = null;

            if (!isVar)
            {
                using (ExpressionCaptureScope typeCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.Type);

                    if (!typeCapture.IsType())
                        throw new System.Exception("Type could not be parsed from variable declaration!");

                    variableType = typeCapture.captureType;

                    if (isArray)
                    {
                        variableType = variableType.MakeArrayType();
                    }
                }
            }

            foreach (VariableDeclaratorSyntax variableDeclarator in node.Variables)
            {
                string variableName = variableDeclarator.Identifier.ValueText;
                bool createdSymbol = false;

                using (ExpressionCaptureScope symbolCreationScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    SymbolDefinition newSymbol = null;

                    // Run the initializer if it exists
                    // Todo: Run the set on the new symbol scope from within the initializer scope for direct setting
                    if (variableDeclarator.Initializer != null && symbolType == SymbolDeclTypeFlags.Local)
                    {
                        using (ExpressionCaptureScope initializerCapture = new ExpressionCaptureScope(visitorContext, null))
                        {
                            Visit(variableDeclarator.Initializer);

                            if (newSymbol == null)
                            {
                                if (isVar)
                                    newSymbol = visitorContext.topTable.CreateNamedSymbol(variableName, initializerCapture.GetReturnType(), symbolType);
                                else
                                    newSymbol = visitorContext.topTable.CreateNamedSymbol(variableName, variableType, symbolType);
                                
                                symbolCreationScope.SetToLocalSymbol(newSymbol);
                                createdSymbol = true;
                            }

                            symbolCreationScope.ExecuteSet(initializerCapture.ExecuteGet());
                        }
                    }
                    else if (variableDeclarator.Initializer != null)
                    {
                        throw new System.NotImplementedException("UdonSharp does not yet support initializers on fields.");
                    }
                }

                if (!createdSymbol)
                {
                    using (ExpressionCaptureScope symbolCreationScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        SymbolDefinition newSymbol = visitorContext.topTable.CreateNamedSymbol(variableDeclarator.Identifier.ValueText, variableType, symbolType);

                        symbolCreationScope.SetToLocalSymbol(newSymbol);
                    }
                }
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
            }

            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.SetToLocalSymbol(visitorContext.topTable.CreateConstSymbol(typeof(System.Type), capturedType));
        }

        public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            UpdateSyntaxNode(node);

            using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
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

            SymbolDefinition rhsValue = null;

            using (ExpressionCaptureScope rhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Right);

                rhsValue = rhsCapture.ExecuteGet();
            }

            // Set parent to allow capture propagation for stuff like x = y = z;
            using (ExpressionCaptureScope lhsCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                Visit(node.Left);

                if (node.OperatorToken.Kind() == SyntaxKind.SimpleAssignmentExpression || node.OperatorToken.Kind() == SyntaxKind.EqualsToken)
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
                            break;
                        default:
                            throw new System.NotImplementedException($"Assignment operator {node.OperatorToken.Kind()} does not have handling");
                    }

                    using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null))
                    {
                        operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                        SymbolDefinition resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { lhsCapture.ExecuteGet(), rhsValue });

                        lhsCapture.ExecuteSet(resultSymbol);
                    }
                }
            }
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            ExpressionCaptureScope topScope = visitorContext.topCaptureScope;

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
                    case SyntaxKind.ExclamationToken:
                    case SyntaxKind.MinusToken:
                        operatorMethods.AddRange(GetOperators(operandCapture.GetReturnType(), node.OperatorToken.Kind()));
                        break;
                    default:
                        throw new System.NotImplementedException($"Handling for prefix token {node.OperatorToken.Kind()} is not implemented");
                }

                using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                    BuiltinOperatorType operatorType = SyntaxKindToBuiltinOperator(node.OperatorToken.Kind());

                    SymbolDefinition resultSymbol = null;

                    if (operatorType == BuiltinOperatorType.UnaryNegation ||
                        operatorType == BuiltinOperatorType.UnaryMinus)
                    {
                        resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { operandCapture.ExecuteGet() });

                        if (topScope != null)
                            topScope.SetToLocalSymbol(resultSymbol);
                    }
                    else
                    {
                        SymbolDefinition valueConstant = visitorContext.topTable.CreateConstSymbol(operandCapture.GetReturnType(), System.Convert.ChangeType(1, operandCapture.GetReturnType()));

                        resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { operandCapture.ExecuteGet(), valueConstant });

                        operandCapture.ExecuteSet(resultSymbol);

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

                using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                    using (ExpressionCaptureScope preIncrementValueReturn = new ExpressionCaptureScope(visitorContext, topScope))
                    {
                        SymbolDefinition preIncrementStore = visitorContext.topTable.CreateUnnamedSymbol(operandCapture.GetReturnType(), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);
                        preIncrementValueReturn.SetToLocalSymbol(preIncrementStore);

                        preIncrementValueReturn.ExecuteSet(operandCapture.ExecuteGet());
                    }

                    SymbolDefinition valueConstant = visitorContext.topTable.CreateConstSymbol(operandCapture.GetReturnType(), System.Convert.ChangeType(1, operandCapture.GetReturnType()));

                    SymbolDefinition resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { operandCapture.ExecuteGet(), valueConstant });

                    operandCapture.ExecuteSet(resultSymbol);
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

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            UpdateSyntaxNode(node);

            MethodDefinition definition = visitorContext.definedMethods.Where(e => e.originalMethodName == node.Identifier.ValueText).First();

            string functionName = node.Identifier.ValueText;
            bool isBuiltinEvent = visitorContext.resolverContext.ReplaceInternalEventName(ref functionName);

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

            using (ExpressionCaptureScope jumpResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                jumpResetScope.SetToLocalSymbol(visitorContext.returnJumpTarget);
                SymbolDefinition constEndAddrVal = visitorContext.topTable.CreateConstSymbol(typeof(uint), 0xFFFFFFFF);
                jumpResetScope.ExecuteSet(constEndAddrVal);
            }

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

            Visit(node.Body);

            visitorContext.topTable.FlattenTableCountersToGlobal();
            visitorContext.PopTable();

            visitorContext.uasmBuilder.AddJumpLabel(returnLabel);
            visitorContext.uasmBuilder.AddJumpLabel(definition.methodReturnPoint);
            visitorContext.uasmBuilder.AddJumpIndirect(visitorContext.returnJumpTarget);
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

        private readonly HashSet<System.Type> builtinTypes = new HashSet<System.Type> 
        {
            typeof(string),
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(char),
            typeof(decimal),
            typeof(double),
            typeof(float),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),
            typeof(object),
        };

        private MethodInfo[] GetOperators(System.Type type, BuiltinOperatorType builtinOperatorType)
        {
            List<MethodInfo> foundOperators = new List<MethodInfo>();

            // If it's a builtin type then create a fake operator methodinfo for it.
            // If this operator doesn't actually exist, it will get filtered by the overload finding
            if (builtinTypes.Contains(type))
                foundOperators.Add(new OperatorMethodInfo(type, builtinOperatorType));
            
            // Now look for operators that the type defines
            string operatorName = System.Enum.GetName(typeof(BuiltinOperatorType), builtinOperatorType);
            if (builtinOperatorType == BuiltinOperatorType.Multiplication)
                operatorName = "Multiply"; // Udon breaks standard naming with its multiplication overrides on base types

            operatorName = $"op_{operatorName}";

            foundOperators.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == operatorName));

            return foundOperators.ToArray();
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
                    return BuiltinOperatorType.LogicalNot;
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
            UpdateSyntaxNode(node);

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
            SymbolDefinition lhsValue = null;

            using (ExpressionCaptureScope rhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Right);

                rhsValue = rhsCapture.ExecuteGet();
            }

            ExpressionCaptureScope outerScope = visitorContext.topCaptureScope;

            using (ExpressionCaptureScope lhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Left);

                lhsValue = lhsCapture.ExecuteGet();

                System.Type lhsType = lhsValue.symbolCsType;
                System.Type rhsType = rhsValue.symbolCsType;

                List<MethodInfo> operatorMethods = new List<MethodInfo>();

                bool isAssignment = false;

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
                        operatorMethods = operatorMethods.Distinct().ToList();
                        isAssignment = false;
                        break;
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
                        operatorMethods.AddRange(GetOperators(lhsType, node.Kind()));
                        isAssignment = true;
                        break;
                    default:
                        throw new System.NotImplementedException($"Binary expression {node.Kind()} is not implemented");
                }

                using (ExpressionCaptureScope operatorMethodCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    operatorMethodCapture.SetToMethods(operatorMethods.ToArray());

                    SymbolDefinition resultSymbol = null;

                    try
                    {
                        resultSymbol = operatorMethodCapture.Invoke(new SymbolDefinition[] { lhsValue, rhsValue });
                    }
                    catch (System.Exception e)
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
                            throw e; // rethrow
                    }

                    if (isAssignment)
                    {
                        lhsCapture.ExecuteSet(resultSymbol);
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

            using (ExpressionCaptureScope castExpressionCapture = new ExpressionCaptureScope(visitorContext, null))
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

            using (ExpressionCaptureScope castOutCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                SymbolDefinition castOutSymbol = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

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
                using (ExpressionCaptureScope returnCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.Expression);

                    using (ExpressionCaptureScope returnOutSetter = new ExpressionCaptureScope(visitorContext, null))
                    {
                        returnOutSetter.SetToLocalSymbol(visitorContext.returnSymbol);
                        returnOutSetter.ExecuteSet(returnCaptureScope.ExecuteGet());
                    }
                }
            }

            visitorContext.uasmBuilder.AddJumpIndirect(visitorContext.returnJumpTarget);
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

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition conditionSymbol = null;

            using (ExpressionCaptureScope conditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = conditionScope.ExecuteGet();
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
                conditionSymbol = conditionScope.ExecuteGet();
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
                conditionSymbol = conditionScope.ExecuteGet();
            }

            visitorContext.uasmBuilder.AddPush(conditionSymbol);
            visitorContext.uasmBuilder.AddJumpIfFalse(doLoopEnd);

            visitorContext.uasmBuilder.AddJump(doLoopStart);

            visitorContext.uasmBuilder.AddJumpLabel(doLoopEnd);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            Visit(node.Declaration);

            foreach (ExpressionSyntax initializer in node.Initializers)
                Visit(initializer);

            JumpLabel forLoopStart = visitorContext.labelTable.GetNewJumpLabel("forLoopStart");
            visitorContext.uasmBuilder.AddJumpLabel(forLoopStart);

            JumpLabel forLoopEnd = visitorContext.labelTable.GetNewJumpLabel("forLoopEnd");

            SymbolDefinition conditionSymbol = null;
            using (ExpressionCaptureScope conditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = conditionScope.ExecuteGet();
            }

            visitorContext.uasmBuilder.AddPush(conditionSymbol);
            visitorContext.uasmBuilder.AddJumpIfFalse(forLoopEnd);

            visitorContext.continueLabelStack.Push(forLoopStart);
            visitorContext.breakLabelStack.Push(forLoopEnd);

            Visit(node.Statement);

            visitorContext.continueLabelStack.Pop();
            visitorContext.breakLabelStack.Pop();

            foreach (ExpressionSyntax incrementor in node.Incrementors)
                Visit(incrementor);

            visitorContext.uasmBuilder.AddJump(forLoopStart);

            visitorContext.uasmBuilder.AddJumpLabel(forLoopEnd);
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

                if (!arraySymbol.symbolCsType.IsArray)
                    throw new System.Exception("foreach loop must iterate an array type");
            }

            if (node.Type.IsVar)
                valueSymbol = visitorContext.topTable.CreateNamedSymbol(node.Identifier.Text, arraySymbol.symbolCsType.GetElementType(), SymbolDeclTypeFlags.Local);
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
                indexAccessExecuteScope.HandleArrayIndexerAccess(indexSymbol);

                // This should be a direct set instead of a copy, but that hasn't been implemented yet
                using (ExpressionCaptureScope valueSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    valueSetScope.SetToLocalSymbol(valueSymbol);
                    valueSetScope.ExecuteSet(indexAccessExecuteScope.ExecuteGet());
                }
            }

            visitorContext.continueLabelStack.Push(loopStartLabel);
            visitorContext.breakLabelStack.Push(loopExitLabel);

            Visit(node.Statement);

            visitorContext.continueLabelStack.Pop();
            visitorContext.breakLabelStack.Pop();

            using (ExpressionCaptureScope incrementExecuteScope = new ExpressionCaptureScope(visitorContext, null))
            {
                incrementExecuteScope.SetToMethods(GetOperators(typeof(int), BuiltinOperatorType.Addition));
                SymbolDefinition constIntIncrement = visitorContext.topTable.CreateConstSymbol(typeof(int), 1);

                // This should be a direct set, but I haven't implemented that yet so we do a wasteful copy here
                SymbolDefinition incrementResultSymbol = incrementExecuteScope.Invoke(new SymbolDefinition[] { indexSymbol, constIntIncrement });
                visitorContext.uasmBuilder.AddCopy(indexSymbol, incrementResultSymbol);
            }

            visitorContext.uasmBuilder.AddJump(loopStartLabel);
            visitorContext.uasmBuilder.AddJumpLabel(loopExitLabel);

            visitorContext.PopTable();
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            SymbolDefinition conditionSymbol = null;

            using (ExpressionCaptureScope conditionCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Condition);
                conditionSymbol = conditionCapture.ExecuteGet();
            }

            JumpLabel conditionExpressionEnd = visitorContext.labelTable.GetNewJumpLabel("conditionExpressionEnd");

            JumpLabel falseConditionStart = visitorContext.labelTable.GetNewJumpLabel("conditionFailStart");

            using (ExpressionCaptureScope outputScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                visitorContext.uasmBuilder.AddPush(conditionSymbol);
                visitorContext.uasmBuilder.AddJumpIfFalse(falseConditionStart);

                SymbolDefinition resultSymbol = null;

                using (ExpressionCaptureScope lhsScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.WhenTrue);
                    resultSymbol = visitorContext.topTable.CreateUnnamedSymbol(lhsScope.GetReturnType(), SymbolDeclTypeFlags.Internal);

                    using (ExpressionCaptureScope resultSetScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        resultSetScope.SetToLocalSymbol(resultSymbol);
                        resultSetScope.ExecuteSet(lhsScope.ExecuteGet());
                    }
                }

                outputScope.SetToLocalSymbol(resultSymbol);

                visitorContext.uasmBuilder.AddJump(conditionExpressionEnd);
                visitorContext.uasmBuilder.AddJumpLabel(falseConditionStart);

                using (ExpressionCaptureScope rhsScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(node.WhenFalse);

                    using (ExpressionCaptureScope resultSetScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        resultSetScope.SetToLocalSymbol(resultSymbol);
                        resultSetScope.ExecuteSet(rhsScope.ExecuteGet());
                    }
                }

                visitorContext.uasmBuilder.AddJumpLabel(conditionExpressionEnd);
            }
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            UpdateSyntaxNode(node);

            throw new System.NotImplementedException("UdonSharp does not yet support switch statements");
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

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            UpdateSyntaxNode(node);

            List<SymbolDefinition> invocationArgs = new List<SymbolDefinition>();

            foreach (ArgumentSyntax argument in node.ArgumentList.Arguments)
            {
                using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    Visit(argument.Expression);

                    invocationArgs.Add(captureScope.ExecuteGet());
                }
            }
            
            // Grab the external scope so that the method call can propagate its output upwards
            ExpressionCaptureScope externalScope = visitorContext.PopCaptureScope();

            if (externalScope != null)
                visitorContext.PushCaptureScope(externalScope);

            using (ExpressionCaptureScope methodCaptureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Expression);
                
                if (!methodCaptureScope.IsMethod())
                    throw new System.Exception("Invocation requires method expression!");

                using (ExpressionCaptureScope functionParamCaptureScope = new ExpressionCaptureScope(visitorContext, externalScope))
                {
                    methodCaptureScope.Invoke(invocationArgs.ToArray());
                }
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

            System.Type newType = null;

            using (ExpressionCaptureScope constructorTypeScope = new ExpressionCaptureScope(visitorContext, null))
            {
                Visit(node.Type);
                newType = constructorTypeScope.captureType;
            }

            if (node.Initializer != null)
                throw new System.NotImplementedException("Initializer lists are not yet supported by UdonSharp");

            using (ExpressionCaptureScope creationCaptureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                // Use the default constructor by just making a constant of the correct type in Udon
                if (node.ArgumentList.Arguments.Count == 0)
                {
                    creationCaptureScope.SetToLocalSymbol(visitorContext.topTable.CreateConstSymbol(newType, null));
                }
                else
                {
                    SymbolDefinition[] argSymbols = new SymbolDefinition[node.ArgumentList.Arguments.Count];

                    for (int i = 0; i < argSymbols.Length; ++i)
                    {
                        using (ExpressionCaptureScope argCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                        {
                            Visit(node.ArgumentList.Arguments[i]);
                            argSymbols[i] = argCaptureScope.ExecuteGet();
                        }
                    }

                    using (ExpressionCaptureScope constructorMethodScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        constructorMethodScope.SetToMethods(newType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

                        creationCaptureScope.SetToLocalSymbol(constructorMethodScope.Invoke(argSymbols));
                    }
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

                        interpolationResultSymbol = toStringScope.Invoke(new SymbolDefinition[] { interpolationResultSymbol });
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
