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

        // Tracking labels for the current function and flow control
        public JumpLabel returnLabel = null;
        public Stack<JumpLabel> continueLabelStack = new Stack<JumpLabel>();
        public Stack<JumpLabel> breakLabelStack = new Stack<JumpLabel>();

        public SymbolTable topTable { get { return symbolTableStack.Peek(); } }

        public ExpressionCaptureScope topCaptureScope { get { return expressionCaptureStack.Count > 0 ? expressionCaptureStack.Peek() : null; } }

        public ASTVisitorContext(ResolverContext resolver, SymbolTable rootTable)
        {
            resolverContext = resolver;

            symbolTableStack = new Stack<SymbolTable>();
            symbolTableStack.Push(rootTable);

            labelTable = new LabelTable();
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
        ASTVisitorContext visitorContext;
        

        public ASTVisitor(ResolverContext resolver, SymbolTable rootTable)
            :base(Microsoft.CodeAnalysis.SyntaxWalkerDepth.Node)
        {
            visitorContext = new ASTVisitorContext(resolver, rootTable);
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
            return visitorContext.uasmBuilder.GetAssemblyStr();
        }

        public override void DefaultVisit(SyntaxNode node)
        {
            Debug.Log(node.Kind().ToString());
            base.DefaultVisit(node);

            //throw new System.Exception($"UdonSharp does not currently support node type {node.Kind().ToString()}");
        }
        
        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            foreach (UsingDirectiveSyntax usingDirective in node.Usings)
            {
                usingDirective.Accept(this);
            }

            foreach (MemberDeclarationSyntax member in node.Members)
            {
                member.Accept(this);
            }
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            visitorContext.uasmBuilder.AppendLine(".code_start", 0);

            foreach (MemberDeclarationSyntax member in node.Members)
            {
                member.Accept(this);
            }

            visitorContext.uasmBuilder.AppendLine(".code_end", 0);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                node.Name.Accept(this);

                Debug.Assert(captureScope.IsNamespace(), "Captured scope is not a namespace!");

                //Debug.Log($"Added namespace: {captureScope.captureNamespace}");
                visitorContext.resolverContext.AddNamespace(captureScope.captureNamespace);
            }
        }

        public override void VisitBlock(BlockSyntax node)
        {
            SymbolTable functionSymbolTable = new SymbolTable(visitorContext.resolverContext, visitorContext.topTable);
            visitorContext.PushTable(functionSymbolTable);

            foreach (StatementSyntax statement in node.Statements)
            {
                statement.Accept(this);
            }

            visitorContext.PopTable();
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            throw new System.NotImplementedException("User property declarations are not yet supported by UdonSharp");
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            // todo: make attributes for syncing and handle them here
            bool isPublic = node.Modifiers.HasModifier("public");
            bool isVar = node.Declaration.Type.IsVar;

            System.Type variableType = null;

            if (!isVar)
            {
                using (ExpressionCaptureScope typeCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    node.Declaration.Type.Accept(this);

                    if (!typeCapture.IsType())
                        throw new System.Exception("Type could not be parsed from variable declaration!");

                    variableType = typeCapture.captureType;
                }
            }
            else
            {
                // todo
                throw new System.NotImplementedException("UdonSharp does not yet support var type declaration");
            }
            
            foreach (VariableDeclaratorSyntax variableDeclarator in node.Declaration.Variables)
            {
                if (variableDeclarator.Initializer != null)
                {
                    throw new System.NotImplementedException("UdonSharp does not yet support default field values");
                }

                string variableName = variableDeclarator.Identifier.ValueText;

                visitorContext.topTable.CreateNamedSymbol(variableName, variableType, isPublic ? SymbolDeclTypeFlags.Public : SymbolDeclTypeFlags.Private);
            }
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            bool isVar = node.Declaration.Type.IsVar;

            System.Type variableType = null;

            if (!isVar)
            {
                using (ExpressionCaptureScope typeCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    node.Declaration.Type.Accept(this);

                    if (!typeCapture.IsType())
                        throw new System.Exception("Type could not be parsed from variable declaration!");

                    variableType = typeCapture.captureType;
                }
            }
            else
            {
                // todo
                throw new System.NotImplementedException("UdonSharp does not yet support var type declaration");
            }

            foreach (VariableDeclaratorSyntax variableDeclarator in node.Declaration.Variables)
            {
                string variableName = variableDeclarator.Identifier.ValueText;

                using (ExpressionCaptureScope symbolCreationScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    SymbolDefinition newSymbol = visitorContext.topTable.CreateNamedSymbol(variableName, variableType, SymbolDeclTypeFlags.Local);
                    symbolCreationScope.SetToLocalSymbol(newSymbol);

                    // Run the initializer if it exists
                    // Todo: Run the set on the new symbol scope from within the initializer scope for direct setting
                    if (variableDeclarator.Initializer != null)
                    {
                        using (ExpressionCaptureScope initializerCapture = new ExpressionCaptureScope(visitorContext, null))
                        {
                            variableDeclarator.Initializer.Accept(this);
                            
                            symbolCreationScope.ExecuteSet(initializerCapture.ExecuteGet());
                        }
                    }
                }
            }
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

        // Not really strictly needed since the compiler for the normal C# will yell at people for us if they attempt to access something not valid for `this`
        public override void VisitThisExpression(ThisExpressionSyntax node)
        {
            if (visitorContext.topCaptureScope != null)
                visitorContext.topCaptureScope.ResolveAccessToken("this");
        }

        public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                node.Value.Accept(this);
            }
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            SymbolDefinition rhsValue = null;

            using (ExpressionCaptureScope rhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                node.Right.Accept(this);

                rhsValue = rhsCapture.ExecuteGet();
            }
            
            // Set parent to allow capture propagation for stuff like x = y = z;
            using (ExpressionCaptureScope lhsCapture = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                node.Left.Accept(this);

                lhsCapture.ExecuteSet(rhsValue);
            }
        }

        // Where we handle creating constants and such
        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            SymbolDefinition expressionConstant = null;

            switch (node.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                    // The Roslyn AST figures out the type automagically for you based on how the token is declared :D 
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
                    // todo: handle null literal
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

            SymbolTable functionSymbolTable = new SymbolTable(visitorContext.resolverContext, visitorContext.topTable);
            visitorContext.PushTable(functionSymbolTable);

            node.Body.Accept(this);

            visitorContext.PopTable();

            visitorContext.uasmBuilder.AddJumpLabel(returnLabel);
            visitorContext.uasmBuilder.AppendLine("JUMP, 0xFFFFFFFF", 2);
            visitorContext.uasmBuilder.AppendLine("");
        }

        public override void VisitQualifiedName(QualifiedNameSyntax node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            node.Expression.Accept(this);
            node.Name.Accept(this);
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
                    return BuiltinOperatorType.Addition;
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.SubtractAssignmentExpression:
                    return BuiltinOperatorType.Subtraction;
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                    return BuiltinOperatorType.Multiplication;
                case SyntaxKind.DivideExpression:
                case SyntaxKind.DivideAssignmentExpression:
                    return BuiltinOperatorType.Division;
                case SyntaxKind.ModuloExpression:
                case SyntaxKind.ModuloAssignmentExpression:
                    return BuiltinOperatorType.Remainder;
                case SyntaxKind.UnaryMinusExpression:
                    return BuiltinOperatorType.UnaryMinus;
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.LeftShiftAssignmentExpression:
                    return BuiltinOperatorType.LeftShift;
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                    return BuiltinOperatorType.RightShift;
                case SyntaxKind.BitwiseAndExpression:
                case SyntaxKind.AndAssignmentExpression:
                    return BuiltinOperatorType.LogicalAnd;
                case SyntaxKind.BitwiseOrExpression:
                case SyntaxKind.OrAssignmentExpression:
                    return BuiltinOperatorType.LogicalOr;
                case SyntaxKind.BitwiseNotExpression:
                    return BuiltinOperatorType.LogicalNot;
                case SyntaxKind.ExclusiveOrExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                    return BuiltinOperatorType.LogicalXor;
                case SyntaxKind.LogicalOrExpression:
                    return BuiltinOperatorType.ConditionalOr;
                case SyntaxKind.LogicalAndExpression:
                    return BuiltinOperatorType.ConditionalAnd;
                case SyntaxKind.LogicalNotExpression:
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

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            SymbolDefinition rhsValue = null;
            SymbolDefinition lhsValue = null;

            using (ExpressionCaptureScope rhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                node.Right.Accept(this);

                rhsValue = rhsCapture.ExecuteGet();
            }

            ExpressionCaptureScope outerScope = visitorContext.topCaptureScope;

            using (ExpressionCaptureScope lhsCapture = new ExpressionCaptureScope(visitorContext, null))
            {
                node.Left.Accept(this);

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
                    case SyntaxKind.LogicalOrExpression:
                    case SyntaxKind.LogicalAndExpression:
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

                    SymbolDefinition resultSymbol = operatorMethodCapture.InvokeExtern(new SymbolDefinition[] { lhsValue, rhsValue });

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
            throw new System.NotImplementedException("todo: implement casts");
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            List<SymbolDefinition> invocationArgs = new List<SymbolDefinition>();

            foreach (ArgumentSyntax argument in node.ArgumentList.Arguments)
            {
                using (ExpressionCaptureScope captureScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    argument.Expression.Accept(this);

                    invocationArgs.Add(captureScope.ExecuteGet());
                }
            }
            
            // Grab the external scope so that the method call can propagate its output upwards
            ExpressionCaptureScope externalScope = visitorContext.PopCaptureScope();

            if (externalScope != null)
                visitorContext.PushCaptureScope(externalScope);

            using (ExpressionCaptureScope methodCaptureScope = new ExpressionCaptureScope(visitorContext, null))
            {
                node.Expression.Accept(this);

                Debug.Assert(methodCaptureScope.IsMethod(), "Invocation requires method expression!");

                using (ExpressionCaptureScope functionParamCaptureScope = new ExpressionCaptureScope(visitorContext, externalScope))
                {
                    methodCaptureScope.InvokeExtern(invocationArgs.ToArray());
                }
            }
        }

        public override void VisitNullableType(NullableTypeSyntax node)
        {
            throw new System.NotImplementedException("Nullable types are not currently supported by UdonSharp");
        }
    }
}
