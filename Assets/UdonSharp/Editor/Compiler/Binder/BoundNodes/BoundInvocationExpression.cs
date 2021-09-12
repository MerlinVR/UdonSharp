
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Emit;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Compiler.Udon;
using UdonSharp.Core;
using UdonSharp.Internal;
using UdonSharp.Lib.Internal;
using UdonSharp.Localization;
using UnityEngine;
using VRC.Udon;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

namespace UdonSharp.Compiler.Binder
{
    internal abstract class BoundInvocationExpression : BoundExpression
    {
        [PublicAPI]
        public MethodSymbol Method { get; }
        
        [PublicAPI]
        public BoundExpression[] ParameterExpressions { get; }
        
        public override TypeSymbol ValueType => Method.ReturnType;
        protected bool IsPropertySetter { get; private set; }
        
        public void MarkPropertySetter()
        {
            IsPropertySetter = true;
        }

        protected BoundInvocationExpression(SyntaxNode node, MethodSymbol method, BoundExpression instanceExpression, BoundExpression[] parameterExpressions)
            :base(node, instanceExpression)
        {
            Method = method;
            ParameterExpressions = parameterExpressions;
        }
        
        /// <summary>
        /// Marks a bound invocation as a base invocation which prevents searching for more derived methods for the call and prevents a virtual call altogether
        /// </summary>
        public virtual void MarkForcedBaseCall() {}

        private static readonly HashSet<string> _getComponentNames = new HashSet<string>()
        {
            "GetComponent",
            "GetComponents",
            "GetComponentInChildren",
            "GetComponentsInChildren",
            "GetComponentInParent",
            "GetComponentsInParent",
        };
        
        private static bool TryCreateGetComponentInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if (symbol.RoslynSymbol != null &&
                symbol.RoslynSymbol.IsGenericMethod && 
                symbol.TypeArguments.Length == 1 &&
                _getComponentNames.Contains(symbol.Name) &&
                (symbol.ContainingType.UdonType.SystemType == typeof(Component) || symbol.ContainingType.UdonType.SystemType == typeof(GameObject)))
            {
                var gameObjectType = context.GetTypeSymbol(typeof(GameObject));
             
                // udon-workaround: Work around the udon bug where it checks the strongbox type instead of variable type and blows up when the strong box is `object`
                if (instanceExpression.ValueType == gameObjectType)
                {
                    var accessProperty = gameObjectType.GetMember<PropertySymbol>("transform", context);
                    instanceExpression = BoundAccessExpression.BindAccess(context, node, accessProperty, instanceExpression);
                }
                else
                {
                    var accessProperty = context.GetTypeSymbol(typeof(Component)).GetMember<PropertySymbol>("transform", context);
                    instanceExpression = BoundAccessExpression.BindAccess(context, node, accessProperty, instanceExpression);
                }
                
                if (symbol.TypeArguments[0].IsUdonSharpBehaviour)
                {
                    MethodSymbol getComponentMethodShim = context.GetTypeSymbol(typeof(GetUserComponentShim))
                        .GetMembers<MethodSymbol>(symbol.Name, context)
                        .First(e => e.Parameters.Length == parameterExpressions.Length + 2);

                    string typeName = symbol.TypeArguments[0].RoslynSymbol.ToDisplayString(new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));
                    var boundConstant = new BoundConstantExpression(UdonSharpInternalUtility.GetTypeID(typeName), context.GetTypeSymbol(SpecialType.System_Int64));
                    createdInvocation = new BoundStaticUserMethodInvocation(node, getComponentMethodShim,
                        new [] {instanceExpression, boundConstant}.Concat(parameterExpressions).ToArray());
                    
                    context.MarkSymbolReferenced(getComponentMethodShim);

                    return true;
                }
                
                createdInvocation = new BoundGetUnityEngineComponentInvocation(context, node, symbol,
                    instanceExpression,
                    parameterExpressions);

                return true;
            }

