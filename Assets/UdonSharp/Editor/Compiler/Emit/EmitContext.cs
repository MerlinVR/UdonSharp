
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Binder;
using UdonSharp.Compiler.Symbols;
using UdonSharp.Compiler.Udon;
using UnityEngine;
using NotSupportedException = UdonSharp.Core.NotSupportedException;

#if UDONSHARP_DEBUG
using UnityEngine;
#endif

namespace UdonSharp.Compiler.Emit
{
    /// <summary>
    /// The emit context is the shared data that a single AssemblyModule needs to have access to in order to generate
    ///  uasm. One of theses will be created for each assembly module.
    /// </summary>
    internal class EmitContext : AbstractPhaseContext
    {
        public AssemblyModule Module { get; }
        public TypeSymbol EmitType { get; }

        private Stack<ValueTable> _valueTableStack = new Stack<ValueTable>();
        private Stack<BlockScope> _blockScopeCache = new Stack<BlockScope>();
        private Stack<AssignmentScope> _assignmentScopes = new Stack<AssignmentScope>();
        private Stack<AssignmentScope> _assignmentScopeCache = new Stack<AssignmentScope>();
        private Stack<JumpLabel> _continueLabelStack = new Stack<JumpLabel>();
        private Stack<JumpLabel> _breakLabelStack = new Stack<JumpLabel>();

        internal ValueTable TopTable => _valueTableStack.Peek();
        public ValueTable RootTable { get; }

        private Value _returnValue;

        private MethodSymbol _currentEmitMethod;
        
        public ImmutableArray<FieldSymbol> DeclaredFields { get; private set; }

        private Dictionary<BoundExpression, Dictionary<string, Value.CowValue[]>> _expressionCowValueTracker =
            new Dictionary<BoundExpression, Dictionary<string, Value.CowValue[]>>();

        public EmitContext(AssemblyModule module, ITypeSymbol emitType)
         :base(module.CompileContext)
        {
            Module = module;
            EmitType = GetTypeSymbol(emitType);
            _valueTableStack.Push(module.RootTable);
            RootTable = module.RootTable;
        }

        public void Emit()
        {
            _returnValue = RootTable.CreateInternalValue(GetTypeSymbol(SpecialType.System_UInt32), "returnJump");
            TypeSymbol udonSharpBehaviourType = GetTypeSymbol(typeof(UdonSharpBehaviour));
            
            Stack<TypeSymbol> emitTypeBases = new Stack<TypeSymbol>();
            
            TypeSymbol currentEmitType = EmitType;
            
            while (currentEmitType.BaseType != null)
            {
                emitTypeBases.Push(currentEmitType);
                currentEmitType = currentEmitType.BaseType;
                
                if (currentEmitType == udonSharpBehaviourType)
                    break;
            }

            if (currentEmitType != udonSharpBehaviourType)
            {
                throw new NotSupportedException("U# behaviour must inherit from UdonSharpBehaviour",
                    currentEmitType.RoslynSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation());
            }

            List<MethodSymbol> rootMethods = new List<MethodSymbol>();

            List<FieldSymbol> userFields = new List<FieldSymbol>(); 
            HashSet<FieldSymbol> visitedFields = new HashSet<FieldSymbol>();

            // Visits each base class and searches for the most derived version of a method if it is overriden
            // The intention with this is to ensure a consistent ordering between all inheritors of a base class
            // This means that we can know that all inheritors of a class have the same method names and parameter symbol allocations
            //   which allows people to call virtual methods on UdonSharpBehaviours and have Udon just make it work
            while (emitTypeBases.Count > 0)
            {
                TypeSymbol currentBase = emitTypeBases.Pop();

                // Make sure fields get emitted
                foreach (FieldSymbol field in currentBase.GetMembers<FieldSymbol>(this))
                {
                    if (field.IsConst)
                        continue;
                    
                    if (!visitedFields.Contains(field))
                    {
                        userFields.Add(field);
                        visitedFields.Add(field);
                    }
                    
                    GetUserValue(field);
                }

                foreach (MethodSymbol methodSymbol in currentBase.GetMembers<MethodSymbol>(this))
                {
                    if (methodSymbol.RoslynSymbol.IsImplicitlyDeclared ||
                        methodSymbol.RoslynSymbol.IsStatic)
                        continue;
                    
                    if (methodSymbol.HasOverrides)
                    {
                        MethodSymbol derivedMethod = GetMostDerivedMethod(methodSymbol);
                        
                        if (derivedMethod.RoslynSymbol.IsAbstract)
                            continue;

                        if (!rootMethods.Contains(derivedMethod))
                            rootMethods.Add(derivedMethod);
                    }
                    else if (!rootMethods.Contains(methodSymbol))
                    {
                        if (methodSymbol.RoslynSymbol.IsAbstract)
                            continue;
                        
                        rootMethods.Add(methodSymbol);
                    }
                }
            }

            DeclaredFields = userFields.ToImmutableArray();

            HashSet<MethodSymbol> emittedSet = new HashSet<MethodSymbol>();
            HashSet<MethodSymbol> setToEmit = new HashSet<MethodSymbol>();
            
            // Do not roll this into the while loop, the order must be maintained for the root symbols so calls across behaviours work consistently
            foreach (MethodSymbol methodSymbol in rootMethods)
            {
                _currentEmitMethod = methodSymbol;
                methodSymbol.Emit(this);
                _currentEmitMethod = null;
                
                emittedSet.Add(methodSymbol);
                
                setToEmit.UnionWith(methodSymbol.DirectDependencies.OfType<MethodSymbol>());
            }

            while (setToEmit.Count > 0)
            {
                HashSet<MethodSymbol> newEmitSet = new HashSet<MethodSymbol>();
                
                foreach (var methodSymbol in setToEmit)
                {
                    if (!emittedSet.Contains(methodSymbol))
                    {
                        if (methodSymbol.ContainingType.IsUdonSharpBehaviour) // Prevent other behaviour type's methods from leaking into this type from calls across behaviours
                        {
                            TypeSymbol topType = EmitType;
                            bool foundType = false;
                            while (topType != udonSharpBehaviourType)
                            {
                                if (methodSymbol.ContainingType == topType)
                                {
                                    foundType = true;
                                    break;
                                }
                                topType = topType.BaseType;
                            }
                            
                            if (!foundType)
                                continue;
                        }

                        _currentEmitMethod = methodSymbol;
                        methodSymbol.Emit(this);
                        _currentEmitMethod = null;
                        
                        emittedSet.Add(methodSymbol);
                        
                        newEmitSet.UnionWith(methodSymbol.DirectDependencies.OfType<MethodSymbol>());
                    }
                }

                setToEmit = newEmitSet;
            }
        }

