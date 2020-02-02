using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UdonSharp
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
        This, // this.<next token> indicates that this must be a local symbol or method
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
        string unresolvedAccessChain = "";
        public ExpressionCaptureArchetype captureArchetype { get; private set; } = ExpressionCaptureArchetype.Unknown;

        // Only the parameters corresponding with the current captureArchetype are guaranteed to be valid
        public string captureNamespace { get; private set; } = "";
        public PropertyInfo captureProperty { get; private set; } = null;
        public FieldInfo captureField { get; private set; } = null;
        public SymbolDefinition captureLocalSymbol { get; private set; } = null;
        public MethodInfo[] captureMethods { get; private set; } = null; // If there are override methods we do not know which to use until we invoke them with the arguments
        public System.Type captureType { get; private set; } = null;

        private SymbolDefinition accessSymbol = null;

        // Namespace lookup to check if something is a namespace
        private static HashSet<string> allLinkedNamespaces;

        // The current visitor context
        ASTVisitorContext visitorContext;

        ExpressionCaptureScope parentScope;

        bool disposed = false;

        private void FindValidNamespaces()
        {
            if (allLinkedNamespaces != null)
                return;

            allLinkedNamespaces = new HashSet<string>();

            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                allLinkedNamespaces.UnionWith(assembly.GetTypes().Select(e => e.Namespace).Distinct());
            }
        }

        public ExpressionCaptureScope(ASTVisitorContext context, ExpressionCaptureScope parentScopeIn)
        {
            FindValidNamespaces();

            visitorContext = context;
            parentScope = parentScopeIn;

            visitorContext.PushCaptureScope(this);
        }

        ~ExpressionCaptureScope()
        {
            Debug.Assert(disposed == true, "Expression capture scope was not disposed!");
        }
        
        public void Dispose()
        {
            if (parentScope != null)
                parentScope.InheretScope(this);
            
            Debug.Assert(visitorContext.topCaptureScope == this);
            if (visitorContext.topCaptureScope == this)
                visitorContext.PopCaptureScope();

            disposed = true;
        }

        private void InheretScope(ExpressionCaptureScope childScope)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Unknown ||
                childScope.captureArchetype == ExpressionCaptureArchetype.This ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Method ||
                childScope.captureArchetype == ExpressionCaptureArchetype.Namespace)
                return;

            captureArchetype = childScope.captureArchetype;
            captureField = childScope.captureField;
            captureLocalSymbol = childScope.captureLocalSymbol;
            captureProperty = childScope.captureProperty;
            captureType = childScope.captureType;
            accessSymbol = childScope.accessSymbol;
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

        public void SetToMethods(MethodInfo[] methods)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.ArgumentException("Cannot set non-unknown symbol scope to a method");

            foreach (MethodInfo method in methods)
            {
                if (!method.IsStatic)
                    throw new System.ArgumentException("All methods set in SetToMethods must be static");
            }

            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = methods;
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
            return captureArchetype == ExpressionCaptureArchetype.Method;
        }

        public bool IsProperty()
        {
            return captureArchetype == ExpressionCaptureArchetype.Property;
        }

        public bool IsField()
        {
            return captureArchetype == ExpressionCaptureArchetype.Field;
        }

        public bool IsType()
        {
            return captureArchetype == ExpressionCaptureArchetype.Type;
        }

        public bool IsThis()
        {
            return captureArchetype == ExpressionCaptureArchetype.This;
        }

        public bool IsLocalSymbol()
        {
            return captureArchetype == ExpressionCaptureArchetype.LocalSymbol;
        }
        #endregion

        // Inserts uasm instructions to get the value stored in the current localSymbol, property, or field
        public SymbolDefinition ExecuteGet()
        {
            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
                return captureLocalSymbol;

            SymbolDefinition outSymbol = null;

            if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                MethodInfo getMethod = captureProperty.GetGetMethod();

                if (getMethod.ReturnType == typeof(void))
                    throw new System.TypeLoadException("Cannot return type of void from a get statement");
                
                outSymbol = visitorContext.topTable.CreateUnnamedSymbol(getMethod.ReturnType, SymbolDeclTypeFlags.Internal);

                string methodUdonString = visitorContext.resolverContext.GetUdonMethodName(getMethod);

                if (!getMethod.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(methodUdonString);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                outSymbol = visitorContext.topTable.CreateUnnamedSymbol(captureField.FieldType, SymbolDeclTypeFlags.Internal);

                string fieldAccessorUdonName = visitorContext.resolverContext.GetUdonFieldAccessorName(captureField, FieldAccessorType.Get);

                if (!captureField.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(outSymbol);
                visitorContext.uasmBuilder.AddExternCall(fieldAccessorUdonName);
            }
            else
            {
                throw new System.Exception("Get can only be run on Fields, Properties, and Local Symbols");
            }

            return outSymbol;
        }

        public void ExecuteSet(SymbolDefinition value)
        {
            SymbolDefinition convertedValue = CastSymbolToType(value, captureLocalSymbol.symbolCsType, false);

            // If it's a local symbol, it's just a simple COPY
            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
            {
                visitorContext.uasmBuilder.AddCopy(captureLocalSymbol, convertedValue);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                MethodInfo setMethod = captureProperty.GetSetMethod();

                string udonMethodString = visitorContext.resolverContext.GetUdonMethodName(setMethod);

                if (!setMethod.IsStatic)
                {
                    visitorContext.uasmBuilder.AddPush(accessSymbol);
                }

                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(udonMethodString);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                string fieldSetterUdonName = visitorContext.resolverContext.GetUdonFieldAccessorName(captureField, FieldAccessorType.Set);

                if (!captureField.IsStatic)
                    visitorContext.uasmBuilder.AddPush(accessSymbol);

                visitorContext.uasmBuilder.AddPush(convertedValue);
                visitorContext.uasmBuilder.AddExternCall(fieldSetterUdonName);
            }
            else
            {
                throw new System.Exception("Set can only be run on Fields, Properties, and local Symbols");
            }
        }

        // There's probably a better place for this function...
        private SymbolDefinition CastSymbolToType(SymbolDefinition sourceSymbol, System.Type targetType, bool isExplicit)
        {
            if (!isExplicit && !targetType.IsImplicitlyAssignableFrom(sourceSymbol.symbolCsType))
                throw new System.ArgumentException($"Cannot implicitly cast from {sourceSymbol.symbolCsType} to {targetType}");

            // Exact type match, just return the symbol, this is what will happen a majority of the time.
            if (targetType == sourceSymbol.symbolCsType)
                return sourceSymbol;

            // We can just return the symbol as-is
            // This might need revisiting since there may be *some* case where you want the type to be explicitly casted to a specific type regardless of the source type
            if (targetType.IsAssignableFrom(sourceSymbol.symbolCsType))
                return sourceSymbol;

            // Numeric conversion handling
            MethodInfo conversionFunction = UdonSharpUtils.GetNumericConversionMethod(targetType, sourceSymbol.symbolCsType);
            if (conversionFunction != null && (isExplicit || UdonSharpUtils.IsNumericImplicitCastValid(targetType, sourceSymbol.symbolCsType)))
            {
                // This code is copied 3 times, todo: find a decent way to refactor it
                SymbolDefinition castOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                visitorContext.uasmBuilder.AddPush(sourceSymbol);
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
                SymbolDefinition castOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

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
                    SymbolDefinition castOutput = visitorContext.topTable.CreateUnnamedSymbol(targetType, SymbolDeclTypeFlags.Internal);

                    visitorContext.uasmBuilder.AddPush(sourceSymbol);
                    visitorContext.uasmBuilder.AddPush(castOutput);
                    visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(foundConversion));

                    return castOutput;
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
        private SymbolDefinition[] GetExpandedInvokeParams(MethodInfo targetMethod, SymbolDefinition[] invokeParams)
        {
            ParameterInfo[] methodParams = targetMethod.GetParameters();

            List<SymbolDefinition> newInvokeParams = new List<SymbolDefinition>();

            for (int i = 0; i < methodParams.Length; ++i)
            {
                // Handle default args
                if (invokeParams.Length < i)
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
                    throw new System.NotImplementedException("todo: params method calls");
                    continue;
                }

                newInvokeParams.Add(CastSymbolToType(invokeParams[i], methodParams[i].ParameterType, false));
            }

            return newInvokeParams.ToArray();
        }

        public SymbolDefinition InvokeExtern(SymbolDefinition[] invokeParams)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Method)
            {
                throw new System.Exception("You can only invoke methods!");
            }

            // Find valid overrides
            MethodInfo targetMethod = visitorContext.resolverContext.FindBestOverloadFunction(captureMethods, invokeParams.Select(e => e.symbolCsType).ToList());

            if (targetMethod == null)
            {
                throw new System.Exception("Could not find valid method for given parameters!");
            }
             
            SymbolDefinition[] expandedParams = GetExpandedInvokeParams(targetMethod, invokeParams);

            // Now make the needed symbol definitions and run the invoke
            if (!targetMethod.IsStatic)
                visitorContext.uasmBuilder.AddPush(accessSymbol);

            foreach (SymbolDefinition invokeParam in expandedParams)
                visitorContext.uasmBuilder.AddPush(invokeParam);

            SymbolDefinition returnSymbol = null;

            if (targetMethod.ReturnType != typeof(void))
            {
                returnSymbol = visitorContext.topTable.CreateNamedSymbol("returnVal", targetMethod.ReturnType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);

                visitorContext.uasmBuilder.AddPush(returnSymbol);

                if (visitorContext.topCaptureScope != null && visitorContext.topCaptureScope.IsUnknownArchetype())
                    visitorContext.topCaptureScope.SetToLocalSymbol(returnSymbol);
            }

            visitorContext.uasmBuilder.AddExternCall(visitorContext.resolverContext.GetUdonMethodName(targetMethod));

            return returnSymbol;
        }

        public System.Type GetReturnType()
        {
            if (captureArchetype == ExpressionCaptureArchetype.Method)
                throw new System.Exception("Cannot infer return type from method without function arguments");

            if (captureArchetype == ExpressionCaptureArchetype.LocalSymbol)
            {
                return accessSymbol.symbolCsType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Property)
            {
                return captureProperty.GetGetMethod().ReturnType;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Field)
            {
                return captureField.FieldType;
            }
            else
            {
                throw new System.Exception("GetReturnType only valid for Local Symbols, Properties, and Fields");
            }
        }

        public bool ResolveAccessToken(string accessToken)
        {
            bool resolvedToken = false;

            if (accessToken == "this")
            {
                if (captureArchetype != ExpressionCaptureArchetype.Unknown && captureArchetype != ExpressionCaptureArchetype.This)
                    throw new System.ArgumentException("Access token `this` is not valid in this context");

                captureArchetype = ExpressionCaptureArchetype.This;
            }
            else if (captureArchetype == ExpressionCaptureArchetype.This)
            {
                resolvedToken = HandleLocalSymbolLookup(accessToken) ||
                                HandleLocalMethodLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Unknown)
            {
                resolvedToken = HandleLocalSymbolLookup(accessToken) ||
                                HandleLocalMethodLookup(accessToken) ||
                                HandleTypeLookup(accessToken) ||
                                HandleNamespaceLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Type)
            {
                resolvedToken = HandleNestedTypeLookup(accessToken) ||
                                HandleStaticMethodLookup(accessToken) ||
                                HandleStaticPropertyLookup(accessToken) ||
                                HandleStaticFieldLookup(accessToken);
            }
            else if (captureArchetype == ExpressionCaptureArchetype.Method)
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
                     captureArchetype == ExpressionCaptureArchetype.Field)
            {
                resolvedToken = HandleMemberPropertyAccess(accessToken) ||
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
            // todo: handle the builtin self reference to transform, gameobject, and udonBehavior

            SymbolDefinition symbol = visitorContext.topTable.FindUserDefinedSymbol(localSymbolName);

            if (symbol == null)
                return false;

            captureArchetype = ExpressionCaptureArchetype.LocalSymbol;
            captureLocalSymbol = symbol;
            accessSymbol = symbol;

            return true;
        }

        // todo: when we figure out how to deal with locally defined methods and if we want to deal with implementing a value stack for recursion
        private bool HandleLocalMethodLookup(string localMethodName)
        {
            return false;
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

            System.Type foundType = visitorContext.resolverContext.ResolveExternType(typeQualifiedName);

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
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.Field)
            {
                throw new System.Exception("Can only access properties on Local Symbols, Properties, and Fields");
            }

            System.Type currentReturnType = GetReturnType();

            PropertyInfo[] foundProperties = currentReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == propertyToken).ToArray();

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
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.Field)
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

        private bool HandleMemberMethodLookup(string methodToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.LocalSymbol &&
                captureArchetype != ExpressionCaptureArchetype.Property &&
                captureArchetype != ExpressionCaptureArchetype.Field)
            {
                throw new System.Exception("Can only access member methods on Local Symbols, Properties, and Fields");
            }
            
            System.Type returnType = GetReturnType();

            MethodInfo[] foundMethodInfos = returnType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == methodToken).ToArray();

            if (foundMethodInfos.Length == 0)
                return false;

            SymbolDefinition newAccessSymbol = ExecuteGet();

            accessSymbol = newAccessSymbol;
            captureArchetype = ExpressionCaptureArchetype.Method;
            captureMethods = foundMethodInfos;

            return true;
        }

        private void HandleUnknownToken(string unknownToken)
        {
            if (captureArchetype != ExpressionCaptureArchetype.Unknown)
                throw new System.Exception("Cannot transition from known archetype to unknown archetype!");

            if (unresolvedAccessChain.Length > 0)
                unresolvedAccessChain += $".{unknownToken}";
            else
                unresolvedAccessChain = unknownToken;
        }
        #endregion
    }
}
