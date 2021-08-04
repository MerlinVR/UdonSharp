using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

namespace UdonSharp.Compiler
{
    public enum ExpressionCaptureArchetype
    {
        Unknown, // This could be multiple things, the reference is currently ambiguous
        Namespace,
        Property,
        Field, // For Udon, fields are largely treated the same way as properties, they have getters and setters defined.
        LocalSymbol,
        Method,
        Type,
        ArrayIndexer,
        This, // this.<next token> indicates that this must be a local symbol or method
        Enum,
        LocalMethod,
        ExternUserField,
        ExternUserMethod,
        InternalUdonSharpMethod,
        LocalProperty,
        ExternUserProperty,
    }

    /// <summary>
    /// There's probably a better way to handle this, 
    ///   but this class tracks the most recent symbol/type/namespace/method group that was last potentially referenced.
    /// This is currently used to resolve chains of statements where the user can do something like UnityEngine.Color.Red.r, 
    ///     in this case, the user first specifies a namespace, then a type in that namespace, then a static property on that type, then a field in that property
    ///     However, all we have to go off from this token-wise is a tree of SimpleMemberAccessExpression.
    ///     So obviously in this case, the first actionable thing we need to do is run the internal function get_red() on the Color type.
    ///     Then we need to get the value of the r field from that output.
    ///     But what if the user was setting a color on a light or something? Then you'd have light.color = Color.Red; 
    ///     So you also need to handle the difference between set and get on properties 
    /// </summary>
    public class ExpressionCaptureScope : System.IDisposable
    {
        public string unresolvedAccessChain { get; private set; } = "";
        public ExpressionCaptureArchetype captureArchetype { get; private set; } = ExpressionCaptureArchetype.Unknown;

        public bool isAttributeCaptureScope { get; set; } = false;
        public bool shouldSkipRecursivePush { get; set; } = false;

        // Only the parameters corresponding with the current captureArchetype are guaranteed to be valid
        public string captureNamespace { get; private set; } = "";
        public PropertyInfo captureProperty { get; private set; } = null;
        public FieldInfo captureField { get; private set; } = null;
        public SymbolDefinition captureLocalSymbol { get; private set; } = null;
        public MethodBase[] captureMethods { get; private set; } = null; // If there are override methods we do not know which to use until we invoke them with the arguments
        public System.Type captureType { get; private set; } = null;
        public string captureEnum { get; private set; } = "";
        public MethodDefinition captureLocalMethod { get; private set; } = null;
        public PropertyDefinition captureLocalProperty { get; private set; } = null;
        public FieldDefinition captureExternUserField { get; private set; } = null;
        public MethodDefinition captureExternUserMethod { get; private set; } = null;
        public PropertyDefinition captureExternUserProperty { get; private set; } = null;
        public InternalMethodHandler InternalMethodHandler { get; private set; } = null;

        // In some cases, we know ahead of time that we want to store a particular value in a particular symbol.
        // For example, this applies when performing an assignment (x = foo())
        // In this case, we pass in a requested destination field, and if the expression is capable of writing directly to that
        // field, we can avoid an extra copy.
        // It should be noted that the expression MUST NOT write any intermediate values to this expression, as they could be observed
        // with function calls or recursive invocations of the UdonBehavior. The field can only be written to when there is no longer any 
        // possibility of any user code running in the context of the expression.
        public SymbolDefinition requestedDestination { get; set; } = null;

        // If the caller requested a copy-on-write read, we'll store the COW reference here and clean it up on disposal.
        // Note that this is only a cache used to help with cleanup, and is not considered to be the value of the object
        // (and therefore is not inherited).
        private SymbolDefinition.COWValue cowValue = null;
        
        private SymbolDefinition _accessSymbol = null;
        public SymbolDefinition accessSymbol
        {
            get
            {
                if (accessValue != null)
                    return accessValue.symbol;

                return _accessSymbol;
            }
            private set
            {
                accessValue = null;
                _accessSymbol = value;
            }
        }

        private SymbolDefinition.COWValue _accessValue = null;
        public SymbolDefinition.COWValue accessValue {
            get
            {
                return _accessValue;
            }
            private set {
                if (_accessValue != null)
                {
                    _accessValue.Dispose();
                }
                if (value != null)
                {
                    _accessValue = value.AddRef();
                    _accessSymbol = null;
                }
                else
                {
                    _accessValue = null;
                }
            }
        }

        // Used for array indexers
        private SymbolDefinition.COWValue _arrayBacktraceValue = null;
        private SymbolDefinition.COWValue arrayBacktraceValue
        {
            get
            {
                return _arrayBacktraceValue;
            }
            set
            {
                if (_arrayBacktraceValue != null)
                {
                    _arrayBacktraceValue.Dispose();
                }
                if (value != null)
                {
                    _arrayBacktraceValue = value.AddRef();
                }
                else
                {
                    _arrayBacktraceValue = null;
                }
            }
        }

        private SymbolDefinition.COWValue _arrayIndexerIndexValue = null;
        private SymbolDefinition.COWValue arrayIndexerIndexValue
        {
            get
            {
                return _arrayIndexerIndexValue;
            }
            set
            {
                if (_arrayIndexerIndexValue != null)
                {
                    _arrayIndexerIndexValue.Dispose();
                }
                if (value != null)
                {
                    _arrayIndexerIndexValue = value.AddRef();
                } else
                {
                    _arrayIndexerIndexValue = null;
                }
            }
        }

        // Used for resolving generic methods, and eventually types when Udon adds support
        private List<System.Type> genericTypeArguments = null;

        // Namespace lookup to check if something is a namespace
        private static HashSet<string> allLinkedNamespaces;
        private static bool namespacesInit = false;
        private static object namespaceLock = new object();

        // The current visitor context
        ASTVisitorContext visitorContext;

        ExpressionCaptureScope parentScope;

        bool disposed = false;

        private void FindValidNamespaces()
        {
            if (namespacesInit)
                return;

            lock (namespaceLock)
            {
                if (namespacesInit)
                    return;

                allLinkedNamespaces = new HashSet<string>();

                foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    allLinkedNamespaces.UnionWith(assembly.GetTypes().Select(e => e.Namespace).Distinct());
                }

                namespacesInit = true;
            }
        }

        public ExpressionCaptureScope(ASTVisitorContext context, ExpressionCaptureScope parentScopeIn, SymbolDefinition requestedDestinationIn = null)
        {
            FindValidNamespaces();

            visitorContext = context;
            parentScope = parentScopeIn;
            requestedDestination = requestedDestinationIn;
            InternalMethodHandler = new InternalMethodHandler(context, this);

            visitorContext.PushCaptureScope(this);
        }

        ~ExpressionCaptureScope()
        {
            Debug.Assert(disposed, "Expression capture scope was not disposed!");
        }
        
        public void Dispose()
        {
            if (parentScope != null)
                parentScope.InheritScope(this);
            
            Debug.Assert(visitorContext.topCaptureScope == this);
            if (visitorContext.topCaptureScope == this)
                visitorContext.PopCaptureScope();

            if (cowValue != null)
            {
                cowValue.Dispose();
                cowValue = null;
            }

            accessValue = null;
            arrayIndexerIndexValue = null;
            arrayBacktraceValue = null;

            disposed = true;
        }

