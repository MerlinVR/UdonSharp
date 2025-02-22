
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler.Assembly;
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

        private static bool TryCreateUdonSharpMetadataInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression,
            out BoundInvocationExpression createdInvocation)
        {
            if (symbol.Name == "GetUdonTypeID" || symbol.Name == "GetUdonTypeName")
            {
                if (symbol.IsStatic &&
                    symbol.TypeArguments.Length == 1 &&
                    symbol.ContainingType == context.GetTypeSymbol(typeof(UdonSharpBehaviour)))
                {
                    IConstantValue constantValue;
                    TypeSymbol constantType;

                    var typeArgs = symbol.TypeArguments.Select(e => context.GetTypeSymbol(e.RoslynSymbol)).ToArray();
                    
                    if (symbol.Name == "GetUdonTypeID")
                    {
                        constantValue = new ConstantValue<long>(UdonSharpInternalUtility.GetTypeID(TypeSymbol.GetFullTypeName(typeArgs[0].RoslynSymbol)));
                        constantType = context.GetTypeSymbol(SpecialType.System_Int64);
                    }
                    else
                    {
                        constantValue = new ConstantValue<string>(TypeSymbol.GetFullTypeName(typeArgs[0].RoslynSymbol));
                        constantType = context.GetTypeSymbol(SpecialType.System_String);
                    }

                    createdInvocation = new BoundConstantInvocationExpression(node, constantValue, constantType);

                    return true;
                }

                if (!symbol.IsStatic &&
                    instanceExpression != null &&
                    symbol.ContainingType == context.GetTypeSymbol(typeof(UdonSharpBehaviour)))
                {
                    TypeSymbol methodContainer = context.GetTypeSymbol(typeof(UdonSharpBehaviourMethods));
                    var shimMethod = methodContainer.GetMember<MethodSymbol>(symbol.Name, context);
                    context.MarkSymbolReferenced(shimMethod);
                    
                    createdInvocation = CreateBoundInvocation(context, node, shimMethod, null, new [] {instanceExpression});

                    return true;
                }
            }

            createdInvocation = null;
            return false;
        }
        
        private static readonly HashSet<Type> _brokenGetComponentTypes = new HashSet<Type>()
        {
            typeof(VRC.SDKBase.VRC_AvatarPedestal), typeof(VRC.SDK3.Components.VRCAvatarPedestal),
            typeof(VRC.SDKBase.VRC_Pickup), typeof(VRC.SDK3.Components.VRCPickup),
            typeof(VRC.SDKBase.VRC_PortalMarker), typeof(VRC.SDK3.Components.VRCPortalMarker),
            //typeof(VRC.SDKBase.VRC_MirrorReflection), typeof(VRC.SDK3.Components.VRCMirrorReflection),
            typeof(VRC.SDKBase.VRCStation),typeof(VRC.SDK3.Components.VRCStation),
            typeof(VRC.SDK3.Video.Components.VRCUnityVideoPlayer),
            typeof(VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer),
            typeof(VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer),
            typeof(VRC.SDK3.Components.VRCObjectPool),
            typeof(VRC.SDK3.Components.VRCObjectSync),
            typeof(UdonBehaviour),
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
                TypeSymbol gameObjectType = context.GetTypeSymbol(typeof(GameObject));
                TypeSymbol typeArgument = symbol.TypeArguments[0];
             
                // udon-workaround: Work around the udon bug where it checks the strongbox type instead of variable type and blows up when the strong box is `object`
                if (instanceExpression.ValueType == gameObjectType)
                {
                    PropertySymbol accessProperty = gameObjectType.GetMember<PropertySymbol>("transform", context);
                    instanceExpression = BoundAccessExpression.BindAccess(context, node, accessProperty, instanceExpression);
                }
                else
                {
                    PropertySymbol accessProperty = context.GetTypeSymbol(typeof(Component)).GetMember<PropertySymbol>("transform", context);
                    instanceExpression = BoundAccessExpression.BindAccess(context, node, accessProperty, instanceExpression);
                }

                TypeSymbol udonSharpBehaviourType = context.GetTypeSymbol(typeof(UdonSharpBehaviour));

                // Exact UdonSharpBehaviour type match
                if (typeArgument == udonSharpBehaviourType)
                {
                    MethodSymbol getComponentMethodShim = context.GetTypeSymbol(typeof(GetComponentShim))
                        .GetMembers<MethodSymbol>(symbol.Name + "USB", context)
                        .First(e => e.Parameters.Length == parameterExpressions.Length + 1);
                    
                    createdInvocation = new BoundStaticUserMethodInvocation(node, getComponentMethodShim,
                        new [] {instanceExpression}.Concat(parameterExpressions).ToArray());
                    
                    context.MarkSymbolReferenced(getComponentMethodShim);

                    return true;
                }
                
                // Subclass of UdonSharpBehaviour
                if (typeArgument.IsUdonSharpBehaviour)
                {
                    // Handle inherited types
                    if (context.CompileContext.HasInheritedUdonSharpBehaviours(typeArgument))
                    {
                        MethodSymbol getComponentInheritedMethodShim = context.GetTypeSymbol(typeof(GetComponentShim))
                            .GetMembers<MethodSymbol>(symbol.Name + "I", context)
                            .First(e => e.Parameters.Length == parameterExpressions.Length + 1);
                        
                        getComponentInheritedMethodShim = getComponentInheritedMethodShim.ConstructGenericMethod(context, new [] { typeArgument });
                    
                        createdInvocation = new BoundStaticUserMethodInvocation(node, getComponentInheritedMethodShim,
                            new [] {instanceExpression}.Concat(parameterExpressions).ToArray());
                    
                        context.MarkSymbolReferenced(getComponentInheritedMethodShim);
                        
                        return true;
                    }
                    
                    MethodSymbol getComponentMethodShim = context.GetTypeSymbol(typeof(GetComponentShim))
                        .GetMembers<MethodSymbol>(symbol.Name, context)
                        .First(e => e.Parameters.Length == parameterExpressions.Length + 1);
                    
                    getComponentMethodShim = getComponentMethodShim.ConstructGenericMethod(context, new [] { typeArgument });
                    
                    createdInvocation = new BoundStaticUserMethodInvocation(node, getComponentMethodShim,
                        new [] {instanceExpression}.Concat(parameterExpressions).ToArray());
                    
                    context.MarkSymbolReferenced(getComponentMethodShim);

                    return true;
                }

                if (_brokenGetComponentTypes.Contains(typeArgument.UdonType.SystemType))
                {
                    MethodSymbol getComponentInheritedMethodShim = context.GetTypeSymbol(typeof(GetComponentShim))
                        .GetMembers<MethodSymbol>(symbol.Name + "VRC", context)
                        .First(e => e.Parameters.Length == parameterExpressions.Length + 1);
                        
                    getComponentInheritedMethodShim = getComponentInheritedMethodShim.ConstructGenericMethod(context, new [] { typeArgument });
                    
                    createdInvocation = new BoundStaticUserMethodInvocation(node, getComponentInheritedMethodShim,
                        new [] {instanceExpression}.Concat(parameterExpressions).ToArray());
                    
                    context.MarkSymbolReferenced(getComponentInheritedMethodShim);
                        
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
                    createdInvocation = new BoundExternInvocation(node, context,
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

                createdInvocation = new BoundExternInvocation(node, context, setProgramVarObjMethod, instanceExpression,
                    parameterExpressions);
                return true;
            }

            createdInvocation = null;
            return false;
        }
        
        private static bool TryCreateArrayMethodInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if ((symbol.Name == "IndexOf" || symbol.Name == "BinarySearch" || symbol.Name == "LastIndexOf" || symbol.Name == "Reverse") &&
                symbol.ContainingType == context.GetTypeSymbol(typeof(Array)))
            {
                MethodSymbol arrayMethod = context.GetTypeSymbol(typeof(Array))
                    .GetMembers<MethodSymbol>(symbol.Name, context)
                    .First(e => !e.RoslynSymbol.IsGenericMethod && e.Parameters.Length == symbol.Parameters.Length);

                createdInvocation = new BoundExternInvocation(node, context, arrayMethod, instanceExpression,
                    parameterExpressions);
                return true;
            }

            createdInvocation = null;
            return false;
        }
        
        private static bool TryCreateTMPMethodInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if (symbol.ContainingType != null && 
                symbol.ContainingType.ToString() == "TMPro.TMP_Text")
            {
                createdInvocation = new BoundExternInvocation(node, context,
                    new ExternSynthesizedMethodSymbol(context, symbol.Name, instanceExpression.ValueType, symbol.Parameters.Select(e => e.Type).ToArray(), symbol.ReturnType, symbol.IsStatic), 
                    instanceExpression,
                    parameterExpressions);
                
                return true;
            }

            createdInvocation = null;
            return false;
        }
        
        private static bool TryCreateBaseEnumMethodInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if ((symbol.Name == "ToString" || symbol.Name == "GetHashCode" || symbol.Name == "Equals") &&
                symbol.ContainingType != null &&
                symbol.ContainingType == context.GetTypeSymbol(SpecialType.System_Enum))
            {
                createdInvocation = new BoundExternInvocation(node, context,
                    context.GetTypeSymbol(SpecialType.System_Object).GetMember<MethodSymbol>(symbol.Name, context), 
                    instanceExpression,
                    parameterExpressions);
                
                return true;
            }

            createdInvocation = null;
            return false;
        }
        
        private static bool TryCreateCompareToInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if (symbol.Name == "CompareTo" &&
                symbol.ContainingType != null &&
                symbol.ContainingType == context.GetTypeSymbol(typeof(IComparable)))
            {
                createdInvocation = new BoundExternInvocation(node, context,
                    new ExternSynthesizedMethodSymbol(context, "CompareTo", instanceExpression.ValueType,
                        new [] { instanceExpression.ValueType },
                        context.GetTypeSymbol(SpecialType.System_Int32), false),
                    instanceExpression, parameterExpressions);
                
                return true;
            }

            createdInvocation = null;
            return false;
        }

        private static bool TryCreateShimInvocation(AbstractPhaseContext context, SyntaxNode node,
            MethodSymbol symbol, BoundExpression instanceExpression, BoundExpression[] parameterExpressions,
            out BoundInvocationExpression createdInvocation)
        {
            if (TryCreateUdonSharpMetadataInvocation(context, node, symbol, instanceExpression, out createdInvocation))
                return true;
            
            if (TryCreateGetComponentInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;

            if (TryCreateInstantiationInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;
            
            if (TryCreateSetProgramVariableInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;
            
            if (TryCreateArrayMethodInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;
            
            if (TryCreateTMPMethodInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;
            
            if (TryCreateBaseEnumMethodInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
                return true;
            
            if (TryCreateCompareToInvocation(context, node, symbol, instanceExpression, parameterExpressions, out createdInvocation))
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
                if (CompilerUdonInterface.IsUdonEvent(symbol) &&
                    symbol.ContainingType == context.GetTypeSymbol(typeof(UdonSharpBehaviour))) // Pass through for making base calls on the U# behaviour type return noop
                    return new BoundUdonSharpBehaviourInvocationExpression(node, symbol, instanceExpression, parameterExpressions);

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

                        return new BoundExternInvocation(node, context, boolNotOperator, null, new BoundExpression[] {boundEqualsInvocation});
                    }
                    
                    if (node is AssignmentExpressionSyntax)
                        return new BoundCompoundAssignmentExpression(context, node, (BoundAccessExpression) parameterExpressions[0], symbol, parameterExpressions[1]);

                    if (symbol is ExternBuiltinOperatorSymbol externBuiltinOperatorSymbol &&
                        externBuiltinOperatorSymbol.OperatorType == BuiltinOperatorType.BitwiseNot)
                        return new BoundBitwiseNotExpression(node, parameterExpressions[0]);
                    
                    if (parameterExpressions.Length == 2 || symbol.Name == "op_UnaryNegation" || symbol.Name == "op_LogicalNot")
                    {
                        return new BoundBuiltinOperatorInvocationExpression(node, context, symbol, parameterExpressions);
                    }

                    throw new NotSupportedException("Operator expressions must have either 1 or 2 parameters", node.GetLocation());
                }
                
                return new BoundExternInvocation(node, context, symbol, instanceExpression, parameterExpressions);
            }

            if (symbol.IsStatic)
                return new BoundStaticUserMethodInvocation(node, symbol, parameterExpressions);

            if (symbol is UdonSharpBehaviourMethodSymbol udonSharpBehaviourMethodSymbol)
            {
                if (instanceExpression != null && !instanceExpression.IsThis)
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
        
        protected void CheckStackSize(Value valueCount, EmitContext context)
        {
            using (context.InterruptAssignmentScope())
            {
                Value stack = context.RecursiveStackValue;
                BoundAccessExpression stackAccess = BoundAccessExpression.BindAccess(stack);
                Value stackAddr = context.RecursiveStackAddressValue;
                BoundAccessExpression stackAddrAccess = BoundAccessExpression.BindAccess(stackAddr);

                TypeSymbol arrayType = context.GetTypeSymbol(SpecialType.System_Array);

                context.Module.AddCommentTag("Stack size check");

                // Check stack size and double it if it's not enough
                // We know that doubling once will always be enough since the default size of the stack is the max number of stack values pushed in any method
                PropertySymbol arraySizeProperty = arrayType.GetMember<PropertySymbol>("Length", context);

                TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);

                Value arraySize =
                    context.EmitValue(BoundAccessExpression.BindAccess(context, SyntaxNode, arraySizeProperty,
                        stackAccess));
                BoundAccessExpression arraySizeAccess = BoundAccessExpression.BindAccess(arraySize);

                Value targetSize = context.EmitValue(CreateBoundInvocation(context, SyntaxNode,
                    new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Addition, intType, context), null,
                    new BoundExpression[] { stackAddrAccess, BoundAccessExpression.BindAccess(valueCount) }));

                Value isSizeGreaterThan = context.EmitValue(CreateBoundInvocation(context, SyntaxNode,
                    new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.GreaterThanOrEqual, intType, context), null,
                    new BoundExpression[]
                    {
                        BoundAccessExpression.BindAccess(targetSize),
                        arraySizeAccess,
                    }));

                JumpLabel skipResizeLabel = context.Module.CreateLabel();

                context.Module.AddJumpIfFalse(skipResizeLabel, isSizeGreaterThan);

                // Resize logic
                Value constantTwo = context.GetConstantValue(intType, 2);
                Value newSize = context.EmitValue(CreateBoundInvocation(context, SyntaxNode,
                    new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Multiplication, intType, context), null,
                    new BoundExpression[]
                    {
                        arraySizeAccess,
                        BoundAccessExpression.BindAccess(constantTwo),
                    }));

                Value newArray = context.EmitValue(new BoundArrayCreationExpression(SyntaxNode, context,
                    context.GetTypeSymbol(SpecialType.System_Object).MakeArrayType(context),
                    new BoundExpression[] { BoundAccessExpression.BindAccess(newSize) }, null));

                MethodSymbol arrayCopyMethod = arrayType.GetMembers<MethodSymbol>("Copy", context)
                    .First(e => e.Parameters.Length == 3 && e.Parameters[2].Type == intType);

                context.Emit(CreateBoundInvocation(context, null, arrayCopyMethod, null,
                    new BoundExpression[]
                    {
                        stackAccess,
                        BoundAccessExpression.BindAccess(newArray),
                        BoundAccessExpression.BindAccess(arraySize)
                    }));

                context.Module.AddCopy(newArray, stack);

                context.Module.LabelJump(skipResizeLabel);
                
                context.Module.AddCommentTag("Stack size check end");
            }
        }
        
        protected void PushRecursiveValues(Value[] values, EmitContext context)
        {
            if (values.Length == 0)
                return;
            
            Value stack = context.RecursiveStackValue;
            BoundAccessExpression stackAccess = BoundAccessExpression.BindAccess(stack);
            Value stackAddr = context.RecursiveStackAddressValue;
            BoundAccessExpression stackAddrAccess = BoundAccessExpression.BindAccess(stackAddr);
            
            context.Module.AddCommentTag("Recursive stack push");
            
            // Now we start copying values over to the stack
            BoundInvocationExpression incrementExpression = new BoundPrefixOperatorExpression(context, SyntaxNode,
                stackAddrAccess, new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Addition, context.GetTypeSymbol(SpecialType.System_Int32), context));

            foreach (var valueToPush in values)
            {
                BoundArrayAccessExpression arraySet = new BoundArrayAccessExpression(null, context, stackAccess,
                    new BoundExpression[] { stackAddrAccess });

                context.EmitSet(arraySet, BoundAccessExpression.BindAccess(valueToPush));
                
                context.Emit(incrementExpression);
            }
            
            context.Module.AddCommentTag("Recursive stack push end");
        }
        
        protected void PopRecursiveValues(Value[] values, EmitContext context)
        {
            if (values.Length == 0)
                return;
            
            Value stack = context.RecursiveStackValue;
            Value stackAddr = context.RecursiveStackAddressValue;
            BoundAccessExpression stackAddrAccess = BoundAccessExpression.BindAccess(stackAddr);
            TypeSymbol intType = context.GetTypeSymbol(SpecialType.System_Int32);
            TypeSymbol objectType = context.GetTypeSymbol(SpecialType.System_Object);
            TypeSymbol objectArrayType = objectType.MakeArrayType(context);
            
            context.Module.AddCommentTag("Recursive stack pop");
            
            BoundInvocationExpression decrementExpression = new BoundPrefixOperatorExpression(context, SyntaxNode,
                stackAddrAccess, new ExternSynthesizedOperatorSymbol(BuiltinOperatorType.Subtraction, intType, context));

            foreach (var valueToPop in values.Reverse())
            {
                context.Emit(decrementExpression);
                
                ExternSynthesizedMethodSymbol arrayGetMethod = new ExternSynthesizedMethodSymbol(context, "Get",
                    objectArrayType, new [] { intType }, objectType, false);

                context.Module.AddPush(stack);
                context.Module.AddPush(stackAddr);
                context.Module.AddPush(valueToPop);
                context.Module.AddExtern(arrayGetMethod);
            }
            
            context.Module.AddCommentTag("Recursive stack pop end");
        }

        private sealed class BoundBuiltinOperatorInvocationExpression : BoundExternInvocation
        {
            public BoundBuiltinOperatorInvocationExpression(SyntaxNode node, AbstractPhaseContext context, MethodSymbol method, BoundExpression[] operandExpressions)
                :base(node, context, method, null, operandExpressions)
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
                
                Value setResult;
                
                if (TargetExpression.ValueType != OperatorMethod.ReturnType)
                    setResult = context.EmitSet(TargetExpression, new BoundCastExpression(null, invocation, ValueType, true));
                else
                    setResult = context.EmitSet(TargetExpression, invocation);

                return setResult;
            }
        }
        
        private sealed class BoundGetUnityEngineComponentInvocation : BoundExternInvocation
        {
            public override TypeSymbol ValueType { get; }

            public BoundGetUnityEngineComponentInvocation(AbstractPhaseContext context, SyntaxNode node, MethodSymbol methodSymbol, BoundExpression sourceExpression, BoundExpression[] parametersExpressions) 
                : base(node, context, BuildMethod(context, methodSymbol), sourceExpression, GetParameterExpressions(context, methodSymbol, parametersExpressions))
            {
                ValueType = methodSymbol.TypeArguments[0];

                if (methodSymbol.ReturnType.IsArray)
                    ValueType = ValueType.MakeArrayType(context);
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
                
                Type targetType = TargetExpression.ValueType.UdonType.SystemType;
                IConstantValue incrementValue = (IConstantValue) Activator.CreateInstance(
                    typeof(ConstantValue<>).MakeGenericType(targetType), Convert.ChangeType(1, targetType));
                BoundExpression expression = CreateBoundInvocation(context, null, InternalExpression.Method, null,
                    new BoundExpression[] {BoundAccessExpression.BindAccess(returnValue), new BoundConstantExpression(incrementValue, TargetExpression.ValueType, SyntaxNode)});

                if (InternalExpression.Method.ReturnType != TargetExpression.ValueType)
                    expression = new BoundCastExpression(null, expression, ValueType, true);
                
                context.EmitSet(TargetExpression, expression);
                
                return returnValue;
            }

            /// <summary>
            /// If we aren't requesting a value, we can just direct assign.
            /// This helps keep increments on stuff like loops with i++ fast
            /// </summary>
            /// <param name="context"></param>
            public override void Emit(EmitContext context)
            {
                if (InternalExpression.Method.ReturnType != TargetExpression.ValueType)
                    context.EmitSet(TargetExpression, new BoundCastExpression(null, InternalExpression, ValueType, true));
                else
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
                if (InternalExpression.Method.ReturnType != TargetExpression.ValueType)
                    return context.EmitSet(TargetExpression, new BoundCastExpression(null, InternalExpression, ValueType, true));
                
                return context.EmitSet(TargetExpression, InternalExpression);
            }
        }

        public sealed class BoundConstantInvocationExpression : BoundInvocationExpression
        {
            private IConstantValue Constant { get; }

            public override IConstantValue ConstantValue => Constant;

            public override TypeSymbol ValueType { get; }

            public BoundConstantInvocationExpression(SyntaxNode node, IConstantValue constantValue, TypeSymbol constantValType) 
                :base(node, null, null, Array.Empty<BoundExpression>())
            {
                Constant = constantValue;
                ValueType = constantValType;
            }

            public override Value EmitValue(EmitContext context)
            {
                Value returnVal = context.GetReturnValue(ValueType);
                
                context.EmitValueAssignment(returnVal,
                    BoundAccessExpression.BindAccess(context.GetConstantValue(ValueType, ConstantValue.Value)));

                return returnVal;
            }
        }
    }
}