        public Value GetReturnValue(TypeSymbol type)
        {
            var assignmentTarget = _assignmentScopes.Count > 0 ? _assignmentScopes.Peek().TargetValue : null;

            if (assignmentTarget != null && IsTriviallyAssignableTo(type, assignmentTarget.UserType))
                return assignmentTarget;
            
            return TopTable.CreateInternalValue(type);
        }

        public Value GetConstantValue(TypeSymbol type, object value)
        {
            return RootTable.GetConstantValue(type, value);
        }

        public Value GetUdonThisValue(TypeSymbol type)
        {
            return RootTable.GetUdonThisValue(type);
        }

        public Value CreateInternalValue(TypeSymbol type)
        {
            return TopTable.CreateInternalValue(type);
        }
        
        public Value CreateGlobalInternalValue(TypeSymbol type)
        {
            return TopTable.CreateGlobalInternalValue(type);
        }

        public Value GetUserValue(Symbol valueSymbol)
        {
            return TopTable.GetUserValue(valueSymbol);
        }

        public void EmitReturn()
        {
            Module.AddReturn(_returnValue);
        }

        public void EmitReturn(BoundExpression returnExpression)
        {
            if (returnExpression == null)
            {
                EmitReturn();
                return;
            }

            MethodLinkage currentMethodLinkage = GetMethodLinkage(_currentEmitMethod);
            
            EmitValueAssignment(currentMethodLinkage.ReturnValue, returnExpression);
            EmitReturn();
        }

        private MethodSymbol GetNumericConversionMethod(TypeSymbol sourceType, TypeSymbol targetType)
        {
            MethodSymbol convertMethod = GetTypeSymbol(typeof(Convert)).GetMembers<MethodSymbol>($"To{targetType.Name}", this)
                .First(e => e.Parameters[0].Type == sourceType);

            return convertMethod;
        }

        private Dictionary<Type, Value> _enumValues;
        