            createdInvocation = null;
            return false;
        }

        private static bool TryCreateInstantiationInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            switch (symbol.Name)
            {
                case "Instantiate_Extern" when symbol.ContainingType == context.GetTypeSymbol(typeof(InstantiationShim)):
                    createdInvocation = new BoundExternInvocation(node,
                        new ExternSynthesizedMethodSymbol(context,
                            "VRCInstantiate.__Instantiate__UnityEngineGameObject__UnityEngineGameObject",
                            parameterExpressions.Select(e => e.ValueType).ToArray(),
                            context.GetTypeSymbol(typeof(GameObject)), true), 
                        instanceExpression, parameterExpressions);

                    return true;
                case "VRCInstantiate" when symbol.ContainingType == context.GetTypeSymbol(typeof(UdonSharpBehaviour)): // Backwards compatibility for UdonSharpBehaviour.VRCInstantiate
                case "Instantiate" when symbol.ContainingType == context.GetTypeSymbol(typeof(UnityEngine.Object)):
                {
                    if (symbol.Name != "VRCInstantiate" && 
                        (symbol.TypeArguments.Length != 1 ||
                         symbol.TypeArguments[0] != context.GetTypeSymbol(typeof(GameObject))))
                        throw new NotSupportedException("Udon does not support instantiating non-GameObject types");

                    TypeSymbol instantiateShim = context.GetTypeSymbol(typeof(InstantiationShim));
                    MethodSymbol instantiateMethod = instantiateShim.GetMembers<MethodSymbol>("Instantiate", context)
                                                                    .First(e => e.Parameters
                                                                        .Select(p => p.Type)
                                                                        .SequenceEqual(parameterExpressions
                                                                            .Select(p => p.ValueType)));
                    
                    context.MarkSymbolReferenced(instantiateMethod);
                    
                    createdInvocation = new BoundStaticUserMethodInvocation(node, instantiateMethod, parameterExpressions);
                    return true;
                }
            }

            createdInvocation = null;
            return false;
        }
        
        /// <summary>
        /// Udon exposes a generic SetProgramVariable which the overload finding will attempt to use and fail to find,
        ///  so just use the non-generic version in this case
        /// </summary>
        private static bool TryCreateSetProgramVariableInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if (symbol.Name == "SetProgramVariable" &&
                symbol.ContainingType == context.GetTypeSymbol(typeof(UdonBehaviour)))
            {
                MethodSymbol setProgramVarObjMethod = context.GetTypeSymbol(typeof(UdonBehaviour))
                    .GetMembers<MethodSymbol>("SetProgramVariable", context)
                    .First(e => !e.RoslynSymbol.IsGenericMethod);

                createdInvocation = new BoundExternInvocation(node, setProgramVarObjMethod, instanceExpression,
                    parameterExpressions);
                return true;
            }

            createdInvocation = null;
            return false;
        }

        private static bool TryCreateShimInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if (TryCreateGetComponentInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;

            if (TryCreateInstantiationInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;
            
            if (TryCreateSetProgramVariableInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;

            return false;
        }
        
        public static BoundInvocationExpression CreateBoundInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions)
        {
            if (TryCreateShimInvocation(context, node, symbol, instanceExpression, parameterExpressions, out var boundShimInvocation))
                return boundShimInvocation;
            
            if (symbol.IsExtern)
            {
                if (CompilerUdonInterface.IsUdonEvent(symbol.Name) &&
                    symbol.ContainingType == context.GetTypeSymbol(typeof(UdonSharpBehaviour))) // Pass through for making base calls on the U# behaviour type return noop
                    return new BoundUdonSharpBehaviourInvocationExpression(node, symbol, instanceExpression, parameterExpressions);
                
                var doExposureCheck = (!symbol.IsOperator || (symbol.ContainingType == null || !symbol.ContainingType.IsEnum));
                if (doExposureCheck && !CompilerUdonInterface.IsExposedToUdon(((ExternMethodSymbol) symbol).ExternSignature))
                    throw new NotExposedException(LocStr.CE_UdonMethodNotExposed, node, $"{symbol.RoslynSymbol?.ToDisplayString() ?? symbol.ToString()}, sig: {((ExternMethodSymbol) symbol).ExternSignature}");

                if (symbol.IsOperator)
                {
                    // Enum equality/inequality
                    if (symbol.ContainingType?.IsEnum ?? false)
                    {
                        MethodSymbol objectEqualsMethod = context.GetTypeSymbol(SpecialType.System_Object)
                            .GetMember<MethodSymbol>("Equals", context);
                        
                        var boundEqualsInvocation = CreateBoundInvocation(context, node, objectEqualsMethod, parameterExpressions[0],
                                new[] {parameterExpressions[1]});
                        if (symbol.Name == "op_Equality")
                            return boundEqualsInvocation;

                        MethodSymbol boolNotOperator = new ExternSynthesizedOperatorSymbol(
                            BuiltinOperatorType.UnaryNegation, context.GetTypeSymbol(SpecialType.System_Boolean),
                            context);

                        return new BoundExternInvocation(node, boolNotOperator, null, new BoundExpression[] {boundEqualsInvocation});
                    }
                    
                    if (node is AssignmentExpressionSyntax)
                        return new BoundCompoundAssignmentExpression(context, node, (BoundAccessExpression) parameterExpressions[0], symbol, parameterExpressions[1]);

                    if (parameterExpressions.Length == 2 || symbol.Name == "op_UnaryNegation" || symbol.Name == "op_LogicalNot")
                    {
                        return new BoundBuiltinOperatorInvocationExpression(node, symbol, parameterExpressions);
                    }

                    throw new NotSupportedException("Operator expressions must have either 1 or 2 parameters", node.GetLocation());
                }
                
                return new BoundExternInvocation(node, symbol, instanceExpression, parameterExpressions);
            }

            if (symbol.IsStatic)
                return new BoundStaticUserMethodInvocation(node, symbol, parameterExpressions);

            if (symbol is UdonSharpBehaviourMethodSymbol udonSharpBehaviourMethodSymbol)
            {
                if (instanceExpression != null)
                    udonSharpBehaviourMethodSymbol.MarkNeedsReferenceExport();
                
                return new BoundUdonSharpBehaviourInvocationExpression(node, symbol, instanceExpression,
                    parameterExpressions);
            }

            throw new System.NotImplementedException();
        }

        protected override void ReleaseCowValuesImpl(EmitContext context)
        {
            if (ParameterExpressions == null)
                return;
            
            foreach (BoundExpression parameterExpression in ParameterExpressions)
            {
                parameterExpression.ReleaseCowReferences(context);
            }
        }

        protected Value[] GetParameterValues(EmitContext context)
        {
            Value.CowValue[] parameterCows = context.GetExpressionCowValues(this, "parameters");

            if (parameterCows != null)
                return parameterCows.Select(e => e.Value).ToArray();
            
            parameterCows = new Value.CowValue[ParameterExpressions.Length];

            using (context.InterruptAssignmentScope())
            {
                for (int i = 0; i < parameterCows.Length; ++i)
                    parameterCows[i] = context.EmitValueWithDeferredRelease(ParameterExpressions[i])
                        .GetCowValue(context);
            }
            
            context.RegisterCowValues(parameterCows, this, "parameters");
            
            Value[] parameterValues = new Value[ParameterExpressions.Length];

            for (int i = 0; i < parameterValues.Length; ++i)
                parameterValues[i] = parameterCows[i].Value;

            return parameterValues;
        }

        protected Value.CowValue GetInstanceValue(EmitContext context)
        {
            Value.CowValue[] instanceValue = context.GetExpressionCowValues(this, "instance");

            if (instanceValue == null)
            {
                using (context.InterruptAssignmentScope())
                    instanceValue = new[] {context.EmitValue(SourceExpression).GetCowValue(context)};
                
                context.RegisterCowValues(instanceValue, this, "instance");
            }

            return instanceValue[0];
        }

        private sealed class BoundBuiltinOperatorInvocationExpression : BoundExternInvocation
        {
            public BoundBuiltinOperatorInvocationExpression(SyntaxNode node, MethodSymbol method, BoundExpression[] operandExpressions)
                :base(node, method, null, operandExpressions)
            {
            }
        }
        
        private sealed class BoundCompoundAssignmentExpression : BoundInvocationExpression
        {
            private BoundAccessExpression TargetExpression { get; }
            private BoundExpression AssignmentSource { get; }
            private MethodSymbol OperatorMethod { get; }

            public BoundCompoundAssignmentExpression(AbstractPhaseContext context, SyntaxNode node, BoundAccessExpression assignmentTarget, MethodSymbol operatorMethod, BoundExpression assignmentSource)
                : base(node, null, null, null)
            {
                TargetExpression = assignmentTarget;
                AssignmentSource = assignmentSource;
                OperatorMethod = operatorMethod;
            }

            public override TypeSymbol ValueType => TargetExpression.ValueType;

            public override Value EmitValue(EmitContext context)
            {
                Value targetValue =
                    context.EmitValueWithDeferredRelease(TargetExpression);
                
                var invocation = CreateBoundInvocation(context, null, OperatorMethod, null,
                    new[] {BoundAccessExpression.BindAccess(targetValue), AssignmentSource});
                
                Value setResult = context.EmitSet(TargetExpression, invocation);

                return setResult;
            }
        }
        
        private sealed class BoundGetUnityEngineComponentInvocation : BoundExternInvocation
        {
            public override TypeSymbol ValueType { get; }

            public BoundGetUnityEngineComponentInvocation(AbstractPhaseContext context, SyntaxNode node, MethodSymbol methodSymbol, BoundExpression sourceExpression, BoundExpression[] parametersExpressions) 
                : base(node, BuildMethod(context, methodSymbol), sourceExpression, GetParameterExpressions(context, methodSymbol, parametersExpressions))
            {
                ValueType = methodSymbol.TypeArguments[0];
            }

            private static BoundExpression[] GetParameterExpressions(AbstractPhaseContext context, MethodSymbol symbol, BoundExpression[] parameters)
            {
                BoundExpression typeExpression = new BoundConstantExpression(
                    symbol.TypeArguments[0].UdonType.SystemType,
                    context.GetTypeSymbol(typeof(Type)));
                
                if (parameters == null || parameters.Length == 0)
                    return new [] { typeExpression };

                return parameters.Concat(new []{typeExpression}).ToArray();
            }

            private static MethodSymbol BuildMethod(AbstractPhaseContext context, MethodSymbol methodSymbol)
            {
                string methodName = methodSymbol.Name;
                string returnName;

                if (methodSymbol.ReturnType.IsArray)
                    returnName = "__TArray";
                else
                    returnName = "__T";

                string paramStr = "";

                if (methodSymbol.Parameters.Length > 0)
                    paramStr = "__SystemBoolean";

                string methodIdentifier = $"UnityEngineComponent.__{methodName}{paramStr}{returnName}";

                var roslynSymbol = methodSymbol.RoslynSymbol;

                return new ExternSynthesizedMethodSymbol(context, methodIdentifier,
                    roslynSymbol.Parameters.Select(e => context.GetTypeSymbol(e.Type)).ToArray(),
                    context.GetTypeSymbol(roslynSymbol.ReturnType), false);
            }
        }

        public sealed class BoundPostfixOperatorExpression : BoundInvocationExpression
        {
            private BoundAccessExpression TargetExpression { get; }
            private BoundInvocationExpression InternalExpression { get; }
        
            public BoundPostfixOperatorExpression(AbstractPhaseContext context, SyntaxNode node, BoundAccessExpression assignmentTarget, MethodSymbol operatorMethod)
                : base(node, null, null, null)
            {
                TargetExpression = assignmentTarget;
                Type targetType = TargetExpression.ValueType.UdonType.SystemType;
                IConstantValue incrementValue = (IConstantValue) Activator.CreateInstance(
                    typeof(ConstantValue<>).MakeGenericType(targetType), Convert.ChangeType(1, targetType));
                
                InternalExpression = CreateBoundInvocation(context, null, operatorMethod, null,
                    new BoundExpression[] {assignmentTarget, new BoundConstantExpression(incrementValue, TargetExpression.ValueType, node)});
            }

            public override TypeSymbol ValueType => TargetExpression.ValueType;

            public override Value EmitValue(EmitContext context)
            {
                Value returnValue = context.GetReturnValue(TargetExpression.ValueType);

                context.EmitValueAssignment(returnValue, TargetExpression);

                context.EmitSet(TargetExpression, InternalExpression);
                
                return returnValue;
            }

            /// <summary>
            /// If we aren't requesting a value, we can just direct assign.
            /// This helps keep increments on stuff like loops with i++ fast
            /// </summary>
            /// <param name="context"></param>
            public override void Emit(EmitContext context)
            {
                context.EmitSet(TargetExpression, InternalExpression);
            }
        }

        public sealed class BoundPrefixOperatorExpression : BoundInvocationExpression
        {
            private BoundAccessExpression TargetExpression { get; }
            private BoundInvocationExpression InternalExpression { get; }
        
            public BoundPrefixOperatorExpression(AbstractPhaseContext context, SyntaxNode node, BoundAccessExpression assignmentTarget, MethodSymbol operatorMethod)
                : base(node, null, null, null)
            {
                TargetExpression = assignmentTarget;
                Type targetType = TargetExpression.ValueType.UdonType.SystemType;
                IConstantValue incrementValue = (IConstantValue) Activator.CreateInstance(
                    typeof(ConstantValue<>).MakeGenericType(targetType), Convert.ChangeType(1, targetType));
                
                InternalExpression = CreateBoundInvocation(context, null, operatorMethod, null,
                    new BoundExpression[] {assignmentTarget, new BoundConstantExpression(incrementValue, TargetExpression.ValueType, node)});
            }

            public override TypeSymbol ValueType => TargetExpression.ValueType;

            public override Value EmitValue(EmitContext context)
            {
                return context.EmitSet(TargetExpression, InternalExpression);
            }
        }
    }
}