        private void InheritScope(ExpressionCaptureScope childScope)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Method ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Namespace)
                return;

            captureArchetype = childScope.captureArchetype;
            captureField = childScope.captureField;
            captureLocalSymbol = childScope.captureLocalSymbol;
            captureProperty = childScope.captureProperty;
            captureType = childScope.captureType;
            accessSymbol = childScope._accessSymbol;
            accessValue = childScope.accessValue;
            captureEnum = childScope.captureEnum;
            arrayIndexerIndexValue = childScope.arrayIndexerIndexValue;
            captureLocalMethod = childScope.captureLocalMethod;
            captureLocalProperty = childScope.captureLocalProperty;
            captureExternUserField = childScope.captureExternUserField;
            captureExternUserMethod = childScope.captureExternUserMethod;
            captureExternUserProperty = childScope.captureExternUserProperty;
            unresolvedAccessChain = childScope.unresolvedAccessChain;
        }

        private void CheckScopeValidity()
        {
            if (IsUnknownArchetype())
            {
                string[] unresolvedTokens = unresolvedAccessChain.Split('.');
                string invalidName = unresolvedTokens.Length > 1 ? unresolvedTokens[unresolvedTokens.Length - 2] : unresolvedTokens[0];
                throw new System.Exception($"The name '{invalidName}' does not exist in the current context");
            }
        }

        public void SetToLocalSymbol(SymbolDefinition symbol)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a local symbol");

            captureArchetype = ExpressionCaptureArchetype.LocalSymbol;
            captureLocalSymbol = symbol;
            accessSymbol = symbol;
        }

        public void SetToType(System.Type type)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a type");

            captureArchetype = ExpressionCaptureArchetype.Type;
            captureType = type;
        }

        public void SetToMethods(MethodBase[] methods)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a method");
            
            foreach (MethodBase method in methods)
            {
                if (!method.IsStatic && !(method is ConstructorInfo))
                    throw new System.ArgumentException("All methods set in SetToMethods must be static");
            }

            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = methods;
        }

        public void MakeArrayType()
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Capture scope must have the 'Type' archetype to convert to an array");

            captureType = captureType.MakeArrayType();
        }

        #region Archetype check functions
        public bool HasBeenAssigned()
        {
            return !(IsUnknownArchetype() && unresolvedAccessChain.Length == 0);
        }

        public bool IsUnknownArchetype()
        {
            return captureArchetype == ExpressionCaptureArchetype.Unknown;
        }

        public bool IsNamespace()
        {
            return captureArchetype == ExpressionCaptureArchetype.Namespace;
        }

        public bool IsMethod()
        {
            return captureArchetype == ExpressionCaptureArchetype.Method || 
                   captureArchetype == ExpressionCaptureArchetype.LocalMethod || 
                   captureArchetype == ExpressionCaptureArchetype.ExternUserMethod ||
                   captureArchetype == ExpressionCaptureArchetype.InternalUdonSharpMethod;
        }

        public bool IsProperty()
        {
            return captureArchetype == ExpressionCaptureArchetype.Property;
        }

        public bool IsField()
        {
            return captureArchetype == ExpressionCaptureArchetype.Field ||
                   captureArchetype == ExpressionCaptureArchetype.ExternUserField;
        }

        public bool IsType()
        {
            return captureArchetype == ExpressionCaptureArchetype.Type;
        }

        public bool IsThis()
        {
            return captureArchetype == ExpressionCaptureArchetype.This;
        }

        public bool IsArrayIndexer()
        {
            return captureArchetype == ExpressionCaptureArchetype.ArrayIndexer;
        }

        public bool IsLocalSymbol()
        {
            return captureArchetype == ExpressionCaptureArchetype.LocalSymbol;
        }

        public bool IsEnum()
        {
            return captureArchetype == ExpressionCaptureArchetype.Enum;
        }
        #endregion

        public object GetEnumValue()
        {
            if (!IsEnum())
                throw new System.Exception("Cannot get enum value from non-enum capture");

            return System.Enum.Parse(captureType, captureEnum);
        }

        private MethodInfo GetUdonGetMethodInfo()
        {
            if (!IsProperty())
                throw new System.Exception("Cannot get property get method on non-properties");

            if (captureProperty.ReflectedType == typeof(VRC.Udon.UdonBehaviour))
            {
                PropertyInfo property = typeof(Component).GetProperty(captureProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (property == null)
                    property = typeof(VRC.Udon.UdonBehaviour).GetProperty(captureProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                if (property == null)
                    return null;

                return property.GetGetMethod();
            }

            return captureProperty.GetGetMethod();
        }

        private MethodInfo GetUdonSetMethodInfo()
        {
            if (!IsProperty())
                throw new System.Exception("Cannot get property get method on non-properties");

            if (captureProperty.ReflectedType == typeof(VRC.Udon.UdonBehaviour))
            {
                PropertyInfo property = typeof(Component).GetProperty(captureProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                if (property == null)
                    property = typeof(VRC.Udon.UdonBehaviour).GetProperty(captureProperty.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                if (property == null)
                    return null;

                return property.GetSetMethod();
            }

            return captureProperty.GetSetMethod();
        }

        public SymbolDefinition.COWValue ExecuteGetCOW()
        {
            if (cowValue == null)
            {
                cowValue = ExecuteGet().GetCOWValue(visitorContext);
            }

            return cowValue;
        }

        // Inserts uasm instructions to get the value stored in the current localSymbol, property, or field
        public SymbolDefinition ExecuteGet()
        {
            arrayBacktraceValue = null;

            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
                return captureLocalSymbol;

            SymbolDefinition outSymbol = null;
            
            CheckScopeValidity();

            if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                MethodInfo getMethod = GetUdonGetMethodInfo();

                if (getMethod == null)
                    throw new System.MemberAccessException($"Property or indexer '{captureProperty.DeclaringType.Name}.{captureProperty.Name}' doesn't exist");

                if (getMethod.ReturnType == typeof(void))
                    throw new System.TypeLoadException("Cannot return type of void from a get statement");

                outSymbol = AllocateOutputSymbol(getMethod.ReturnType);

                string methodUdonString = visitorContext.resolverContext.GetUdonMethodName(getMethod);

                if (!getMethod.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(methodUdonString);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.LocalProperty)
            {
                PropertyDefinition definition = captureLocalProperty;

                GetterDefinition getter = definition.getter;
                if (getter.type == typeof(void))
                    throw new System.TypeLoadException("Cannot return type of void from a get statement");

                SymbolDefinition exitJumpLocation = visitorContext.topTable.CreateNamedSymbol("exitJumpLoc", typeof(uint), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant);

                visitorContext.uasmBuilder.AddPush(exitJumpLocation);
                visitorContext.uasmBuilder.AddJump(getter.userCallStart);

                JumpLabel exitLabel = visitorContext.labelTable.GetNewJumpLabel("returnLocation");

                visitorContext.uasmBuilder.AddJumpLabel(exitLabel);
                exitJumpLocation.symbolDefaultValue = exitLabel.resolvedAddress;

                outSymbol = AllocateOutputSymbol(getter.returnSymbol.userCsType);
                visitorContext.uasmBuilder.AddCopy(outSymbol, getter.returnSymbol);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserProperty)
            {
                PropertyDefinition definition = captureExternUserProperty;

                GetterDefinition getter = definition.getter;
                if (getter.type == typeof(void))
                    throw new System.TypeLoadException("Cannot return type of void from a get statement");

                outSymbol = AllocateOutputSymbol(getter.type);

                using (ExpressionCaptureScope getPropertyMethodScope = new ExpressionCaptureScope(visitorContext, null, requestedDestination))
                {
                    getPropertyMethodScope.SetToLocalSymbol(accessSymbol);
                    getPropertyMethodScope.ResolveAccessToken("SendCustomEvent");
                    getPropertyMethodScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), getter.accessorName) });
                }

                using (ExpressionCaptureScope getReturnScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    getReturnScope.SetToLocalSymbol(accessSymbol);
                    getReturnScope.ResolveAccessToken("GetProgramVariable");

                    SymbolDefinition externVarReturn = getReturnScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), getter.returnSymbol.symbolUniqueName) });
                    outSymbol = CastSymbolToType(externVarReturn, getter.type, true, true, outSymbol == requestedDestination ? requestedDestination : null);
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                outSymbol = AllocateOutputSymbol(captureField.FieldType);

                string fieldAccessorUdonName = visitorContext.resolverContext.GetUdonFieldAccessorName(captureField, FieldAccessorType.Get);

                if (!captureField.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(fieldAccessorUdonName);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserField)
            {
                outSymbol = AllocateOutputSymbol(captureExternUserField.fieldSymbol.symbolCsType);

                using (ExpressionCaptureScope getVariableMethodScope = new ExpressionCaptureScope(visitorContext, null, requestedDestination))
                {
                    getVariableMethodScope.SetToLocalSymbol(accessSymbol);
                    getVariableMethodScope.ResolveAccessToken("GetProgramVariable");

                    SymbolDefinition externVarReturn = getVariableMethodScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserField.fieldSymbol.symbolUniqueName) });
                    outSymbol = CastSymbolToType(externVarReturn, captureExternUserField.fieldSymbol.userCsType, true, true, outSymbol == requestedDestination ? requestedDestination : null);
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                SymbolDefinition arraySymbol = accessValue.symbol;
                System.Type elementType = null;

                System.Type arraySymbolType = arraySymbol.symbolCsType;

                string getIndexerUdonName;
                if (arraySymbolType == typeof(string))
                {
                    // udon-workaround: This is where support for Udon's string indexer would go, IF IT HAD ONE
                    //getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(arraySymbol.symbolCsType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "get_Chars").First());
                    
                    elementType = typeof(char);

                    SymbolDefinition substringStrSymbol;
                    using (ExpressionCaptureScope substringScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        substringScope.SetToLocalSymbol(arraySymbol);
                        substringScope.ResolveAccessToken(nameof(string.Substring));

                        substringStrSymbol = substringScope.Invoke(new SymbolDefinition[] { arrayIndexerIndexValue.symbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    }

                    SymbolDefinition subStrCharArrSymbol;

                    using (ExpressionCaptureScope charArrScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        charArrScope.SetToLocalSymbol(substringStrSymbol);
                        charArrScope.ResolveAccessToken(nameof(string.ToCharArray));

                        subStrCharArrSymbol = charArrScope.Invoke(new SymbolDefinition[] { });
                    }

                    getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(typeof(char[]).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Get"));
                    visitorContext.uasmBuilder.AddPush(subStrCharArrSymbol);
                    visitorContext.uasmBuilder.AddPush(visitorContext.topTable.CreateConstSymbol(typeof(int), 0)); // 0 index
                }
                else if (arraySymbolType == typeof(Vector2) ||
                         arraySymbolType == typeof(Vector3) ||
                         arraySymbolType == typeof(Vector4) ||
                         arraySymbolType == typeof(Matrix4x4))
                {
                    elementType = typeof(float);

                    getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(arraySymbolType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "get_Item" && e.GetParameters().Length == 1));

                    visitorContext.uasmBuilder.AddPush(arraySymbol);
                    visitorContext.uasmBuilder.AddPush(arrayIndexerIndexValue.symbol);
                }
                else
                {
                    // udon-workaround: VRC scans UnityEngine.Object arrays in their respective methods, so those methods are useless since they get disproportionately expensive the larger the array is.
                    // Instead use the object[] indexer for these objects since it does not get scanned
                    if (arraySymbolType.GetElementType() == typeof(UnityEngine.Object) || arraySymbolType.GetElementType().IsSubclassOf(typeof(UnityEngine.Object)))
                        getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(typeof(object[]).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Get"));
                    else
                        getIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(arraySymbolType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Get"));

                    elementType = arraySymbol.userCsType.GetElementType();

                    visitorContext.uasmBuilder.AddPush(arraySymbol);
                    visitorContext.uasmBuilder.AddPush(arrayIndexerIndexValue.symbol);
                }

                arrayBacktraceValue = accessValue;

                outSymbol = AllocateOutputSymbol(elementType);

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(getIndexerUdonName);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.This)
            {
                outSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Enum)
            {
                // Capture type should still be valid from the last transition
                outSymbol = visitorContext.topTable.CreateConstSymbol(captureType, GetEnumValue());
            }
            else
            {
                throw new System.Exception("Get can only be run on Fields, Properties, Local Symbols, array indexers, and the `this` keyword");
            }

            return outSymbol;
        }

        public SymbolDefinition destinationSymbolForSet { 
            get
            {
                if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
                {
                    if (captureLocalSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) || captureLocalSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.This))
                        return null;

                    return captureLocalSymbol;
                } else
                {
                    return null;
                }
            } 
        }

        public void ExecuteSet(SymbolDefinition value, bool explicitCast = false)
        {
            CheckScopeValidity();

            SymbolDefinition destinationSymbol = destinationSymbolForSet;
            SymbolDefinition convertedValue = CastSymbolToType(value, GetReturnType(true), explicitCast, false, destinationSymbol);

            // If it's a local symbol, it's just a simple COPY
            if (destinationSymbolForSet != null)
            {
                if (destinationSymbolForSet != convertedValue)
                {
                    destinationSymbol.MarkDirty();
                    visitorContext.uasmBuilder.AddCopy(destinationSymbol, convertedValue);
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                MethodInfo setMethod = GetUdonSetMethodInfo();

                if (setMethod == null)
                    throw new System.MemberAccessException($"Property or indexer '{captureProperty.DeclaringType.Name}.{captureProperty.Name}' cannot be assigned to -- it is read only or doesn't exist");

                string udonMethodString = visitorContext.resolverContext.GetUdonMethodName(setMethod);

                if (!setMethod.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(udonMethodString);

                JumpLabel exitLabel = visitorContext.labelTable.GetNewJumpLabel("returnLocation");
                visitorContext.uasmBuilder.AddJumpLabel(exitLabel);
            
            }
            else if (captureArchetype == ExpressionCaptureArchetype.LocalProperty)
            {
                PropertyDefinition definition = captureLocalProperty;
                SetterDefinition setter = definition.setter;

                if (setter == null)
                    throw new System.MemberAccessException($"Property or indexer '{definition.originalPropertyName}' cannot be assigned to -- it is read only or doesn't exist");

                using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    argAssignmentScope.SetToLocalSymbol(setter.paramSymbol);
                    argAssignmentScope.ExecuteSet(convertedValue);
                }

                SymbolDefinition exitJumpLocation = visitorContext.topTable.CreateNamedSymbol("exitJumpLoc", typeof(uint), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant);

                visitorContext.uasmBuilder.AddPush(exitJumpLocation);
                visitorContext.uasmBuilder.AddJump(setter.userCallStart);
                JumpLabel exitLabel = visitorContext.labelTable.GetNewJumpLabel("returnLocation");

                visitorContext.uasmBuilder.AddJumpLabel(exitLabel);
                exitJumpLocation.symbolDefaultValue = exitLabel.resolvedAddress;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserProperty)
            {
                PropertyDefinition definition = captureExternUserProperty;
                SetterDefinition setter = definition.setter;

                if (setter == null || setter.declarationFlags == PropertyDeclFlags.Private)
                    throw new System.MemberAccessException($"Property or indexer '{definition.originalPropertyName}' cannot be assigned to -- it is read only or doesn't exist");

                using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    argAssignmentScope.SetToLocalSymbol(accessSymbol);
                    argAssignmentScope.ResolveAccessToken("SetProgramVariable");

                    argAssignmentScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), setter.paramSymbol.symbolUniqueName), convertedValue });
                }

                using (ExpressionCaptureScope setPropertyMethodScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    setPropertyMethodScope.SetToLocalSymbol(accessSymbol);
                    setPropertyMethodScope.ResolveAccessToken("SendCustomEvent");
                    setPropertyMethodScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), setter.accessorName) });
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                string fieldSetterUdonName = visitorContext.resolverContext.GetUdonFieldAccessorName(captureField, FieldAccessorType.Set);

                if (!captureField.IsStatic)
                    visitorContext.uasmBuilder.AddPush(accessSymbol);

                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(fieldSetterUdonName);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserField)
            {
                if (visitorContext.onModifyCallbackFields.Values.Any(e => e.fieldSymbol.symbolUniqueName == captureExternUserField.fieldSymbol.symbolUniqueName))
                    throw new System.InvalidOperationException($"Cannot set field with {nameof(FieldChangeCallbackAttribute)}, use a property or SetProgramVariable");

                using (ExpressionCaptureScope setVariableMethodScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    setVariableMethodScope.SetToLocalSymbol(accessSymbol);
                    setVariableMethodScope.ResolveAccessToken("SetProgramVariable");

                    setVariableMethodScope.Invoke(new SymbolDefinition[] {
                        visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserField.fieldSymbol.symbolUniqueName),
                        convertedValue
                    });
                }
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                SymbolDefinition arraySymbol = accessValue.symbol;
                string setIndexerUdonName;
                System.Type arraySymbolType = arraySymbol.symbolCsType;

                if (arraySymbolType == typeof(Vector2) ||
                    arraySymbolType == typeof(Vector3) ||
                    arraySymbolType == typeof(Vector4) ||
                    arraySymbolType == typeof(Matrix4x4))
                {
                    setIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(arraySymbol.symbolCsType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "set_Item" && e.GetParameters().Length == 2));
                }
                else
                {
                    // udon-workaround: VRC scans UnityEngine.Object arrays in their respective methods, so those methods are useless since they get disproportionately expensive the larger the array is.
                    // Instead use the object[] indexer for these objects since it does not get scanned
                    if (arraySymbolType.GetElementType() == typeof(UnityEngine.Object) || arraySymbolType.GetElementType().IsSubclassOf(typeof(UnityEngine.Object)))
                        setIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(typeof(object[]).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Set"));
                    else
                        setIndexerUdonName = visitorContext.resolverContext.GetUdonMethodName(arraySymbolType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Set"));
                }

                visitorContext.uasmBuilder.AddPush(arraySymbol);
                visitorContext.uasmBuilder.AddPush(arrayIndexerIndexValue.symbol);
                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(setIndexerUdonName);
            }
            else
            {
                throw new System.Exception("Set can only be run on Fields, Properties, and local Symbols");
            }

            // Copy the result back into the array if it's a value type
            if (NeedsArrayCopySet())
            {
                using (ExpressionCaptureScope arraySetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arraySetScope.SetToLocalSymbol(arrayBacktraceValue.symbol);
                    arraySetScope.HandleArrayIndexerAccess(arrayIndexerIndexValue);

                    arraySetScope.ExecuteSet(accessSymbol);
                }
            }
        }

        // Just a stub for now that will be extended to avoid the COPY instruction when possible
        public void ExecuteSetDirect(ExpressionCaptureScope valueExpression, bool explicitCast = false)
        {
            CheckScopeValidity();

            ExecuteSet(valueExpression.ExecuteGet(), explicitCast);
        }

        public SymbolDefinition AllocateOutputSymbol(System.Type returnType)
        {
            SymbolDefinition requestedDestination = this.requestedDestination;

            if (requestedDestination == null || requestedDestination.symbolCsType != returnType)
            {
                SymbolDefinition returnSymbol = visitorContext.topTable.CreateUnnamedSymbol(returnType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local); ;
                if (visitorContext.topCaptureScope != null && visitorContext.topCaptureScope.IsUnknownArchetype())
                    visitorContext.topCaptureScope.SetToLocalSymbol(returnSymbol);

                return returnSymbol;
            }

            requestedDestination.MarkDirty();

            return requestedDestination;
        }

        /// <summary>
        /// Creates a const object array that is populated with each value of an enum which can be used for integer casts
        /// </summary>
        /// <param name="enumType"></param>
        /// <returns></returns>
        SymbolDefinition GetEnumArrayForType(System.Type enumType)
        {
            if (visitorContext.enumCastSymbols == null) // Lazy init since this will relatively never be used
                visitorContext.enumCastSymbols = new Dictionary<System.Type, SymbolDefinition>();

            SymbolDefinition enumArraySymbol;
            if (visitorContext.enumCastSymbols.TryGetValue(enumType, out enumArraySymbol))
                return enumArraySymbol;

            int maxEnumVal = 0;
            foreach (var enumVal in System.Enum.GetValues(enumType))
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
                enumConstArr[i] = System.Enum.ToObject(enumType, i);

            enumArraySymbol = visitorContext.topTable.CreateConstSymbol(typeof(object[]), enumConstArr);

            visitorContext.enumCastSymbols.Add(enumType, enumArraySymbol);

            return enumArraySymbol;
        }

        // There's probably a better place for this function...
        public SymbolDefinition CastSymbolToType(SymbolDefinition sourceSymbol, System.Type targetType, bool isExplicit, bool needsNewSymbol = false, SymbolDefinition requestedDestination = null)
        {
            if (targetType.IsByRef) // Convert ref and out args to their main types.
                targetType = targetType.GetElementType();

            // Special case for passing through user defined classes if they match
            if ((sourceSymbol.IsUserDefinedType() || UdonSharpUtils.IsUdonWorkaroundType(sourceSymbol.userCsType)) && 
                (targetType.IsAssignableFrom(sourceSymbol.userCsType) || (targetType.IsArray && targetType == sourceSymbol.userCsType)))
                return sourceSymbol;
            
            // Special case for assigning objects to non-value types so we can assign and the output of things that return a generic object
            // This lets the user potentially break their stuff if they assign an object return value from some function to a heap variable with a non-matching type. 
            // For instance you could assign a Transform component to a Renderer component variable, and you'd only realize the error when you tried to treat the Renderer as a Renderer.
            // This can't be trivially type checked at runtime with what Udon exposes in System.Type at the moment.
            bool isObjectAssignable = !targetType.IsValueType && sourceSymbol.symbolCsType == typeof(object);

            bool isNumericCastValid = UdonSharpUtils.IsNumericImplicitCastValid(targetType, sourceSymbol.symbolCsType) ||
                 (sourceSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) && sourceSymbol.symbolCsType == typeof(int)); // Handle Roslyn giving us ints constant expressions

            if ((!isExplicit && !targetType.IsImplicitlyAssignableFrom(sourceSymbol.userCsType)) &&
                !isObjectAssignable && !isNumericCastValid)
                throw new System.ArgumentException($"Cannot implicitly convert type '{UdonSharpUtils.PrettifyTypeName(sourceSymbol.userCsType)}' to '{UdonSharpUtils.PrettifyTypeName(targetType)}'");

            // Exact type match, just return the symbol, this is what will happen a majority of the time.
            if (targetType == sourceSymbol.symbolCsType || (isObjectAssignable && !needsNewSymbol))
                return sourceSymbol;

            // We can just return the symbol as-is
            // This might need revisiting since there may be *some* case where you want the type to be explicitly casted to a specific type regardless of the source type
            if (targetType.IsAssignableFrom(sourceSymbol.symbolCsType))
                return sourceSymbol;

            // Numeric conversion handling
            MethodInfo conversionFunction = UdonSharpUtils.GetNumericConversionMethod(targetType, sourceSymbol.symbolCsType);

            if (conversionFunction != null && 
                (isExplicit || isNumericCastValid) && 
                (targetType != typeof(string) || sourceSymbol.userCsType != typeof(object))) // Convert.ToString(object) will convert null to an empty string, we do not want that.
            {
                SymbolDefinition sourceNumericSymbol = sourceSymbol;

                // System.Convert.ToIntXX with a floating point argument will not be truncated, instead it will be rounded using Banker's Rounding.
                // This is not what we want for casts, so we first floor the input before running the conversion
                if (UdonSharpUtils.IsFloatType(sourceSymbol.symbolCsType) && UdonSharpUtils.IsIntegerType(targetType))
                {
                    // Mathf.Floor only works on floats so if it's a double we need to convert it first.
                    // This does lose a small amount of accuracy on gigantic numbers, but it should hopefully be enough until Udon has dedicated cast instructions at some point in the future
                    SymbolDefinition inputFloat = CastSymbolToType(sourceSymbol, typeof(float), true);
                    conversionFunction = UdonSharpUtils.GetNumericConversionMethod(targetType, typeof(float));

                    using (ExpressionCaptureScope floatFloorMethodCaptureScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        floatFloorMethodCaptureScope.SetToMethods(new[] { typeof(Mathf).GetMethod("Floor", BindingFlags.Static | BindingFlags.Public) });
                        sourceNumericSymbol = floatFloorMethodCaptureScope.Invoke(new SymbolDefinition[] { inputFloat });
                    }
                }

                // This code is copied 3 times, todo: find a decent way to refactor it
                SymbolDefinition castOutput = requestedDestination != null ? requestedDestination : visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                visitorContext.uasmBuilder.AddPush(sourceNumericSymbol);
                visitorContext.uasmBuilder.AddPush(castOutput);
                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(conversionFunction));

                return castOutput;
            }

            // Implicit user-defined conversion handling
            List<System.Type> operatorTypes = new List<System.Type>();
            operatorTypes.Add(targetType);

            System.Type currentSourceType = sourceSymbol.symbolCsType;
            while (currentSourceType != null)
            {
                operatorTypes.Add(currentSourceType);
                currentSourceType = currentSourceType.BaseType;
            }

            MethodInfo foundConversion = null;

            foreach (System.Type operatorType in operatorTypes)
            {
                IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Implicit");

                foreach (MethodInfo methodInfo in methods)
                {
                    if (methodInfo.ReturnType == targetType && (methodInfo.GetParameters()[0].ParameterType == sourceSymbol.symbolCsType || methodInfo.GetParameters()[0].ParameterType == typeof(UnityEngine.Object)))
                    {
                        foundConversion = methodInfo;
                        break;
                    }
                }

                if (foundConversion != null)
                    break;
            }

            if (foundConversion != null)
            {
                SymbolDefinition castOutput = requestedDestination != null ? requestedDestination : visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                visitorContext.uasmBuilder.AddPush(sourceSymbol);
                visitorContext.uasmBuilder.AddPush(castOutput);
                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(foundConversion));

                return castOutput;
            }

            // Explicit user-defined conversion handling
            if (isExplicit)
            {
                foreach (System.Type operatorType in operatorTypes)
                {
                    IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Explicit");

                    foreach (MethodInfo methodInfo in methods)
                    {
                        if (methodInfo.ReturnType == targetType && methodInfo.GetParameters()[0].ParameterType == sourceSymbol.symbolCsType)
                        {
                            foundConversion = methodInfo;
                            break;
                        }
                    }

                    if (foundConversion != null)
                        break;
                }

                if (foundConversion != null)
                {
                    SymbolDefinition castOutput = requestedDestination != null ? requestedDestination : visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                    visitorContext.uasmBuilder.AddPush(sourceSymbol);
                    visitorContext.uasmBuilder.AddPush(castOutput);
                    visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(foundConversion));

                    return castOutput;
                }

                // udon-workaround: Int to enum cast
                if (UdonSharpUtils.IsIntegerType(sourceSymbol.symbolCsType) && targetType.IsEnum)
                {
                    SymbolDefinition enumArraySymbol = GetEnumArrayForType(targetType);

                    SymbolDefinition indexSymbol = CastSymbolToType(sourceSymbol, typeof(int), true);
                    
                    SymbolDefinition castOutput = requestedDestination != null ? requestedDestination : visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                    string objArrayGetMethod = visitorContext.resolverContext.GetUdonMethodName(typeof(object[]).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Get"));

                    visitorContext.uasmBuilder.AddPush(enumArraySymbol);
                    visitorContext.uasmBuilder.AddPush(indexSymbol);
                    visitorContext.uasmBuilder.AddPush(castOutput);
                    visitorContext.uasmBuilder.AddExternCall(objArrayGetMethod);

                    return castOutput;
                }

                // All other casts have failed, just try to straight assign it to a new symbol
                if (needsNewSymbol)
                {
                    SymbolDefinition copyCastOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);
                    visitorContext.uasmBuilder.AddCopy(copyCastOutput, sourceSymbol);
                    return copyCastOutput;
                }
                else
                {
                    // Copying to an invalid type won't throw exceptions sadly so just return the symbol...
                    return sourceSymbol;
                }
            }

            throw new System.Exception($"Cannot find cast for {sourceSymbol.symbolCsType} to {targetType}");
        }

        /// <summary>
        /// Runs implicit conversions on method arguments, creates symbols for default values, and handles conversion of parameters for a `params` argument.
        /// </summary>
        /// <param name="targetMethod"></param>
        /// <param name="invokeParams"></param>
        /// <returns></returns>
        private SymbolDefinition[] GetExpandedInvokeParams(MethodBase targetMethod, SymbolDefinition[] invokeParams)
        {
            ParameterInfo[] methodParams = targetMethod.GetParameters();

            List<SymbolDefinition> newInvokeParams = new List<SymbolDefinition>();
            SymbolDefinition[] argDestinationSymbols = GetLocalMethodArgumentSymbols();
            if (argDestinationSymbols == null)
            {
                argDestinationSymbols = new SymbolDefinition[methodParams.Length];
            }

            for (int i = 0; i < methodParams.Length; ++i)
            {
                // Handle default args
                if (invokeParams.Length <= i)
                {
                    if (!methodParams[i].HasDefaultValue)
                        throw new System.Exception("Overran valid parameters without default default value to use");

                    SymbolDefinition defaultArg = visitorContext.topTable.CreateConstSymbol(methodParams[i].ParameterType, methodParams[i].DefaultValue);

                    newInvokeParams.Add(defaultArg);
                    continue;
                }

                // If this method has a params array then we need to take the remaining invoke parameters and make an array to use as input
                if (methodParams[i].HasParamsParameter())
                {
                    int paramCount = invokeParams.Length - i;

                    if (paramCount == 1 && methodParams[i].ParameterType.IsImplicitlyAssignableFrom(invokeParams[i].userCsType))
                    {
                        newInvokeParams.Add(invokeParams[i]);
                    }
                    else
                    {
                        SymbolDefinition paramsArraySymbol;

                        //if (!visitorContext.isRecursiveMethod)
                        {
                            paramsArraySymbol = visitorContext.topTable.CreateConstSymbol(methodParams[i].ParameterType,
                                        System.Activator.CreateInstance(methodParams[i].ParameterType, new object[] { paramCount }));
                        }
                        //else // This isn't needed currently
                        //{
                        //    paramsArraySymbol = visitorContext.topTable.CreateUnnamedSymbol(methodParams[i].ParameterType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.NeedsRecursivePush);
                        //    using (ExpressionCaptureScope paramsArrayConstructScope = new ExpressionCaptureScope(visitorContext, null, paramsArraySymbol))
                        //    {
                        //        paramsArrayConstructScope.SetToMethods(methodParams[i].ParameterType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
                        //        paramsArraySymbol = paramsArrayConstructScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(int), paramCount) } );
                        //    }
                        //}

                        for (int j = i; j < invokeParams.Length; ++j)
                        {
                            int paramArrayIndex = j - i;

                            // This can potentially grow unbounded, but we'll hope that the user doesn't go insane with the param count
                            SymbolDefinition arrayIndexSymbol = visitorContext.topTable.CreateConstSymbol(typeof(int), paramArrayIndex);

                            using (ExpressionCaptureScope paramArraySetterScope = new ExpressionCaptureScope(visitorContext, null))
                            {
                                paramArraySetterScope.SetToLocalSymbol(paramsArraySymbol);
                                using (SymbolDefinition.COWValue arrayIndex = arrayIndexSymbol.GetCOWValue(visitorContext)) {
                                    paramArraySetterScope.HandleArrayIndexerAccess(arrayIndex);
                                }
                                paramArraySetterScope.ExecuteSet(invokeParams[j]);
                            }
                        }

                        newInvokeParams.Add(paramsArraySymbol);
                    }
                    break;
                }

                newInvokeParams.Add(CastSymbolToType(invokeParams[i], methodParams[i].ParameterType, false, false, argDestinationSymbols[i]));
            }

            return newInvokeParams.ToArray();
        }

        public MethodBase GetInvokeMethod(SymbolDefinition[] invokeParams)
        {
            return visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, invokeParams.Select(e => e.symbolCsType).ToList());
        }

        private bool AllMethodParametersMatch(MethodInfo methodInfo, System.Type[] parameterTypes)
        {
            ParameterInfo[] parameters = methodInfo.GetParameters();

            if (parameters.Length != parameterTypes.Length)
                return false;

            for (int i = 0; i < parameters.Length; ++i)
            {
                if (parameters[i].ParameterType != parameterTypes[i])
                    return false;
            }

            return true;
        }

        private MethodInfo ConvertGetComponentToGetComponents(MethodInfo targetMethod)
        {
            if (targetMethod.GetParameters().FirstOrDefault() != null && targetMethod.GetParameters().FirstOrDefault().ParameterType == typeof(System.Type))
                throw new System.ArgumentException("Cannot use GetComponent with type arguments on generic versions");

            System.Type declaringType = targetMethod.DeclaringType;

            string searchString = targetMethod.Name;
            if (!searchString.Contains("GetComponents"))
                searchString = searchString.Replace("GetComponent", "GetComponents");

            System.Type[] targetParameters = targetMethod.GetParameters().Select(e => e.ParameterType).ToArray();

            if (targetParameters.Count() == 0 || targetParameters.First() != typeof(System.Type))
            {
                targetParameters = new System.Type[] { typeof(System.Type) }.Concat(targetParameters).ToArray();
            }

            MethodInfo[] foundMethods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == searchString && AllMethodParametersMatch(e, targetParameters)).ToArray();
            
            if (foundMethods.Length != 1)
            {
                throw new System.ArgumentException($"No valid typeof override found for function {targetMethod}");
            }

            return foundMethods.First();
        }

        // Metaprogramming using expression captures, very fun. It would be cool to have a scripting thing setup to generate an AST and code from just an input string of code at some point in the future.
        // Handles getting a single user component type by looping through all components of the type UdonBehaviour and checking their internal type ID
        private SymbolDefinition HandleGenericGetComponentSingle(SymbolDefinition componentArray, System.Type udonSharpType)
        {
            SymbolDefinition resultSymbol = visitorContext.topTable.CreateUnnamedSymbol(udonSharpType, SymbolDeclTypeFlags.Internal);
            
            visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));

            using (ExpressionCaptureScope resultResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                resultResetScope.SetToLocalSymbol(resultSymbol);
                resultResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(udonSharpType, null));
            }

            SymbolDefinition componentTypeID = visitorContext.topTable.CreateConstSymbol(typeof(long), Internal.UdonSharpInternalUtility.GetTypeID(udonSharpType));

            SymbolDefinition componentCount = null;

            using (ExpressionCaptureScope lengthGetterScope = new ExpressionCaptureScope(visitorContext, null))
            {
                lengthGetterScope.SetToLocalSymbol(componentArray);
                lengthGetterScope.ResolveAccessToken("Length");

                componentCount = lengthGetterScope.ExecuteGet();
            }

            SymbolDefinition arrayIndex = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);
            using (ExpressionCaptureScope arrayIndexResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                arrayIndexResetScope.SetToLocalSymbol(arrayIndex);
                arrayIndexResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
            }

            JumpLabel loopStartJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentLoop");
            visitorContext.uasmBuilder.AddJumpLabel(loopStartJumpPoint);

            JumpLabel loopExitJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentLoopEnd");

            SymbolDefinition loopConditionSymbol = null;

            using (ExpressionCaptureScope loopConditionCheck = new ExpressionCaptureScope(visitorContext, null))
            {
                loopConditionCheck.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                loopConditionSymbol = loopConditionCheck.Invoke(new SymbolDefinition[] { arrayIndex, componentCount });
            }
            
            visitorContext.uasmBuilder.AddJumpIfFalse(loopExitJumpPoint, loopConditionSymbol);


            SymbolDefinition componentValue = null;

            using (ExpressionCaptureScope componentArrayGetter = new ExpressionCaptureScope(visitorContext, null))
            {
                componentArrayGetter.SetToLocalSymbol(componentArray);
                using (SymbolDefinition.COWValue arrayIndexValue = arrayIndex.GetCOWValue(visitorContext))
                {
                    componentArrayGetter.HandleArrayIndexerAccess(arrayIndexValue);
                }
                
                componentValue = CastSymbolToType(componentArrayGetter.ExecuteGet(), udonSharpType, true, true);
            }

            SymbolDefinition objectTypeId = null;

            using (ExpressionCaptureScope typeIDGetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                typeIDGetScope.SetToLocalSymbol(componentValue);
                typeIDGetScope.ResolveAccessToken(nameof(UdonSharpBehaviour.GetUdonTypeID));

                objectTypeId = typeIDGetScope.Invoke(new SymbolDefinition[] { });
            }

            SymbolDefinition typeIdEqualsConditionSymbol = null;

            using (ExpressionCaptureScope equalsConditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                equalsConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(long), BuiltinOperatorType.Equality));
                typeIdEqualsConditionSymbol = equalsConditionScope.Invoke(new SymbolDefinition[] { objectTypeId, componentTypeID });
            }

            JumpLabel conditionFalseJumpLoc = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentIdNotEqual");

            visitorContext.uasmBuilder.AddJumpIfFalse(conditionFalseJumpLoc, typeIdEqualsConditionSymbol);

            using (ExpressionCaptureScope setResultScope = new ExpressionCaptureScope(visitorContext, null))
            {
                setResultScope.SetToLocalSymbol(resultSymbol);
                setResultScope.ExecuteSet(componentValue);
            }

            visitorContext.uasmBuilder.AddJump(loopExitJumpPoint); // Exit early

            visitorContext.uasmBuilder.AddJumpLabel(conditionFalseJumpLoc);

            using (ExpressionCaptureScope indexIncrementScope = new ExpressionCaptureScope(visitorContext, null))
            {
                indexIncrementScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                SymbolDefinition incrementResult = indexIncrementScope.Invoke(new SymbolDefinition[] { arrayIndex, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                visitorContext.uasmBuilder.AddCopy(arrayIndex, incrementResult);
            }

            visitorContext.uasmBuilder.AddJump(loopStartJumpPoint);

            visitorContext.uasmBuilder.AddJumpLabel(loopExitJumpPoint);

            visitorContext.PopTable();

            return resultSymbol;
        }

        // Handles getting an array of user components, first iterates the array of components to count how many of them are the given user type, then creates an array of that size, 
        //   then iterates the array again and assigns the components to the correct index in the array
        private SymbolDefinition HandleGenericGetComponentArray(SymbolDefinition componentArray, System.Type udonSharpType)
        {
            SymbolDefinition resultSymbol = visitorContext.topTable.CreateUnnamedSymbol(udonSharpType.MakeArrayType(), SymbolDeclTypeFlags.Internal);

            visitorContext.PushTable(new SymbolTable(visitorContext.resolverContext, visitorContext.topTable));

            using (ExpressionCaptureScope resultResetScope = new ExpressionCaptureScope(visitorContext, null))
            {
                resultResetScope.SetToLocalSymbol(resultSymbol);
                SymbolDefinition arrayConstSymbol = visitorContext.topTable.CreateConstSymbol(udonSharpType.MakeArrayType(), null);
                arrayConstSymbol.symbolDefaultValue = new Component[0];
                resultResetScope.ExecuteSet(arrayConstSymbol);
            }

            SymbolDefinition componentTypeID = visitorContext.topTable.CreateConstSymbol(typeof(long), Internal.UdonSharpInternalUtility.GetTypeID(udonSharpType));
            
            SymbolDefinition componentCounterSymbol = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);

            using (ExpressionCaptureScope resetComponentCountScope = new ExpressionCaptureScope(visitorContext, null))
            {
                resetComponentCountScope.SetToLocalSymbol(componentCounterSymbol);
                resetComponentCountScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
            }

            SymbolDefinition componentCount = null;

            using (ExpressionCaptureScope lengthGetterScope = new ExpressionCaptureScope(visitorContext, null))
            {
                lengthGetterScope.SetToLocalSymbol(componentArray);
                lengthGetterScope.ResolveAccessToken("Length");

                componentCount = lengthGetterScope.ExecuteGet();
            }

            SymbolDefinition arrayIndex = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);

            // First loop to count the number of components
            {
                using (ExpressionCaptureScope arrayIndexResetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayIndexResetScope.SetToLocalSymbol(arrayIndex);
                    arrayIndexResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
                }

                JumpLabel loopStartJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoop");
                visitorContext.uasmBuilder.AddJumpLabel(loopStartJumpPoint);

                JumpLabel loopExitJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoopEnd");

                SymbolDefinition loopConditionSymbol = null;

                using (ExpressionCaptureScope loopConditionCheck = new ExpressionCaptureScope(visitorContext, null))
                {
                    loopConditionCheck.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                    loopConditionSymbol = loopConditionCheck.Invoke(new SymbolDefinition[] { arrayIndex, componentCount });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(loopExitJumpPoint, loopConditionSymbol);

                SymbolDefinition componentValue = null;

                using (ExpressionCaptureScope componentArrayGetter = new ExpressionCaptureScope(visitorContext, null))
                {
                    componentArrayGetter.SetToLocalSymbol(componentArray);
                    using (SymbolDefinition.COWValue arrayIndexValue = arrayIndex.GetCOWValue(visitorContext))
                    {
                        componentArrayGetter.HandleArrayIndexerAccess(arrayIndexValue);
                    }

                    componentValue = CastSymbolToType(componentArrayGetter.ExecuteGet(), udonSharpType, true, true);
                }

                SymbolDefinition objectTypeId = null;

                using (ExpressionCaptureScope typeIDGetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    typeIDGetScope.SetToLocalSymbol(componentValue);
                    typeIDGetScope.ResolveAccessToken(nameof(UdonSharpBehaviour.GetUdonTypeID));

                    objectTypeId = typeIDGetScope.Invoke(new SymbolDefinition[] { });
                }

                JumpLabel incrementConditionFalseJump = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsIncrementConditionFalse");

                SymbolDefinition incrementConditionSymbol = null;

                using (ExpressionCaptureScope incrementConditionScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    incrementConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(long), BuiltinOperatorType.Equality));
                    incrementConditionSymbol = incrementConditionScope.Invoke(new SymbolDefinition[] { objectTypeId, componentTypeID });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(incrementConditionFalseJump, incrementConditionSymbol);

                using (ExpressionCaptureScope incrementComponentCountScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    incrementComponentCountScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = incrementComponentCountScope.Invoke(new SymbolDefinition[] { componentCounterSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(componentCounterSymbol, incrementResult);
                }

                visitorContext.uasmBuilder.AddJumpLabel(incrementConditionFalseJump);


                using (ExpressionCaptureScope indexIncrementScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    indexIncrementScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = indexIncrementScope.Invoke(new SymbolDefinition[] { arrayIndex, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(arrayIndex, incrementResult);
                }

                visitorContext.uasmBuilder.AddJump(loopStartJumpPoint);

                visitorContext.uasmBuilder.AddJumpLabel(loopExitJumpPoint);
            }

            // Skip the second loop if we found no valid components
            JumpLabel exitJumpLabel = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsSkipArrayIteration");
            SymbolDefinition skipSecondLoopConditionSymbol = null;
            using (ExpressionCaptureScope skipSecondLoopConditionScope = new ExpressionCaptureScope(visitorContext, null))
            {
                skipSecondLoopConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.GreaterThan));
                skipSecondLoopConditionSymbol = skipSecondLoopConditionScope.Invoke(new SymbolDefinition[] { componentCounterSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 0) });
            }

            visitorContext.uasmBuilder.AddJumpIfFalse(exitJumpLabel, skipSecondLoopConditionSymbol);

            // Second loop to assign values to the array
            {
                // Initialize the new array
                using (ExpressionCaptureScope arrayVarScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayVarScope.SetToLocalSymbol(resultSymbol);

                    using (ExpressionCaptureScope arrayCreationScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        arrayCreationScope.SetToMethods(resultSymbol.symbolCsType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

                        SymbolDefinition newArraySymbol = arrayCreationScope.Invoke(new SymbolDefinition[] { componentCounterSymbol });
                        if (resultSymbol.IsUserDefinedType())
                            newArraySymbol.symbolCsType = resultSymbol.userCsType;

                        arrayVarScope.ExecuteSet(newArraySymbol);
                    }
                }

                using (ExpressionCaptureScope arrayIndexResetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    arrayIndexResetScope.SetToLocalSymbol(arrayIndex);
                    arrayIndexResetScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
                }

                SymbolDefinition destIdxSymbol = visitorContext.topTable.CreateUnnamedSymbol(typeof(int), SymbolDeclTypeFlags.Internal);

                using (ExpressionCaptureScope resetDestIdxScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    resetDestIdxScope.SetToLocalSymbol(destIdxSymbol);
                    resetDestIdxScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(int), 0));
                }

                JumpLabel loopStartJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoop2");
                visitorContext.uasmBuilder.AddJumpLabel(loopStartJumpPoint);

                JumpLabel loopExitJumpPoint = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsLoopEnd2");

                SymbolDefinition loopConditionSymbol = null;

                using (ExpressionCaptureScope loopConditionCheck = new ExpressionCaptureScope(visitorContext, null))
                {
                    loopConditionCheck.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.LessThan));
                    loopConditionSymbol = loopConditionCheck.Invoke(new SymbolDefinition[] { arrayIndex, componentCount });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(loopExitJumpPoint, loopConditionSymbol);

                SymbolDefinition componentValue = null;
                
                using (ExpressionCaptureScope componentArrayGetter = new ExpressionCaptureScope(visitorContext, null))
                {
                    componentArrayGetter.SetToLocalSymbol(componentArray);
                    using (SymbolDefinition.COWValue arrayIndexValue = arrayIndex.GetCOWValue(visitorContext))
                    {
                        componentArrayGetter.HandleArrayIndexerAccess(arrayIndexValue);
                    }

                    componentValue = CastSymbolToType(componentArrayGetter.ExecuteGet(), udonSharpType, true, true);
                }

                SymbolDefinition objectTypeId = null;

                using (ExpressionCaptureScope typeIDGetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    typeIDGetScope.SetToLocalSymbol(componentValue);
                    typeIDGetScope.ResolveAccessToken(nameof(UdonSharpBehaviour.GetUdonTypeID));

                    objectTypeId = typeIDGetScope.Invoke(new SymbolDefinition[] { });
                }

                JumpLabel addConditionFalseJump = visitorContext.labelTable.GetNewJumpLabel("genericGetUserComponentsAddConditionFalse");

                SymbolDefinition addConditionSymbol = null;

                using (ExpressionCaptureScope addConditionScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    addConditionScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(long), BuiltinOperatorType.Equality));
                    addConditionSymbol = addConditionScope.Invoke(new SymbolDefinition[] { objectTypeId, componentTypeID });
                }

                visitorContext.uasmBuilder.AddJumpIfFalse(addConditionFalseJump, addConditionSymbol);

                using (ExpressionCaptureScope setArrayValueScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    setArrayValueScope.SetToLocalSymbol(resultSymbol);
                    using (SymbolDefinition.COWValue destIdxValue = destIdxSymbol.GetCOWValue(visitorContext))
                    {
                        setArrayValueScope.HandleArrayIndexerAccess(destIdxValue);
                    }

                    using (ExpressionCaptureScope sourceValueGetScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        sourceValueGetScope.SetToLocalSymbol(componentArray);
                        using (SymbolDefinition.COWValue arrayIndexValue = arrayIndex.GetCOWValue(visitorContext)) {
                            sourceValueGetScope.HandleArrayIndexerAccess(arrayIndexValue);
                        }

                        SymbolDefinition arrayValue = sourceValueGetScope.ExecuteGet();
                        arrayValue.symbolCsType = udonSharpType;
                        setArrayValueScope.ExecuteSet(arrayValue);
                    }
                }

                using (ExpressionCaptureScope incrementTargetCountScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    incrementTargetCountScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = incrementTargetCountScope.Invoke(new SymbolDefinition[] { destIdxSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(destIdxSymbol, incrementResult);
                }

                visitorContext.uasmBuilder.AddJumpLabel(addConditionFalseJump);

                using (ExpressionCaptureScope indexIncrementScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    indexIncrementScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementResult = indexIncrementScope.Invoke(new SymbolDefinition[] { arrayIndex, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });
                    visitorContext.uasmBuilder.AddCopy(arrayIndex, incrementResult);
                }

                visitorContext.uasmBuilder.AddJump(loopStartJumpPoint);

                visitorContext.uasmBuilder.AddJumpLabel(loopExitJumpPoint);
            }

            visitorContext.uasmBuilder.AddJumpLabel(exitJumpLabel);

            visitorContext.PopTable();

            return resultSymbol;
        }

        private SymbolDefinition HandleGenericUSharpGetComponent(MethodInfo targetMethod, System.Type udonSharpType, SymbolDefinition[] invokeParams)
        {
            bool isArray = targetMethod.ReturnType.IsArray;

            targetMethod = ConvertGetComponentToGetComponents(targetMethod);

            SymbolDefinition udonBehaviourType = visitorContext.topTable.CreateConstSymbol(typeof(System.Type), typeof(VRC.Udon.UdonBehaviour));

            visitorContext.uasmBuilder.AddPush(udonBehaviourType);

            foreach (SymbolDefinition invokeParam in invokeParams)
                visitorContext.uasmBuilder.AddPush(invokeParam);

            SymbolDefinition resultComponents = visitorContext.topTable.CreateUnnamedSymbol(typeof(Component[]), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);

            visitorContext.uasmBuilder.AddPush(resultComponents);

            visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(targetMethod));

            if (isArray)
            {
                return HandleGenericGetComponentArray(resultComponents, udonSharpType);
            }
            else
            {
                return HandleGenericGetComponentSingle(resultComponents, udonSharpType);
            }
        }

        SymbolDefinition[] BuildSymbolPushList(IEnumerable<SymbolDefinition> extraParamsToPush, bool includeRecursiveSymbols = true)
        {
            HashSet<SymbolDefinition> definitionSet;
            if (includeRecursiveSymbols)
            {
                definitionSet = new HashSet<SymbolDefinition>(visitorContext.topTable.GetAllRecursiveSymbols());
                definitionSet.UnionWith(visitorContext.topTable.GetOpenCOWSymbols());
            }
            else
            {
                definitionSet = new HashSet<SymbolDefinition>();
            }

            if (extraParamsToPush != null)
                definitionSet.UnionWith(extraParamsToPush);

            return definitionSet.ToArray();
        }

        private void PushRecursiveStack(SymbolDefinition[] pushSymbols, ref SymbolDefinition checkSizeSymbol, bool checkStackSize = true)
        {
            if (checkSizeSymbol == null)
                checkSizeSymbol = visitorContext.topTable.CreateNamedSymbol("usharpStackReservation", typeof(int), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant);

            if (pushSymbols.Length == 0)
                return;

            // Set max so we can init the stack properly as a constant
            visitorContext.maxMethodFrameSize = Mathf.Max(pushSymbols.Length, visitorContext.maxMethodFrameSize);

            if (checkStackSize)
            {
                visitorContext.uasmBuilder.AppendCommentedLine("", "");
                visitorContext.uasmBuilder.AppendCommentedLine("", "Stack size check");

                // First check stack size, if it's too small, double the stack size and copy it over
                SymbolDefinition stackSize;

                using (ExpressionCaptureScope stackSizeCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    stackSizeCapture.SetToLocalSymbol(visitorContext.artificalStackSymbol);
                    stackSizeCapture.ResolveAccessToken("Length");
                    stackSize = stackSizeCapture.ExecuteGet();
                }

                SymbolDefinition targetStackSizeSymbol;
                using (ExpressionCaptureScope targetSizeAddCapture = new ExpressionCaptureScope(visitorContext, null))
                {
                    targetSizeAddCapture.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    targetStackSizeSymbol = targetSizeAddCapture.Invoke(new SymbolDefinition[] { checkSizeSymbol, visitorContext.stackAddressSymbol });
                }

                SymbolDefinition isGreaterThanCondition;
                using (ExpressionCaptureScope greaterThanCompare = new ExpressionCaptureScope(visitorContext, null))
                {
                    greaterThanCompare.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.GreaterThanOrEqual));
                    isGreaterThanCondition = greaterThanCompare.Invoke(new SymbolDefinition[] { targetStackSizeSymbol, stackSize });
                }

                JumpLabel skipResizeLabel = visitorContext.labelTable.GetNewJumpLabel("resizeRecusiveStackSkip");

                visitorContext.uasmBuilder.AddJumpIfFalse(skipResizeLabel, isGreaterThanCondition);

                // Handle the resize & copy
                SymbolDefinition newStackSizeSymbol;
                using (ExpressionCaptureScope stackDoubleScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    stackDoubleScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Multiplication));
                    newStackSizeSymbol = stackDoubleScope.Invoke(new SymbolDefinition[] { stackSize, visitorContext.topTable.CreateConstSymbol(typeof(int), 2) });
                }

                // Construct new stack
                SymbolDefinition newStackSymbol;
                using (ExpressionCaptureScope stackCreationScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    stackCreationScope.SetToMethods(typeof(object[]).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
                    newStackSymbol = stackCreationScope.Invoke(new SymbolDefinition[] { newStackSizeSymbol });
                }

                object[] myArr = new object[4];

                // Copy old stack to new one
                using (ExpressionCaptureScope copyScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    copyScope.SetToLocalSymbol(visitorContext.artificalStackSymbol);
                    copyScope.ResolveAccessToken("CopyTo");
                    copyScope.Invoke(new SymbolDefinition[] { newStackSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 0) });
                }

                // Now finally copy over the old stack reference
                visitorContext.uasmBuilder.AddCopy(visitorContext.artificalStackSymbol, newStackSymbol);

                visitorContext.uasmBuilder.AddJumpLabel(skipResizeLabel);
            }

            visitorContext.uasmBuilder.AppendCommentedLine("", "");
            visitorContext.uasmBuilder.AppendCommentedLine("", "Start push recursive fields");

            // Now we start pushing to the stack
            for (int i = 0; i < pushSymbols.Length; ++i)
            {
                using (ExpressionCaptureScope symbolSetScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    symbolSetScope.SetToLocalSymbol(visitorContext.artificalStackSymbol);
                    SymbolDefinition.COWValue indexerCOW = visitorContext.stackAddressSymbol.GetCOWValue(visitorContext);

                    symbolSetScope.HandleArrayIndexerAccess(indexerCOW);
                    symbolSetScope.ExecuteSet(pushSymbols[i]);

                    indexerCOW.Dispose();
                }

                // Increment address
                using (ExpressionCaptureScope incrementAddressScope = new ExpressionCaptureScope(visitorContext, null, visitorContext.stackAddressSymbol))
                {
                    incrementAddressScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Addition));
                    SymbolDefinition incrementedVal = incrementAddressScope.Invoke(new SymbolDefinition[] { visitorContext.stackAddressSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });

                    // This should be a NOP always, but is here in case the optimized direct call breaks
                    if (incrementedVal != visitorContext.stackAddressSymbol)
                    {
                        Debug.LogWarning($"Copy elision failed on symbol '{pushSymbols[i].ToString()}' during recursion handling");
                        visitorContext.uasmBuilder.AddCopy(visitorContext.stackAddressSymbol, incrementedVal);
                    }
                }
            }

            visitorContext.uasmBuilder.AppendCommentedLine("", "End push recursive fields");
            visitorContext.uasmBuilder.AppendCommentedLine("", "");
        }

        private void PopRecursiveStack(SymbolDefinition[] popSymbols)
        {
            if (popSymbols == null || popSymbols.Length == 0)
                return;

            visitorContext.uasmBuilder.AppendCommentedLine("", "");
            visitorContext.uasmBuilder.AppendCommentedLine("", "Start pop recursive fields");

            // Pop symbols off the stack in reverse order
            for (int i = popSymbols.Length - 1; i >= 0; --i)
            {
                // Decrement address
                using (ExpressionCaptureScope decrementAddressScope = new ExpressionCaptureScope(visitorContext, null, visitorContext.stackAddressSymbol))
                {
                    decrementAddressScope.SetToMethods(UdonSharpUtils.GetOperators(typeof(int), BuiltinOperatorType.Subtraction));
                    SymbolDefinition incrementedVal = decrementAddressScope.Invoke(new SymbolDefinition[] { visitorContext.stackAddressSymbol, visitorContext.topTable.CreateConstSymbol(typeof(int), 1) });

                    // This should be a NOP always, but is here in case the optimized direct call breaks
                    if (incrementedVal != visitorContext.stackAddressSymbol)
                    {
                        Debug.LogWarning($"Copy elision failed on symbol '{popSymbols[i].ToString()}' during recursion handling");
                        visitorContext.uasmBuilder.AddCopy(visitorContext.stackAddressSymbol, incrementedVal);
                    }
                }

                SymbolDefinition.COWValue paramCOWVal = popSymbols[i].GetCOWValue(visitorContext);

                // Manually write this out to allow copy elision on non-compatible types
                visitorContext.uasmBuilder.AddPush(visitorContext.artificalStackSymbol);
                visitorContext.uasmBuilder.AddPush(visitorContext.stackAddressSymbol);
                visitorContext.uasmBuilder.AddPush(paramCOWVal.symbol);
                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(typeof(object[]).GetMethods(BindingFlags.Public | BindingFlags.Instance).First(e => e.Name == "Get")));

                paramCOWVal.Dispose();
            }

            visitorContext.uasmBuilder.AppendCommentedLine("", "End pop recursive fields");
            visitorContext.uasmBuilder.AppendCommentedLine("", "");
        }

        private static readonly HashSet<System.Type> _brokenGetComponentTypes = new HashSet<System.Type>()
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
        };

        private SymbolDefinition InvokeExtern(SymbolDefinition[] invokeParams)
        {
            // We use void as a placeholder for a null constant value getting passed in, if null is passed in and the target type is a reference type then we assume they are compatible
            List<System.Type> typeList = invokeParams.Select(e =>
            {
                if (e.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) &&
                    e.symbolCsType == typeof(object) &&
                    e.symbolDefaultValue == null)
                    return typeof(void);

                return e.symbolCsType;
            }).ToList();

            // Find valid overrides
            MethodBase targetMethod = visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, typeList);

            if (targetMethod == null)
            {
                targetMethod = visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, typeList, false);

                if (targetMethod != null &&
                    targetMethod.ReflectedType == typeof(VRC.Udon.UdonBehaviour) &&
                    targetMethod.Name.StartsWith("GetComponent") &&
                    ((MethodInfo)targetMethod).ReturnType.IsGenericParameter)
                {
                    // Uhh just skip the else stuff, this fixes GetComponent(s) on UdonBehaviour variables.
                }
                else
                {
                    if (targetMethod != null)
                    {
                        throw new System.Exception($"Method is not exposed to Udon: {targetMethod}, Udon signature: {visitorContext.resolverContext.GetUdonMethodName(targetMethod, false)}");
                    }

                    string udonFilteredMethods = "";

                    udonFilteredMethods = string.Join("\n", captureMethods
                        .Select(e => new System.Tuple<MethodBase, string>(e, visitorContext.resolverContext.GetUdonMethodName(e, false)))
                        .Where(e => !visitorContext.resolverContext.IsValidUdonMethod(e.Item2))
                        .Select(e => e.Item1));

                    if (udonFilteredMethods.Length > 0)
                    {
                        throw new System.Exception($"Could not find valid method that exists in Udon.\nList of applicable methods that do not exist:\n{udonFilteredMethods}");
                    }
                    else
                    {
                        throw new System.Exception("Could not find valid method for given parameters!");
                    }
                }
            }

            SymbolDefinition[] expandedParams = GetExpandedInvokeParams(targetMethod, invokeParams);
            bool isGetComponent = targetMethod.Name.StartsWith("GetComponent") && genericTypeArguments != null;
            bool isUserTypeGetComponent = isGetComponent && genericTypeArguments.First().IsSubclassOf(typeof(UdonSharpBehaviour));

            if (isGetComponent && _brokenGetComponentTypes.Contains(genericTypeArguments.First()))
                throw new System.Exception($"{targetMethod.Name}<T>() is currently broken in Udon for SDK3 components (<b><i> https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/getcomponentst-functions-are-not-defined-internally-for-vrcsdk3-components </i></b>)\n" +
                    $"Until this is fixed by VRC, try using: <b>((T){targetMethod.Name}(typeof(T)))</b> instead of <b>{targetMethod.Name}<T>()</b>");

            // Now make the needed symbol definitions and run the invoke
            if (!targetMethod.IsStatic && !(targetMethod is ConstructorInfo)/* && targetMethod.Name != "Instantiate"*/) // Constructors don't take an instance argument, but are still classified as an instance method
            {
                if (genericTypeArguments != null && typeof(GameObject).IsAssignableFrom(accessSymbol.symbolCsType) && !isUserTypeGetComponent) // Handle GetComponent<T> on gameobjects by getting their transform first
                {
                    using (ExpressionCaptureScope transformComponentGetScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        transformComponentGetScope.SetToLocalSymbol(accessSymbol);
                        transformComponentGetScope.ResolveAccessToken("transform");

                        visitorContext.uasmBuilder.AddPush(transformComponentGetScope.ExecuteGet());
                    }
                }
                else if (isGetComponent && !isUserTypeGetComponent)
                {
                    // udon-workaround: Works around a bug in Udon's GetComponent methods that require a variable with the **StrongBox** type of Transform or GameObject, instead of the actual variable type
                    // This means that if the strongbox of the variable for the object we're getting changes, then GetComponent will start failing

                    MethodInfo getTransformMethod = typeof(Component).GetProperty("transform", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();

                    SymbolDefinition outputTransformComponent = visitorContext.topTable.CreateUnnamedSymbol(typeof(Transform), SymbolDeclTypeFlags.Internal);

                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                    visitorContext.uasmBuilder.AddPush(outputTransformComponent);
                    visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(getTransformMethod), "GetComponent strongbox mismatch fix");

                    visitorContext.uasmBuilder.AddPush(outputTransformComponent);
                }
                else
                { 
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }
            }

            bool isPotentiallyRecursive = false;

            if (accessSymbol != null && (typeof(UdonSharpBehaviour).IsAssignableFrom(accessSymbol.symbolCsType) || typeof(VRC.Udon.UdonBehaviour).IsAssignableFrom(accessSymbol.symbolCsType)))
            {
                switch (targetMethod.Name)
                {
                    case "RunProgram":
                    case "SendCustomEvent":
                    case "SendCustomNetworkEvent":
                        // We might be recursing back into the same UdonBehavior, assume any non-local fields might be modified.
                        visitorContext.topTable.DirtyEverything(true);
                        isPotentiallyRecursive = visitorContext.isRecursiveMethod;
                        break;
                    default:
                        break;
                }
            }

            SymbolDefinition returnSymbol = null;

            System.Type returnType = typeof(void);

            if (targetMethod is MethodInfo)
                returnType = ((MethodInfo)targetMethod).ReturnType;
            else if (targetMethod is ConstructorInfo)
                returnType = targetMethod.DeclaringType;
            else
                throw new System.Exception("Invalid target method type");

            if (isUserTypeGetComponent)
            {
                returnSymbol = HandleGenericUSharpGetComponent(targetMethod as MethodInfo, genericTypeArguments.First(), invokeParams);
            }
            else
            {
                SymbolDefinition[] symbolsToPush = null;
                if (isPotentiallyRecursive && !shouldSkipRecursivePush)
                {
                    symbolsToPush = BuildSymbolPushList(expandedParams);
                    SymbolDefinition sizeSymbol = null;
                    PushRecursiveStack(symbolsToPush, ref sizeSymbol);
                    sizeSymbol.symbolDefaultValue = symbolsToPush.Length;
                }

                foreach (SymbolDefinition invokeParam in expandedParams)
                    visitorContext.uasmBuilder.AddPush(invokeParam);

                if (returnType != typeof(void))
                {
                    if (genericTypeArguments != null)
                    {
                        System.Type genericType = genericTypeArguments.First();

                        if (returnType.IsArray)
                            returnType = genericType.MakeArrayType();
                        else
                            returnType = genericType;

                        visitorContext.uasmBuilder.AddPush(visitorContext.topTable.CreateConstSymbol(typeof(System.Type), genericType));
                    }

                    returnSymbol = AllocateOutputSymbol(returnType);

                    visitorContext.uasmBuilder.AddPush(returnSymbol);
                }

                visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(targetMethod, true, genericTypeArguments));

                if (isPotentiallyRecursive && !shouldSkipRecursivePush)
                    PopRecursiveStack(symbolsToPush);
            }

            return returnSymbol;
        }

        public SymbolDefinition[] GetLocalMethodArgumentSymbols()
        {
            if (captureArchetype != ExpressionCaptureArchetype.LocalMethod)
            {
                return null;
            }

            return captureLocalMethod.parameters.Select((param) => param.paramSymbol).ToArray();
        }

        private SymbolDefinition InvokeLocalMethod(SymbolDefinition[] invokeParams)
        {
            if (invokeParams.Length != captureLocalMethod.parameters.Length)
                throw new System.NotSupportedException("UdonSharp custom methods currently do not support default arguments or params arguments");
            
            SymbolDefinition[] symbolsToPush = null;

            SymbolDefinition stackSizeSymbol = null;
            if (visitorContext.isRecursiveMethod)
            {
                symbolsToPush = BuildSymbolPushList(GetLocalMethodArgumentSymbols(), false);
                PushRecursiveStack(symbolsToPush, ref stackSizeSymbol);

                // Prevents situations where you call a method like void DoThing(string a, string b) with DoThing(b, a)
                // Without COW values this would mean you copy b -> a, then you copy a -> b after you've already written over a so both parameters end with b's value
                SymbolDefinition.COWValue[] paramCOWValues = new SymbolDefinition.COWValue[invokeParams.Length];
                for (int i = 0; i < invokeParams.Length; ++i)
                    paramCOWValues[i] = invokeParams[i].GetCOWValue(visitorContext);

                for (int i = 0; i < captureLocalMethod.parameters.Length; ++i)
                {
                    using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        argAssignmentScope.SetToLocalSymbol(captureLocalMethod.parameters[i].paramSymbol);
                        argAssignmentScope.ExecuteSet(paramCOWValues[i].symbol);
                    }
                }

                foreach (SymbolDefinition.COWValue cow in paramCOWValues)
                    cow.Dispose();
            }
            else
            {
                for (int i = 0; i < captureLocalMethod.parameters.Length; ++i)
                {
                    using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        argAssignmentScope.SetToLocalSymbol(captureLocalMethod.parameters[i].paramSymbol);
                        argAssignmentScope.ExecuteSet(invokeParams[i]);
                    }
                }
            }

            // Capture any COW'd values here in case they're modified during the function.
            // TODO: Keep local variables as-is?
            visitorContext.topTable.DirtyEverything(true);

            SymbolDefinition[] cowSymbolPush = null;
            if (visitorContext.isRecursiveMethod)
            {
                HashSet<SymbolDefinition> newCOWSymbolsToPush = new HashSet<SymbolDefinition>(BuildSymbolPushList(null));
                newCOWSymbolsToPush.ExceptWith(symbolsToPush);

                cowSymbolPush = newCOWSymbolsToPush.ToArray();
                
                PushRecursiveStack(cowSymbolPush, ref stackSizeSymbol, false);

                int symbolCount = symbolsToPush.Length + cowSymbolPush.Length;
                stackSizeSymbol.symbolDefaultValue = symbolCount;

                visitorContext.maxMethodFrameSize = Mathf.Max(symbolCount, visitorContext.maxMethodFrameSize);
            }

            SymbolDefinition exitJumpLocation = visitorContext.topTable.CreateNamedSymbol("exitJumpLoc", typeof(uint), SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant);

            visitorContext.uasmBuilder.AddPush(exitJumpLocation);
            visitorContext.uasmBuilder.AddJump(captureLocalMethod.methodUserCallStart);

            JumpLabel exitLabel = visitorContext.labelTable.GetNewJumpLabel("returnLocation");

            // Now we can set this value after we have found the exit address
            visitorContext.uasmBuilder.AddJumpLabel(exitLabel);
            exitJumpLocation.symbolDefaultValue = exitLabel.resolvedAddress;

            SymbolDefinition returnSymbol = captureLocalMethod.returnSymbol;

            if (visitorContext.isRecursiveMethod)
            {
                if (returnSymbol != null)
                {
                    SymbolDefinition returnCopy = visitorContext.topTable.CreateUnnamedSymbol(returnSymbol.userCsType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.NeedsRecursivePush);
                    visitorContext.uasmBuilder.AddCopy(returnCopy, returnSymbol);
                    returnSymbol = returnCopy;
                }

                PopRecursiveStack(cowSymbolPush);
                PopRecursiveStack(symbolsToPush);
            }

            return returnSymbol;
        }

        private SymbolDefinition InvokeUserExtern(SymbolDefinition[] invokeParams)
        {
            if (invokeParams.Length != captureExternUserMethod.parameters.Length)
                throw new System.NotSupportedException("UdonSharp custom methods currently do not support default arguments or params arguments");

            if (!accessSymbol.IsUserDefinedBehaviour())
                throw new System.FieldAccessException("Cannot run extern invoke on non-user symbol");
            
            SymbolDefinition[] symbolsToPush = null;
            SymbolDefinition stackSizeSymbol = null;

            // We are calling directly into our type, so we need to handle parameter values since we may be messing with our own local variables
            if (visitorContext.isRecursiveMethod && accessSymbol.userCsType == visitorContext.behaviourUserType)
            {
                symbolsToPush = BuildSymbolPushList(captureExternUserMethod.parameters.Select(e => e.paramSymbol), false);
                PushRecursiveStack(symbolsToPush, ref stackSizeSymbol);

                SymbolDefinition.COWValue[] paramCOWValues = new SymbolDefinition.COWValue[invokeParams.Length];
                for (int i = 0; i < invokeParams.Length; ++i)
                    paramCOWValues[i] = invokeParams[i].GetCOWValue(visitorContext);

                List<SymbolDefinition> parameterDefinitions = visitorContext.topTable.GetCurrentMethodParameters();
                SymbolDefinition[] mappedSymbols = new SymbolDefinition[invokeParams.Length];

                for (int i = 0; i < mappedSymbols.Length; ++i)
                {
                    mappedSymbols[i] = parameterDefinitions.FirstOrDefault(e => e.symbolUniqueName == captureExternUserMethod.parameters[i].paramSymbol.symbolUniqueName);
                }

                for (int i = 0; i < captureExternUserMethod.parameters.Length; ++i)
                {
                    mappedSymbols[i]?.MarkDirty();

                    SymbolDefinition convertedArg = CastSymbolToType(paramCOWValues[i].symbol, captureExternUserMethod.parameters[i].type, false);

                    using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        argAssignmentScope.SetToLocalSymbol(accessSymbol);
                        argAssignmentScope.ResolveAccessToken("SetProgramVariable");

                        argAssignmentScope.Invoke(new SymbolDefinition[] {
                        visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.parameters[i].paramSymbol.symbolUniqueName),
                        convertedArg
                    });
                    }
                }

                foreach (SymbolDefinition.COWValue cow in paramCOWValues)
                    cow.Dispose();
            }
            else
            {
                for (int i = 0; i < captureExternUserMethod.parameters.Length; ++i)
                {
                    SymbolDefinition convertedArg = CastSymbolToType(invokeParams[i], captureExternUserMethod.parameters[i].type, false);

                    using (ExpressionCaptureScope argAssignmentScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        argAssignmentScope.SetToLocalSymbol(accessSymbol);
                        argAssignmentScope.ResolveAccessToken("SetProgramVariable");

                        argAssignmentScope.Invoke(new SymbolDefinition[] {
                        visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.parameters[i].paramSymbol.symbolUniqueName),
                        convertedArg
                    });
                    }
                }
            }

            // We might recurse back into this UdonBehavior and change locals, so capture any COW'd values here
            visitorContext.topTable.DirtyEverything(true);

            SymbolDefinition[] cowSymbolPush = null;
            if (visitorContext.isRecursiveMethod)
            {
                HashSet<SymbolDefinition> newCOWSymbolsToPush = new HashSet<SymbolDefinition>(BuildSymbolPushList(null));

                if (symbolsToPush != null)
                    newCOWSymbolsToPush.ExceptWith(symbolsToPush);

                cowSymbolPush = newCOWSymbolsToPush.ToArray();

                PushRecursiveStack(cowSymbolPush, ref stackSizeSymbol);

                int symbolCount = (symbolsToPush?.Length ?? 0) + cowSymbolPush.Length;
                stackSizeSymbol.symbolDefaultValue = symbolCount;

                visitorContext.maxMethodFrameSize = Mathf.Max(symbolCount, visitorContext.maxMethodFrameSize);
            }

            using (ExpressionCaptureScope externInvokeScope = new ExpressionCaptureScope(visitorContext, null))
            {
                externInvokeScope.shouldSkipRecursivePush = true;
                externInvokeScope.SetToLocalSymbol(accessSymbol);
                externInvokeScope.ResolveAccessToken("SendCustomEvent");
                externInvokeScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.uniqueMethodName) });
            }

            SymbolDefinition returnSymbol = null;

            if (captureExternUserMethod.returnSymbol != null)
            {
                using (ExpressionCaptureScope getReturnScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    getReturnScope.SetToLocalSymbol(accessSymbol);
                    getReturnScope.ResolveAccessToken("GetProgramVariable");
                    returnSymbol = getReturnScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), captureExternUserMethod.returnSymbol.symbolUniqueName) });
                    returnSymbol = CastSymbolToType(returnSymbol, captureExternUserMethod.returnSymbol.userCsType, true, true);
                    returnSymbol.declarationType |= SymbolDeclTypeFlags.NeedsRecursivePush;
                }

                using (ExpressionCaptureScope propagateScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
                {
                    propagateScope.SetToLocalSymbol(returnSymbol);
                }
            }

            if (visitorContext.isRecursiveMethod)
            {
                PopRecursiveStack(cowSymbolPush);
                PopRecursiveStack(symbolsToPush);
            }

            return returnSymbol;
        }

        public SymbolDefinition Invoke(SymbolDefinition[] invokeParams)
        {
            if (!IsMethod())
            {
                throw new System.Exception("You can only invoke methods!");
            }

            if (captureArchetype == ExpressionCaptureArchetype.Method)
            {
                return InvokeExtern(invokeParams);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.LocalMethod)
            {
                return InvokeLocalMethod(invokeParams);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserMethod)
            {
                return InvokeUserExtern(invokeParams);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.InternalUdonSharpMethod)
            {
                return InternalMethodHandler.Invoke(invokeParams);
            }
            else
            {
                CheckScopeValidity();
                throw new System.Exception($"Cannot call invoke on archetype {captureArchetype}");
            }
        }
        
        public System.Type GetReturnType(bool getUserType = false)
        {
            CheckScopeValidity();

            if (captureArchetype == ExpressionCaptureArchetype.Method)
                throw new System.Exception("Cannot infer return type from method without function arguments");

            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
            {
                if (getUserType)
                    return accessSymbol.userCsType;

                return accessSymbol.symbolCsType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                return captureProperty.GetGetMethod().ReturnType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.LocalProperty)
            {
                return captureLocalProperty.type;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserProperty)
            {
                return captureExternUserProperty.type;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                return captureField.FieldType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ExternUserField)
            {
                if (getUserType)
                    return captureExternUserField.fieldSymbol.userCsType;

                return captureExternUserField.fieldSymbol.symbolCsType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                SymbolDefinition arraySymbol = accessValue.symbol;

                if (arraySymbol.symbolCsType == typeof(Vector2) ||
                    arraySymbol.symbolCsType == typeof(Vector3) ||
                    arraySymbol.symbolCsType == typeof(Vector4) ||
                    arraySymbol.symbolCsType == typeof(Matrix4x4))
                    return typeof(float);

                if (!arraySymbol.symbolCsType.IsArray)
                    throw new System.Exception("Type is not an array type");

                if (getUserType)
                    return arraySymbol.userCsType.GetElementType();

                if (arraySymbol.IsUserDefinedBehaviour() && arraySymbol.userCsType.IsArray && arraySymbol.symbolCsType == typeof(Component[]))
                {
                    // Special case for arrays since the symbolCsType needs to return a Component[], but we need to get the element type of the UdonBehaviour[]
                    return typeof(VRC.Udon.UdonBehaviour);
                }

                if (arraySymbol.userCsType.GetElementType().IsArray)
                    return typeof(object[]);

                return arraySymbol.symbolCsType.GetElementType();
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Enum)
            {
                return captureType;
            }
            else
            {
                throw new System.Exception("GetReturnType only valid for Local Symbols, Properties, Fields, and array indexers");
            }
        }

        public bool DoesReturnIntermediateSymbol()
        {
            return !IsLocalSymbol();
        }

        public bool IsConstExpression()
        {
            // Only basic handling for local symbols for now since we can directly reference them
            if (IsLocalSymbol() && ExecuteGet().declarationType.HasFlag(SymbolDeclTypeFlags.Constant))
                return true;

            return false;
        }

        /// <summary>
        /// Value types need to be copied back to the array if you change a field on them. 
        /// For instance if you have an array of Vector3, and do vecArray[0].x += 4f;, the vector result needs to be copied back to that index in the array since you're modifying an intermediate copy of it.
        /// This function tells you if that copy is necessary.
        /// </summary>
        /// <returns></returns>
        public bool NeedsArrayCopySet()
        {
            return !IsArrayIndexer() && arrayBacktraceValue != null && accessSymbol.symbolCsType.IsValueType;
        }

        public bool ResolveAccessToken(string accessToken)
        {
            bool resolvedToken = false;

            if (accessToken == "this")
            {
                if (captureArchetype != ExpressionCaptureArchetype.Unknown && captureArchetype != ExpressionCaptureArchetype.This)
                    throw new System.ArgumentException("Access token `this` is not valid in this context");

                captureArchetype = ExpressionCaptureArchetype.This;
                resolvedToken = true;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.This)
            {
                resolvedToken = HandleLocalSymbolLookup(accessToken) ||
                                HandleLocalMethodLookup(accessToken) ||
                                HandleLocalPropertyLookup(accessToken) ||
                                HandleLocalUdonBehaviourMethodLookup(accessToken) ||
                                HandleLocalUdonBehaviourPropertyLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                resolvedToken = HandleLocalSymbolLookup(accessToken) ||
                                HandleLocalMethodLookup(accessToken) ||
                                HandleLocalPropertyLookup(accessToken) ||
                                HandleLocalUdonBehaviourMethodLookup(accessToken) ||
                                HandleLocalUdonBehaviourPropertyLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken) ||
                                HandleTypeLookup(accessToken) ||
                                HandleNamespaceLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Type)
            {
                resolvedToken = HandleNestedTypeLookup(accessToken) ||
                                HandleEnumFieldLookup(accessToken) ||
                                HandleStaticMethodLookup(accessToken) ||
                                HandleStaticPropertyLookup(accessToken) ||
                                HandleStaticFieldLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken);
            }
            else if (IsMethod())
            {
                throw new System.InvalidOperationException("Cannot run an accessor on a method!");
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Namespace)
            {
                resolvedToken = HandleNamespaceLookup(accessToken) ||
                                HandleTypeLookup(accessToken);
            }
            // This is where we need to start building intermediate variables to store the input for the next statement
            else if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol || 
                     captureArchetype == ExpressionCaptureArchetype.Property || 
                     captureArchetype == ExpressionCaptureArchetype.LocalProperty ||
                     captureArchetype == ExpressionCaptureArchetype.Field ||
                     captureArchetype == ExpressionCaptureArchetype.ExternUserField ||
                     captureArchetype == ExpressionCaptureArchetype.ExternUserProperty ||
                     captureArchetype == ExpressionCaptureArchetype.ArrayIndexer ||
                     captureArchetype == ExpressionCaptureArchetype.Enum)
            {
                resolvedToken = HandleExternUserFieldLookup(accessToken) ||
                                HandleExternUserMethodLookup(accessToken) ||
                                HandleExternUserPropertyLookup(accessToken) ||
                                HandleUdonSharpInternalMethodLookup(accessToken) ||
                                HandleMemberPropertyAccess(accessToken) ||
                                HandleMemberFieldAccess(accessToken) ||
                                HandleMemberMethodLookup(accessToken); 
            }


            // Fallback in case all other lookups fails. This would usually happen if a namespace doesn't contain any types, but has child namespaces that have types.
            if (!resolvedToken)
            {
                HandleUnknownToken(accessToken);
            }

            return resolvedToken;
        }

        #region Token lookups
        private bool HandleLocalSymbolLookup(string localSymbolName)
        {
            SymbolDefinition symbol = null;

            if (IsThis())
                symbol = visitorContext.topTable.GetGlobalSymbolTable().FindUserDefinedSymbol(localSymbolName);
            else
                symbol = visitorContext.topTable.FindUserDefinedSymbol(localSymbolName);

            // Allow user to mask built-in lookups
            if (symbol == null)
            {
                if (localSymbolName == "gameObject")
                    symbol = visitorContext.topTable.CreateThisSymbol(typeof(GameObject));
                else if (localSymbolName == "transform")
                    symbol = visitorContext.topTable.CreateThisSymbol(typeof(Transform));
            }

            if (symbol == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.LocalSymbol;
            captureLocalSymbol = symbol;
            accessSymbol = symbol;

            return true;
        }
        
        private bool HandleLocalMethodLookup(string localMethodName)
        {
            if (visitorContext.definedMethods == null)
                return false;

            MethodDefinition foundMethod = null;

            foreach (MethodDefinition methodDefinition in visitorContext.definedMethods)
            {
                if (methodDefinition.originalMethodName == localMethodName)
                {
                    foundMethod = methodDefinition;
                    break;
                }
            }

            if (foundMethod == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.LocalMethod;
            captureLocalMethod = foundMethod;

            return true;
        }

        private bool HandleLocalPropertyLookup(string localPropertyName)
        {
            if (visitorContext.definedProperties == null)
                return false;

            PropertyDefinition foundProperty = null;

            foreach (PropertyDefinition propertyDefinition in visitorContext.definedProperties)
            {
                if (propertyDefinition.originalPropertyName == localPropertyName)
                {
                    foundProperty = propertyDefinition;
                    break;
                }
            }

            if (foundProperty == null)
                return false;

            accessSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            captureArchetype = ExpressionCaptureArchetype.LocalProperty;
            captureLocalProperty = foundProperty;

            return true;
        }

        private MethodInfo[] GetTypeMethods(System.Type type, BindingFlags bindingFlags)
        {
            MethodInfo[] methods;
            if (!visitorContext.typeMethodCache.TryGetValue((type, bindingFlags), out methods))
            {
                methods = type.GetMethods(bindingFlags);
                visitorContext.typeMethodCache.Add((type, bindingFlags), methods);
            }

            return methods;
        }

        private bool HandleLocalUdonBehaviourMethodLookup(string localUdonMethodName)
        {
            List<MethodInfo> methods = new List<MethodInfo>(GetTypeMethods(typeof(VRC.Udon.Common.Interfaces.IUdonEventReceiver), BindingFlags.Instance | BindingFlags.Public));
            methods.AddRange(GetTypeMethods(typeof(Component), BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            methods.AddRange(GetTypeMethods(typeof(Object), BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));

            if (localUdonMethodName == "VRCInstantiate")
                methods.AddRange(GetTypeMethods(typeof(UdonSharpBehaviour), BindingFlags.Static | BindingFlags.Public));
            else if (localUdonMethodName == "SetProgramVariable" || localUdonMethodName == "GetProgramVariable")
                methods.Add(typeof(UdonSharpBehaviour).GetMethod(localUdonMethodName, BindingFlags.Instance | BindingFlags.Public));

            IEnumerable<MethodInfo> foundMethods = methods.Where(e => e.Name == localUdonMethodName).Distinct();

            if (foundMethods.Count() == 0)
                return false;

            accessSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            captureMethods = foundMethods.ToArray();
            captureArchetype = ExpressionCaptureArchetype.Method;

            return true;
        }

        private static readonly PropertyInfo[] _componentProperties =
            typeof(Component).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        
        private static readonly PropertyInfo[] _udonEventReceiverProperties =
            typeof(VRC.Udon.Common.Interfaces.IUdonEventReceiver).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private bool HandleLocalUdonBehaviourPropertyLookup(string localUdonPropertyName)
        {
            PropertyInfo[] foundProperties = _componentProperties.Where(e => e.Name == localUdonPropertyName).ToArray();
            
            if (localUdonPropertyName == "enabled" || localUdonPropertyName == "DisableInteractive")
                foundProperties = _udonEventReceiverProperties.Where(e => e.Name == localUdonPropertyName).ToArray();

            if (foundProperties.Length == 0)
                return false;

            accessSymbol = visitorContext.topTable.CreateThisSymbol(visitorContext.behaviourUserType);
            captureProperty = foundProperties.First();
            captureArchetype = ExpressionCaptureArchetype.Property;

            return true;
        }

        private bool HandleTypeLookup(string typeName)
        {
            string typeQualifiedName = visitorContext.resolverContext.ParseBuiltinTypeAlias(typeName);

            if (captureArchetype == ExpressionCaptureArchetype.Namespace)
            {
                if (captureNamespace.Length > 0)
                    typeQualifiedName = $"{captureNamespace}.{typeName}";
                else
                    typeQualifiedName = typeName;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                if (unresolvedAccessChain.Length > 0)
                    typeQualifiedName = $"{unresolvedAccessChain}.{typeName}";
                else
                    typeQualifiedName = typeName;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Type)
            {
                typeQualifiedName = $"{captureType.FullName}.{typeName}";
            }
            else
            {
                throw new System.Exception("Invalid source archetype for type lookup");
            }

            System.Type foundType = null;

            if (isAttributeCaptureScope)
                foundType = visitorContext.resolverContext.ResolveExternType(typeQualifiedName + "Attribute");

            if (foundType == null)
                foundType = visitorContext.resolverContext.ResolveExternType(typeQualifiedName);

            if (foundType == null)
                return false;
            
            unresolvedAccessChain = "";

            captureType = foundType;
            captureArchetype = ExpressionCaptureArchetype.Type;


            return true;
        }

        private bool HandleNamespaceLookup(string namespaceName)
        {
            string qualifiedNamespace = namespaceName;

            if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                if (unresolvedAccessChain.Length > 0)
                    qualifiedNamespace = $"{unresolvedAccessChain}.{namespaceName}";
                else
                    qualifiedNamespace = namespaceName;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Namespace)
            {
                if (captureNamespace.Length > 0)
                    qualifiedNamespace = $"{captureNamespace}.{namespaceName}";
                else
                    qualifiedNamespace = namespaceName;
            }

            if (!allLinkedNamespaces.Contains(qualifiedNamespace))
                return false;

            unresolvedAccessChain = "";
            captureArchetype = ExpressionCaptureArchetype.Namespace;
            captureNamespace = qualifiedNamespace;

            return true;
        }

        private bool HandleStaticMethodLookup(string methodToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Static method lookup only supported on Type archetypes");

            MethodInfo[] methods = captureType.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(e => e.Name == methodToken).ToArray();

            if (methods.Length == 0)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = methods;

            return true;
        }

        private bool HandleStaticPropertyLookup(string propertyToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Static property lookup only supported on Type archetypes");

            PropertyInfo property = captureType.GetProperty(propertyToken, BindingFlags.Static | BindingFlags.Public);

            if (property == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Property;
            captureProperty = property;

            return true;
        }

        private bool HandleNestedTypeLookup(string nestedTypeToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Nested type lookup only supported on Type archetypes");

            System.Type type = captureType.GetNestedType(nestedTypeToken, BindingFlags.Public);

            if (type == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Type;
            captureType = type;

            return true;
        }

        private bool HandleEnumFieldLookup(string enumFieldToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Enum field lookup only supported on Type archetypes");

            if (!captureType.IsEnum)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Enum;
            captureEnum = enumFieldToken;

            return true;
        }

        private bool HandleStaticFieldLookup(string fieldToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Type)
                throw new System.Exception("Static field type lookup only supported on Type archetypes");

            FieldInfo field = captureType.GetField(fieldToken, BindingFlags.Static | BindingFlags.Public);

            if (field == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.Field;
            captureField = field;

            return true;
        }

        private bool HandleMemberPropertyAccess(string propertyToken)
        {
            if (captureArchetype == ExpressionCaptureArchetype.Enum)
                return false;

            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.LocalProperty &&
                captureArchetype != ExpressionCaptureArchetype.Field &&
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserField)
            {
                throw new System.Exception("Can only access properties on Local Symbols, Properties, and Fields");
            }

            System.Type currentReturnType = GetReturnType();

            PropertyInfo[] foundProperties = currentReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == propertyToken).ToArray();

            if (propertyToken == "enabled" &&
                (currentReturnType == typeof(VRC.Udon.UdonBehaviour) ||
                 currentReturnType == typeof(UdonSharpBehaviour) ||
                 currentReturnType.IsSubclassOf(typeof(UdonSharpBehaviour))))
            {
                foundProperties = typeof(VRC.Udon.Common.Interfaces.IUdonEventReceiver).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(e => e.Name == propertyToken).ToArray();
            }

            if (foundProperties.Length == 0)
                return false;

            Debug.Assert(foundProperties.Length == 1);

            PropertyInfo foundProperty = foundProperties[0];

            // We have verified that this is a property token, so access the current accessSymbol to get its value and continue the chain.
            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Property;
            captureProperty = foundProperty;

            return true;
        }

        private bool HandleMemberFieldAccess(string fieldToken)
        {
            if (captureArchetype == ExpressionCaptureArchetype.Enum)
                return false;

            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.LocalProperty &&
                captureArchetype != ExpressionCaptureArchetype.Field &&
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserField)
            {
                throw new System.Exception("Can only access fields on Local Symbols, Properties, and Fields");
            }

            System.Type currentReturnType = GetReturnType();

            FieldInfo[] foundFields = currentReturnType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == fieldToken).ToArray();

            if (foundFields.Length == 0)
                return false;

            Debug.Assert(foundFields.Length == 1);

            FieldInfo foundField = foundFields[0];

            // We have verified that this is a field token, so access the current accessSymbol to get its value and continue the chain.
            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Field;
            captureField = foundField;

            return true;
        }

        private static readonly MethodInfo[] _objectMethods =
            typeof(object).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        
        private bool HandleMemberMethodLookup(string methodToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.LocalProperty &&
                captureArchetype != ExpressionCaptureArchetype.Field && 
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer &&
                captureArchetype != ExpressionCaptureArchetype.Enum &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserField)
            {
                throw new System.Exception("Can only access member methods on Local Symbols, Properties, and Fields");
            }
            
            System.Type returnType = GetReturnType();

            List<MethodInfo> foundMethodInfos = new List<MethodInfo>(returnType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == methodToken));

            if (returnType != typeof(object))
                foundMethodInfos.AddRange(_objectMethods.Where(e => e.Name == methodToken));

            if (foundMethodInfos.Count == 0)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = foundMethodInfos.ToArray();

            return true;
        }

        private bool HandleExternUserFieldLookup(string fieldToken)
        {
            if (accessSymbol == null || !accessSymbol.IsUserDefinedBehaviour())
                return false;

            System.Type returnType = GetReturnType(true);

            ClassDefinition externClass = visitorContext.externClassDefinitions.Find(e => e.userClassType == returnType);

            if (externClass == null)
                return false;

            FieldDefinition foundDefinition = externClass.fieldDefinitions.Find(e => e.fieldSymbol.symbolOriginalName == fieldToken && e.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public));

            if (foundDefinition == null)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.ExternUserField;
            captureExternUserField = foundDefinition;

            return true;
        }

        private bool HandleExternUserMethodLookup(string methodToken)
        {
            if (accessSymbol == null || !accessSymbol.IsUserDefinedBehaviour())
                return false;

            System.Type returnType = GetReturnType(true);
            ClassDefinition externClass = visitorContext.externClassDefinitions.Find(e => e.userClassType == returnType);

            if (externClass == null)
                return false;

            MethodDefinition foundDefinition = externClass.methodDefinitions.Find(e => e.originalMethodName == methodToken && e.declarationFlags.HasFlag(MethodDeclFlags.Public));

            if (foundDefinition == null)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.ExternUserMethod;
            captureExternUserMethod = foundDefinition;

            return true;
        }

        private bool HandleExternUserPropertyLookup(string propertyToken)
        {
            if (accessSymbol == null || !accessSymbol.IsUserDefinedBehaviour())
                return false;

            System.Type returnType = GetReturnType(true);
            ClassDefinition externClass = visitorContext.externClassDefinitions.Find(e => e.userClassType == returnType);

            if (externClass == null)
                return false;

            PropertyDefinition foundDefinition = externClass.propertyDefinitions.Find(e => e.originalPropertyName == propertyToken && e.declarationFlags.HasFlag(PropertyDeclFlags.Public));
            if (foundDefinition == null)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.ExternUserProperty;
            captureExternUserProperty = foundDefinition;

            return true;
        }

        private bool HandleUdonSharpInternalMethodLookup(string methodToken)
        {
            bool isInternalMethod = InternalMethodHandler.ResolveAccessToken(methodToken);

            if (!isInternalMethod)
                return false;

            if (captureArchetype == ExpressionCaptureArchetype.Unknown && unresolvedAccessChain.Length == 0)
                ResolveAccessToken("this");

            accessSymbol = ExecuteGet();
            captureArchetype = ExpressionCaptureArchetype.InternalUdonSharpMethod;

            return true;
        }

        public void HandleArrayIndexerAccess(SymbolDefinition.COWValue indexerValue, SymbolDefinition requestedDestination = null)
        {
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.LocalProperty &&
                captureArchetype != ExpressionCaptureArchetype.ExternUserProperty &&
                !IsField() &&
                captureArchetype != ExpressionCaptureArchetype.ArrayIndexer)
            {
                throw new System.Exception("Can only run indexers on Local Symbols, Properties, Fields, and other indexers");
            }

            System.Type returnType = GetReturnType(true);

            if (!returnType.IsArray && 
                returnType != typeof(string) && // We have hacky handling for strings now
                returnType != typeof(Vector2) &&
                returnType != typeof(Vector3) &&
                returnType != typeof(Vector4) &&
                returnType != typeof(Matrix4x4))
                throw new System.Exception("Can only run array indexers on array types");

            SymbolDefinition cowIndexerSymbol = indexerValue.symbol;
            SymbolDefinition indexerSymbol = cowIndexerSymbol;

            bool isCastValid = (indexerSymbol.symbolCsType == typeof(int) || UdonSharpUtils.GetNumericConversionMethod(typeof(int), indexerSymbol.symbolCsType) != null) &&
                                indexerSymbol.symbolCsType.IsValueType &&
                                indexerSymbol.symbolCsType != typeof(float) && indexerSymbol.symbolCsType != typeof(float) && indexerSymbol.symbolCsType != typeof(decimal);

            // This will need to be changed if Udon ever exposes collections with non-int indexers
            if (isCastValid)
                indexerSymbol = CastSymbolToType(indexerSymbol, typeof(int), true);
            else
                indexerSymbol = CastSymbolToType(indexerSymbol, typeof(int), false); // Non-explicit cast to handle if any types have implicit conversion operators and throw an error otherwise

            SymbolDefinition.COWValue oldCOWValue = _accessValue;
            _accessValue = ExecuteGetCOW(); // implicitly adds refcount
            oldCOWValue?.Dispose();

            cowValue = null; // Move ownership to accessValue here
            _accessSymbol = null;

            captureArchetype = ExpressionCaptureArchetype.ArrayIndexer;
            // If we didn't need to cast, use the original symbol's COW value as-is.
            // Otherwise, (for consistency) COW-ify the post-cast value.
            if (cowIndexerSymbol == indexerSymbol)
            {
                // We didn't need to do a conversion, so retain the existing COW value
                arrayIndexerIndexValue = indexerValue; // implicitly creates a new reference
            }
            else
            {
                // We needed to do a cast, so create a COW value for the post-cast value
                using (SymbolDefinition.COWValue cowIndex = indexerSymbol.GetCOWValue(visitorContext))
                {
                    arrayIndexerIndexValue = cowIndex;
                }
            }

            this.requestedDestination = requestedDestination;
        }

        public void HandleGenericAccess(List<System.Type> genericArguments)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Method && captureArchetype != ExpressionCaptureArchetype.InternalUdonSharpMethod)
                throw new System.ArgumentException("Cannot resolve generic arguments on non-method expression");

            genericTypeArguments = genericArguments;

            if (captureArchetype == ExpressionCaptureArchetype.InternalUdonSharpMethod)
                InternalMethodHandler.HandleGenericAccess(genericArguments);
        }

        private void HandleUnknownToken(string unknownToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown && captureArchetype != ExpressionCaptureArchetype.Namespace)
            {
                System.Type returnType = null;

                try
                {
                    returnType = GetReturnType(true);
                }
                catch { }

                string tokenName = unresolvedAccessChain + (unresolvedAccessChain.Length != 0 ? "." : "") + unknownToken;

                if (returnType != null)
                {
                    throw new System.Exception($"'{returnType.Name}' does not contain a definition for '{tokenName}'");
                }
                else
                {
                    throw new System.Exception($"Unknown type/field/parameter/method '{tokenName}'");
                }
            }

            captureArchetype = ExpressionCaptureArchetype.Unknown;
            if (captureNamespace.Length > 0)
            {
                unresolvedAccessChain = captureNamespace;
                captureNamespace = "";
            }

            if (unresolvedAccessChain.Length > 0)
                unresolvedAccessChain += $".{unknownToken}";
            else
                unresolvedAccessChain = unknownToken;
        }
        #endregion
    }
}