        /// <summary>
        /// Creates a const object array that is populated with each value of an enum which can be used for integer casts
        /// </summary>
        /// <param name="enumType"></param>
        /// <returns></returns>
        private Value GetEnumArrayForType(Type enumType)
        {
            if (_enumValues == null) // Lazy init since this will relatively never be used
                _enumValues = new Dictionary<Type, Value>();

            if (_enumValues.TryGetValue(enumType, out Value enumArrayValue))
                return enumArrayValue;

            int maxEnumVal = 0;
            foreach (var enumVal in Enum.GetValues(enumType))
                maxEnumVal = (int)enumVal > maxEnumVal ? (int)enumVal : maxEnumVal;

            // After a survey of what enums are exposed by Udon, it doesn't seem like anything goes above this limit. The only things I see that go past this are some System.Reflection enums which are unlikely to ever be exposed.
            if (maxEnumVal > 2048)
                throw new System.NotSupportedException($"Cannot cast integer to enum {enumType.Name} because target enum has too many potential states({maxEnumVal}) to contain in an UdonBehaviour reasonably");

            // Find the most significant bit of this enum so we can generate all combinations <= it
            int mostSignificantBit = 0;
            int currentEnumVal = maxEnumVal;

            while (currentEnumVal > 0)
            {
                currentEnumVal >>= 1;
                ++mostSignificantBit;
            }

            int enumValCount = (1 << mostSignificantBit) - 1;

            object[] enumConstArr = new object[enumValCount];

            for (int i = 0; i < enumConstArr.Length; ++i)
                enumConstArr[i] = Enum.ToObject(enumType, i);

            enumArrayValue = CreateGlobalInternalValue(GetTypeSymbol(SpecialType.System_Object).MakeArrayType(this));
            enumArrayValue.DefaultValue = enumConstArr;

            _enumValues.Add(enumType, enumArrayValue);

            return enumArrayValue;
        }

        private MethodSymbol _mathfFloorMethodSymbol;

        private void CastValue(Value sourceValue, Value targetValue, bool explicitCast)
        {
            if (sourceValue == targetValue)
                return;

            // Early out for exact type matches
            if (sourceValue.UserType == targetValue.UserType)
            {
                Module.AddCopy(sourceValue, targetValue);
                return;
            }

            TypeSymbol sourceType = sourceValue.UserType;
            TypeSymbol targetType = targetValue.UserType;

            void ExecuteBoundInvocation(MethodSymbol conversionMethod)
            {
                using (OpenAssignmentScope(targetValue))
                {
                    sourceValue = EmitValue(BoundInvocationExpression.CreateBoundInvocation(this, null, conversionMethod, null,
                        new BoundExpression[] {BoundAccessExpression.BindAccess(sourceValue)}));
                }

                if (sourceValue != targetValue)
                    Module.AddCopy(sourceValue, targetValue);
            }

            var conversion = CompileContext.RoslynCompilation.ClassifyConversion(sourceType.RoslynSymbol, targetType.RoslynSymbol);

            if (conversion.IsEnumeration)
            {
                if (sourceValue.UdonType.IsEnum &&
                    UdonSharpUtils.IsIntegerType(targetType.UdonType.SystemType))
                {
                    MethodSymbol conversionMethod =
                        GetNumericConversionMethod(GetTypeSymbol(SpecialType.System_Object), targetType);
                    ExecuteBoundInvocation(conversionMethod);
                    return;
                }

                if (UdonSharpUtils.IsIntegerType(sourceType.UdonType.SystemType) &&
                    targetType.UdonType.IsEnum)
                {
                    Value enumArray = GetEnumArrayForType(targetType.UdonType.SystemType);

                    Value indexValue = CastValue(sourceValue, GetTypeSymbol(SpecialType.System_Int32), true);
                    var boundElementAccess = BoundAccessExpression.BindElementAccess(this, null, BoundAccessExpression.BindAccess(enumArray),
                        new BoundExpression[]
                        {
                            BoundAccessExpression.BindAccess(indexValue),
                        });

                    Value enumVal = EmitValue(boundElementAccess);
                    
                    // todo: get rid of the copy again
                    Module.AddCopy(enumVal, targetValue);
                    return;
                }
            }

            if (conversion.IsNumeric || 
                (UdonSharpUtils.IsNumericType(targetType.UdonType.SystemType) && sourceType == GetTypeSymbol(SpecialType.System_Object)))
            {
                MethodSymbol conversionMethod = GetNumericConversionMethod(sourceType, targetType);

                if (conversionMethod != null)
                {
                    using (InterruptAssignmentScope())
                    {
                        // Float to int truncation handling since System.Convert rounds
                        if (UdonSharpUtils.IsFloatType(sourceType.UdonType.SystemType) &&
                            UdonSharpUtils.IsIntegerType(targetType.UdonType.SystemType))
                        {
                            TypeSymbol floatType = GetTypeSymbol(SpecialType.System_Single);
                            sourceValue = CastValue(sourceValue, floatType, true);

                            if (_mathfFloorMethodSymbol == null)
                                _mathfFloorMethodSymbol =
                                    GetTypeSymbol(typeof(Mathf)).GetMember<MethodSymbol>("Floor", this);

                            sourceValue = EmitValue(BoundInvocationExpression.CreateBoundInvocation(this, null, _mathfFloorMethodSymbol,
                                null,
                                new BoundExpression[] {BoundAccessExpression.BindAccess(sourceValue)}));
                        
                            conversionMethod = GetNumericConversionMethod(floatType, targetType);
                        }
                    }
                    
                    ExecuteBoundInvocation(conversionMethod);
                    return;
                }
            }

            if (conversion.IsUserDefined && conversion.MethodSymbol != null)
            {
                MethodSymbol conversionMethod = (MethodSymbol)GetSymbol(conversion.MethodSymbol);
                ExecuteBoundInvocation(conversionMethod);
                return;
            }
            
            Module.AddCopy(sourceValue, targetValue);
        }

        private TypeSymbol _systemObjectType;

        private bool IsTriviallyAssignableTo(TypeSymbol sourceType, TypeSymbol targetType)
        {
            if (sourceType == targetType)
                return true;
            
            if (_systemObjectType == null) _systemObjectType = GetTypeSymbol(SpecialType.System_Object);
            
            // Quick early out for assigning to object types since anything can technically be passed
            // todo: better checking for IsAssignableFrom equivalent functionality so we can skip copies on subclass assignments and such
            if (targetType == _systemObjectType)
                return true;

            return false;
        }

        public Value CastValue(Value sourceValue, TypeSymbol targetType, bool explicitCast)
        {
            if (IsTriviallyAssignableTo(sourceValue.UdonType, targetType))
                return sourceValue;
            
            Value resultValue = GetReturnValue(targetType);

            CastValue(sourceValue, resultValue, explicitCast);
            return resultValue;
        }

        public Value EmitValueAssignment(Value targetValue, BoundExpression sourceExpression, bool explicitCast = false)
        {
            targetValue.MarkDirty();
            
            using (OpenAssignmentScope(targetValue))
            {
                Value expressionResult = EmitValue(sourceExpression);

                if (expressionResult != targetValue)
                    CastValue(expressionResult, targetValue, explicitCast);

                return expressionResult;
            }
        }

        public JumpLabel TopContinueLabel => _continueLabelStack.Peek();
        
        public JumpLabel PushContinueLabel()
        {
            JumpLabel continueLabel = Module.CreateLabel();
            _continueLabelStack.Push(continueLabel);
            return continueLabel;
        }

        public void PopContinueLabel()
        {
            _continueLabelStack.Pop();
        }

        public JumpLabel TopBreakLabel => _breakLabelStack.Peek();

        public JumpLabel PushBreakLabel()
        {
            JumpLabel breakLabel = Module.CreateLabel();
            _breakLabelStack.Push(breakLabel);
            return breakLabel;
        }

        public void PopBreakLabel()
        {
            _breakLabelStack.Pop();
        }

        public void FlattenTableCounters()
        {
            if (TopTable.IsRoot || !TopTable.ParentTable.IsRoot)
                throw new InvalidOperationException("Table must be direct child of root table");
            
            TopTable.FlattenTableCountersToGlobal();
        }

        #region Scopes
    #if UDONSHARP_DEBUG
        private int _scopeDepth;
    #endif
        
        private void OpenScope()
        {
        #if UDONSHARP_DEBUG
            _scopeDepth += 1;
        #endif

            ValueTable newTable = new ValueTable(Module, TopTable);
            
            TopTable.AddChildTable(newTable);
            
            _valueTableStack.Push(newTable);
        }

        private void CloseScope()
        {
        #if UDONSHARP_DEBUG
            _scopeDepth -= 1;
            Debug.Assert(scopeDepth >= 0, "Incorrect scope pairing");
        #endif

            _valueTableStack.Pop();
        }

        public IDisposable OpenBlockScope()
        {
            if (_blockScopeCache.Count > 0)
            {
                OpenScope();
                return _blockScopeCache.Pop();
            }

            OpenScope();
            return new BlockScope(this);
        }

        private IDisposable OpenAssignmentScope(Value assignmentTarget)
        {
            AssignmentScope scope;

            if (_assignmentScopeCache.Count > 0)
                scope = _assignmentScopeCache.Pop();
            else
                scope = new AssignmentScope(this);

            scope.TargetValue = assignmentTarget;
            _assignmentScopes.Push(scope);

            return scope;
        }

        public IDisposable InterruptAssignmentScope()
        {
            return OpenAssignmentScope(null);
        }

        private class BlockScope : IDisposable
        {
            private EmitContext _context;
            
            public BlockScope(EmitContext context)
            {
                _context = context;
            }
            
            public void Dispose()
            {
                _context.CloseScope();
                _context._blockScopeCache.Push(this);
            }
        }

        private class AssignmentScope : IDisposable
        {
            public Value TargetValue { get; set; }
            private EmitContext _context;

            public AssignmentScope(EmitContext context)
            {
                _context = context;
            }

            public void Dispose()
            {
                TargetValue = null;
                
                _context._assignmentScopes.Pop();
                _context._assignmentScopeCache.Push(this);
            }
        }
        #endregion

        // todo: check private/protected usage of method and export
        public bool MethodNeedsExport(MethodSymbol methodSymbol)
        {
            if (methodSymbol.IsStatic)
                return false;
            
            return methodSymbol.RoslynSymbol.DeclaredAccessibility == Accessibility.Public ||
                   CompilerUdonInterface.IsUdonEvent(methodSymbol.Name) ||
                   (methodSymbol is UdonSharpBehaviourMethodSymbol udonSharpBehaviourMethodSymbol && udonSharpBehaviourMethodSymbol.NeedsExportFromReference);
        }

        public class MethodLinkage
        {
            public JumpLabel MethodLabel { get; }
            public string MethodExportName { get; }
            public Value ReturnValue { get; }
            public Value[] ParameterValues { get; }

            public MethodLinkage(JumpLabel label, string exportName, Value returnValue, Value[] parameterValues)
            {
                MethodLabel = label;
                MethodExportName = exportName;
                ReturnValue = returnValue;
                ParameterValues = parameterValues;
            }
        }

        private Dictionary<MethodSymbol, MethodLinkage> _virtualLinkages = new Dictionary<MethodSymbol,MethodLinkage>();
        private Dictionary<MethodSymbol, MethodLinkage> _directLinkages = new Dictionary<MethodSymbol,MethodLinkage>();

        /// <summary>
        /// Gets the most derived method of a given method symbol in the current emit type's context.
        /// </summary>
        /// <param name="methodSymbol"></param>
        /// <returns></returns>
        private MethodSymbol GetMostDerivedMethod(MethodSymbol methodSymbol)
        {
            if (!methodSymbol.HasOverrides)
                return methodSymbol;

            TypeSymbol currentSearchType = EmitType;

            MethodSymbol derivedSymbol = null;

            while (currentSearchType != null)
            {
                foreach (var symbol in currentSearchType.GetMembers<MethodSymbol>(methodSymbol.Name, this))
                {
                    if (symbol.Parameters.Length != methodSymbol.Parameters.Length) // early out when the method obviously doesn't overload this
                        continue;

                    MethodSymbol currentMethod = symbol;

                    while (currentMethod != null && currentMethod != methodSymbol)
                        currentMethod = currentMethod.OverridenMethod;

                    if (currentMethod == methodSymbol)
                    {
                        derivedSymbol = symbol;
                        break;
                    }
                }
                
                if (derivedSymbol != null)
                    break;

                currentSearchType = currentSearchType.BaseType;
            }

            return derivedSymbol;
        }

        private int _internalParamNameCounter;

        private string GetInternalParamName()
        {
            return $"__{_internalParamNameCounter++}__intnlparam";
        }
        
        /// <summary>
        /// Retrieves the linkage for a local user method call.
        /// A local user method call means any method that is not a call on a different Udon Behaviour.
        /// This includes any static function call, function call on an imported user type, or call on the local UdonSharpBehaviour
        /// </summary>
        /// <param name="methodSymbol">The method symbol to get the linkage for</param>
        /// <param name="useVirtual">Whether this is a virtual call where we want the most derived method to be called</param>
        /// <returns></returns>
        public MethodLinkage GetMethodLinkage(MethodSymbol methodSymbol, bool useVirtual = true)
        {
            if (!useVirtual && _directLinkages.TryGetValue(methodSymbol, out var linkage))
                return linkage;
            
            MethodSymbol mostBaseMethod = methodSymbol;
            while (mostBaseMethod.OverridenMethod != null)
                mostBaseMethod = mostBaseMethod.OverridenMethod;
            
            if (useVirtual && _virtualLinkages.TryGetValue(mostBaseMethod, out linkage))
                return linkage;
            
            MethodSymbol derivedMethod = GetMostDerivedMethod(methodSymbol);

            MethodLinkage newLinkage;

            JumpLabel methodLabel = Module.CreateLabel();
            Value[] parameterValues = new Value[methodSymbol.Parameters.Length];
            
            if ((useVirtual || derivedMethod == methodSymbol) && methodSymbol is UdonSharpBehaviourMethodSymbol)
            {
                methodLabel.DebugMethod = derivedMethod;
                
                CompilationContext.MethodExportLayout layout = CompileContext.GetUsbMethodLayout(methodSymbol, this);

                Value returnValue = layout.ReturnExportName == null ? 
                    null : 
                    Module.RootTable.CreateParameterValue(layout.ReturnExportName, derivedMethod.ReturnType);

                for (int i = 0; i < parameterValues.Length; ++i)
                {
                    parameterValues[i] = Module.RootTable.CreateParameterValue(layout.ParameterExportNames[i], methodSymbol.Parameters[i].Type);
                }

                newLinkage = new MethodLinkage(methodLabel, layout.ExportMethodName, returnValue, parameterValues);
                
                _virtualLinkages.Add(mostBaseMethod, newLinkage);
                
                if (methodSymbol == derivedMethod)
                    _directLinkages.Add(methodSymbol, newLinkage);
            }
            else
            {
                methodLabel.DebugMethod = methodSymbol;
                
                Value returnValue = methodSymbol.ReturnType == null
                    ? null
                    : Module.RootTable.CreateParameterValue(GetInternalParamName(), methodSymbol.ReturnType);

                for (int i = 0; i < parameterValues.Length; ++i)
                {
                    parameterValues[i] = Module.RootTable.CreateParameterValue(GetInternalParamName(), methodSymbol.Parameters[i].Type);
                }

                newLinkage = new MethodLinkage(methodLabel, null, returnValue, parameterValues);
                
                _directLinkages.Add(methodSymbol, newLinkage);
                
                if (methodSymbol == derivedMethod)
                    _virtualLinkages.Add(methodSymbol, newLinkage);
            }

            return newLinkage;
        }

        public void Emit(BoundNode node)
        {
            node.Emit(this);
            
            if (node is BoundExpression boundExpression)
                boundExpression.ReleaseCowReferences(this);
        }
        
        public Value EmitValue(BoundExpression expression)
        {
            Value result = expression.EmitValue(this);
            expression.ReleaseCowReferences(this);
            
            return result;
        }

        public Value EmitValueWithDeferredRelease(BoundExpression expression)
        {
            return expression.EmitValue(this);
        }

        public Value EmitSet(BoundAccessExpression targetExpression, BoundExpression sourceExpression)
        {
            Value resultVal = targetExpression.EmitSet(this, sourceExpression);
            targetExpression.ReleaseCowReferences(this);

            return resultVal;
        }

        public Value.CowValue[] GetExpressionCowValues(BoundExpression expression, string key)
        {
            if (_expressionCowValueTracker.TryGetValue(expression, out var valueLookup))
            {
                if (valueLookup.TryGetValue(key, out var values))
                    return values;
            }

            return null;
        }

        public void RegisterCowValues(Value.CowValue[] values, BoundExpression expression, string key)
        {
            if (!_expressionCowValueTracker.TryGetValue(expression, out var valueLookup))
            {
                valueLookup = new Dictionary<string, Value.CowValue[]>();
                _expressionCowValueTracker.Add(expression, valueLookup);
            }
            
            valueLookup.Add(key, values);
        }

        public void ReleaseCowValues(BoundExpression expression)
        {
            if (!_expressionCowValueTracker.TryGetValue(expression, out var valuesMap)) return;
            
            foreach (var valueMap in valuesMap)
            {
                foreach (var value in valueMap.Value)
                {
                    value.Dispose();
                }
            }

            _expressionCowValueTracker.Remove(expression);
        }
    }
}
