
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;
using VRC.Udon.Editor;
using VRC.Udon.Graph;

namespace UdonSharp
{
    public enum UdonReferenceType
    {
        None,
        Variable,
        Const,
        Type,
    }

    public enum FieldAccessorType
    {
        Get,
        Set,
    }

    public class ResolverContext
    {
        public HashSet<string> usingNamespaces { get; private set; } = new HashSet<string>() { "" }; // Add a blank namespace in case the type is already fully qualified, this is used in ResolveExternType() and ResolveExternMethod()

        private static readonly IReadOnlyDictionary<string, string> builtinTypeAliasMap = new Dictionary<string, string>()
        {
            { "string", "System.String" },
            { "int", "System.Int32" },
            { "uint", "System.UInt32" },
            { "long", "System.Int64" },
            { "ulong", "System.UInt64" },
            { "short", "System.Int16" },
            { "ushort", "System.UInt16" },
            { "char", "System.Char" },
            { "bool", "System.Boolean" },
            { "byte", "System.Byte" },
            { "sbyte", "System.SByte" },
            { "float", "System.Single" },
            { "double", "System.Double" },
            { "decimal", "System.Decimal" },
            { "object", "System.Object" },
            { "void", "System.Void" } // void might need to be revisited since it could mess with something
        };

        private Dictionary<string, System.Type> typeLookupCache = new Dictionary<string, System.Type>();

        private static HashSet<string> nodeDefinitionLookup;

        private static Dictionary<string, string> builtinEventLookup;
        private static bool cacheInitRan = false;
        private static readonly object cacheInitLock = new object();

        public static void CacheInit()
        {
            if (cacheInitRan)
                return;

            lock (cacheInitLock)
            {
                if (cacheInitRan)
                    return;

                if (nodeDefinitionLookup == null)
                {
                    nodeDefinitionLookup = new HashSet<string>(UdonEditorManager.Instance.GetNodeDefinitions().Select(e => e.fullName));
                }

                if (builtinEventLookup == null)
                {
                    builtinEventLookup = new Dictionary<string, string>();

                    foreach (UdonNodeDefinition nodeDefinition in UdonEditorManager.Instance.GetNodeDefinitions("Event_"))
                    {
                        if (nodeDefinition.fullName == "Event_Custom")
                            continue;

                        string eventNameStr = nodeDefinition.fullName.Substring(6);
                        char[] eventName = eventNameStr.ToCharArray();
                        eventName[0] = char.ToLowerInvariant(eventName[0]);

                        if (!builtinEventLookup.ContainsKey(eventNameStr))
                            builtinEventLookup.Add(eventNameStr, "_" + new string(eventName));
                        else
                            Debug.LogWarning($"Duplicate event node {nodeDefinition.fullName} found");
                    }
                }

                cacheInitRan = true;
            }
        }

        public ResolverContext()
        {
            CacheInit();

            if (!cacheInitRan)
                throw new System.Exception("Type cache must be initialized before you can construct a ResolverContext");
        }

        public void AddNamespace(string namespaceToAdd)
        {
            if (!usingNamespaces.Contains(namespaceToAdd))
                usingNamespaces.Add(namespaceToAdd);
        }

        public void AddLocalFunction()
        {
            throw new System.NotImplementedException();
        }

        public bool ReplaceInternalEventName(ref string eventName)
        {
            if (builtinEventLookup.ContainsKey(eventName))
            {
                eventName = builtinEventLookup[eventName];
                return true;
            }

            return false;
        }

        private readonly Dictionary<string, System.Tuple<System.Type, string>[]> internalMethodCustomArgs = new Dictionary<string, System.Tuple<System.Type, string>[]>()
        {
            { "_onAnimatorIK", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(int), "onAnimatorIkLayerIndex") } },
            { "_onAudioFilterRead", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(float[]), "onAudioFilterReadData"), new System.Tuple<System.Type, string>(typeof(int), "onAudioFilterReadChannels") } },
            { "_onCollisionEnter", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collision), "onCollisionEnterOther") } },
            { "_onCollisionEnter2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collision2D), "onCollisionEnter2DOther") } },
            { "_onCollisionExit", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collision), "onCollisionExitOther") } },
            { "_onCollisionExit2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collision2D), "onCollisionExit2DOther") } },
            { "_onCollisionStay", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collision), "onCollisionStayOther") } },
            { "_onCollisionStay2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collision2D), "onCollisionStay2DOther") } },
            { "_onControllerColliderHit", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(ControllerColliderHit), "onControllerColliderHitHit") } },
            { "_onJointBreak", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(float), "onJointBreakBreakForce") } },
            { "_onJointBreak2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Joint2D), "onJointBreak2DBrokenJoint") } },
            { "_onParticleCollision", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(GameObject), "onParticleCollisionOther") } },
            { "_onRenderImage", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(RenderTexture), "onRenderImageSrc"), new System.Tuple<System.Type, string>(typeof(RenderTexture), "onRenderImageDest") } },
            { "_onTriggerEnter", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collider), "onTriggerEnterOther") } },
            { "_onTriggerEnter2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collider2D), "onTriggerEnter2DOther") } },
            { "_onTriggerExit", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collider), "onTriggerExitOther") } },
            { "_onTriggerExit2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collider2D), "onTriggerExit2DOther") } },
            { "_onTriggerStay", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collider), "onTriggerStayOther") } },
            { "_onTriggerStay2D", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(Collider2D), "onTriggerStay2DOther") } },
            { "_onPlayerJoined", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerJoinedPlayer") } },
            { "_onPlayerLeft", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerLeftPlayer") } },
            { "_onStationEntered", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onStationEnteredPlayer") } },
            { "_onStationExited", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onStationExitedPlayer") } },
            { "_onOwnershipRequest", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onOwnershipRequestRequester"), new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onOwnershipRequestNewOwner") } },
            { "_onPlayerTriggerEnter", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerTriggerEnterPlayer") } },
            { "_onPlayerTriggerExit", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerTriggerExitPlayer") } },
            { "_onPlayerTriggerStay", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerTriggerStayPlayer") } },
            { "_onPlayerCollisionEnter", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerCollisionEnterPlayer") } },
            { "_onPlayerCollisionExit", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerCollisionExitPlayer") } },
            { "_onPlayerCollisionStay", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerCollisionStayPlayer") } },
            { "_onPlayerParticleCollision", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerParticleCollisionPlayer") } },
            { "_onPlayerRespawn", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onPlayerRespawnPlayer") } },
            { "_onVideoError", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDK3.Components.Video.VideoError), "onVideoErrorVideoError") } },
            { "_midiNoteOn", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(int), "midiNoteOnChannel"), new System.Tuple<System.Type, string>(typeof(int), "midiNoteOnNumber"), new System.Tuple<System.Type, string>(typeof(int), "midiNoteOnVelocity") } },
            { "_midiNoteOff", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(int), "midiNoteOffChannel"), new System.Tuple<System.Type, string>(typeof(int), "midiNoteOffNumber"), new System.Tuple<System.Type, string>(typeof(int), "midiNoteOffVelocity") } },
            { "_midiControlChange", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(int), "midiControlChangeChannel"), new System.Tuple<System.Type, string>(typeof(int), "midiControlChangeNumber"), new System.Tuple<System.Type, string>(typeof(int), "midiControlChangeValue") } },
            { "_inputJump", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(bool), "inputJumpBoolValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputJumpArgs") } },
            { "_inputUse", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(bool), "inputUseBoolValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputUseArgs") } },
            { "_inputGrab", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(bool), "inputGrabBoolValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputGrabArgs") } },
            { "_inputDrop", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(bool), "inputDropBoolValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputDropArgs") } },
            { "_inputMoveHorizontal", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(float), "inputMoveHorizontalFloatValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputMoveHorizontalArgs") } },
            { "_inputMoveVertical", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(float), "inputMoveVerticalFloatValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputMoveVerticalArgs") } },
            { "_inputLookHorizontal", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(float), "inputLookHorizontalFloatValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputLookHorizontalArgs") } },
            { "_inputLookVertical", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(float), "inputLookVerticalFloatValue"), new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.UdonInputEventArgs), "inputLookVerticalArgs") } },
            { "_onOwnershipTransferred", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.SDKBase.VRCPlayerApi), "onOwnershipTransferredPlayer") } },
            { "_onPostSerialization", new System.Tuple<System.Type, string>[] { new System.Tuple<System.Type, string>(typeof(VRC.Udon.Common.SerializationResult), "onPostSerializationResult") } },
        };

        public System.Tuple<System.Type, string>[] GetMethodCustomArgs(string methodName)
        {
            if (internalMethodCustomArgs.ContainsKey(methodName))
                return internalMethodCustomArgs[methodName];

            return null;
        }

        public MethodInfo ResolveStaticMethod(string qualifiedMethodName, string[] argTypeNames)
        {
            System.Type[] types = argTypeNames.Select(e => ResolveExternType(e)).ToArray();

            return ResolveStaticMethod(qualifiedMethodName, types);
        }

        // This will fall down in situations with stuff like StaticManager.instance.DoThing() where instance is a accessor, not a type.
        // I need to handle this better by traversing each transition from the lhs type/value to the rhs type/value
        public MethodInfo ResolveStaticMethod(string qualifiedMethodName, System.Type[] argTypes)
        {
            string[] tokQualifiedMethod = qualifiedMethodName.Split('.');

            string qualifiedType = string.Join(".", tokQualifiedMethod.Take(tokQualifiedMethod.Length - 1));
            string memberMethodName = tokQualifiedMethod[tokQualifiedMethod.Length - 1];

            return ResolveMemberMethod(ResolveExternType(qualifiedType), memberMethodName, argTypes);
        }

        public MethodInfo ResolveMemberMethod(System.Type lhsType, string methodName, System.Type[] argTypes)
        {
            foreach (MemberInfo info in ResolveMemberMethods(lhsType, methodName))
            {
                if (info is MethodInfo)
                {
                    MethodInfo methodInfo = info as MethodInfo;

                    if (methodInfo.Name == methodName)
                    {
                        ParameterInfo[] parameters = methodInfo.GetParameters();
                        bool isValidMethod = true;

                        if (parameters.Length == (argTypes.Length - 1)) // Ignore default args for now...
                        {
                            for (int i = 0; i < parameters.Length; ++i)
                            {
                                if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i]))
                                {
                                    isValidMethod = false;
                                    break;
                                }
                            }
                        }

                        if (isValidMethod)
                            return methodInfo;
                    }
                }
            }

            return null;
        }

        public IEnumerable<MethodInfo> ResolveMemberMethods(System.Type type, string methodName)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(e => e.Name == methodName);
        }

        public string ParseBuiltinTypeAlias(string typeName)
        {
            string newTypeName;
            if (builtinTypeAliasMap.TryGetValue(typeName, out newTypeName))
                return newTypeName;

            return typeName;
        }

        private static List<Assembly> loadedAssemblyCache = null;
        private static readonly object assemblyCacheLock = new object();
        
        public System.Type ResolveExternType(string qualifiedTypeName)
        {
            qualifiedTypeName = ParseBuiltinTypeAlias(qualifiedTypeName);

            System.Type foundType;

            // If we've already used this type then it's a simple cache lookup
            if (typeLookupCache.TryGetValue(qualifiedTypeName, out foundType))
            {
                return foundType;
            }

            // We haven't used this type yet, look through all of the loaded assemblies for the type. This can be quite expensive so we cache the results.
            // todo: look at optimizing the lookup for real
            foreach (string includedNamespace in usingNamespaces)
            {
                string testFullyQualifiedType = includedNamespace.Length > 0 ? $"{includedNamespace}.{qualifiedTypeName}" : qualifiedTypeName;

                if (typeLookupCache.TryGetValue(testFullyQualifiedType, out foundType))
                {
                    return foundType;
                }

                foundType = System.Type.GetType(testFullyQualifiedType);

                if (foundType != null)
                {
                    if (!typeLookupCache.ContainsKey(qualifiedTypeName))
                        typeLookupCache.Add(qualifiedTypeName, foundType);
                    if (!typeLookupCache.ContainsKey(testFullyQualifiedType))
                        typeLookupCache.Add(testFullyQualifiedType, foundType);
                    return foundType;
                }
                else // Type wasn't found in current assembly, look through all loaded assemblies
                {
                    if (loadedAssemblyCache == null)
                    {
                        lock (assemblyCacheLock)
                        {
                            if (loadedAssemblyCache == null)
                            {
                                loadedAssemblyCache = System.AppDomain.CurrentDomain.GetAssemblies()
                                    .OrderBy(e => {
                                        if (e.IsDynamic || string.IsNullOrEmpty(e.Location) || e.Location.StartsWith("data"))
                                            return false;

                                        string assemblyName = e.GetName().Name;

                                    return
                                        assemblyName.Contains("UnityEngine") ||
                                        assemblyName.Contains("System") ||
                                        assemblyName.Contains("VRC") ||
                                        assemblyName.Contains("Udon") ||
                                        assemblyName.Contains("Assembly-CSharp") ||
                                        assemblyName.Contains("mscorlib");
                                    }).Reverse().ToList();
                            }
                        }
                    }
                    
                    foreach (Assembly assembly in loadedAssemblyCache)
                    { 
                        foundType = assembly.GetType(testFullyQualifiedType);

                        if (foundType != null)
                        {
                            //Debug.Log($"Found type {foundType} in assembly {assembly.GetName().Name}");
                            
                            if (!typeLookupCache.ContainsKey(qualifiedTypeName)) 
                                typeLookupCache.Add(qualifiedTypeName, foundType);
                            if (!typeLookupCache.ContainsKey(testFullyQualifiedType))
                                typeLookupCache.Add(testFullyQualifiedType, foundType);
                            return foundType;
                        }
                    }
                }
            }
            
            typeLookupCache.Add(qualifiedTypeName, null);
            // We didn't find a valid type
            //throw new System.ArgumentException($"Could not resolve type {qualifiedTypeName}");
            return null;
        }

        public string SanitizeTypeName(string typeName)
        {
            return typeName.Replace(",", "")
                           .Replace(".", "")
                           .Replace("[]", "Array")
                           .Replace("&", "Ref")
                           .Replace("+", "");
        }

        /// <summary>
        /// Verifies that Udon supports the given type and resolves the type name used to reference it in Udon
        /// </summary>
        /// <param name="externType">The found type</param>
        /// <param name="skipBaseTypeRemap">Skips remapping base VRCSDK types, this is primarily used for VRCSDK function return value types since they may point to the base type and we want to maintain that.</param>
        /// <returns>The Udon type name string if it is a valid Udon type, 
        ///     or null if it is not a valid Udon type.</returns>
        public string GetUdonTypeName(System.Type externType, bool skipBaseTypeRemap = false)
        {
            if (!skipBaseTypeRemap)
                externType = UdonSharpUtils.RemapBaseType(externType);

            string externTypeName = externType.GetNameWithoutGenericArity();
            while (externType.IsArray || externType.IsByRef)
            {
                externType = externType.GetElementType();
            }

            string typeNamespace = externType.Namespace;

            // Handle nested type names (+ sign in names)
            if (externType.DeclaringType != null)
            {
                string declaringTypeNamespace = "";

                System.Type declaringType = externType.DeclaringType;

                while (declaringType != null)
                {
                    declaringTypeNamespace = $"{externType.DeclaringType.Name}.{declaringTypeNamespace}";
                    declaringType = declaringType.DeclaringType;
                }

                typeNamespace += $".{declaringTypeNamespace}";
            }

            if (externTypeName == "T" || externTypeName == "T[]")
                typeNamespace = "";
            
            string fullTypeName = SanitizeTypeName($"{typeNamespace}.{externTypeName}");

            foreach (System.Type genericType in externType.GetGenericArguments())
            {
                fullTypeName += GetUdonTypeName(genericType);
            }

            // Seems like Udon does shortening for this specific type somewhere
            if (fullTypeName == "SystemCollectionsGenericListT")
            {
                fullTypeName = "ListT";
            }
            else if (fullTypeName == "SystemCollectionsGenericIEnumerableT")
            {
                fullTypeName = "IEnumerableT";
            }

            fullTypeName = fullTypeName.Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            return fullTypeName;
        }

        /// <summary>
        /// Verifies that Udon supports the given method and resolves the name used to reference it in Udon EXTERN calls
        /// </summary>
        /// <param name="externMethod"></param>
        /// <returns></returns>
        public string GetUdonMethodName(MethodBase externMethod, bool validate = true, List<System.Type> genericArguments = null)
        {
            System.Type methodSourceType = externMethod.ReflectedType;

            if (genericArguments != null)
            {
                if (genericArguments.Count != 1)
                    throw new System.ArgumentException("UdonSharp only supports 1 type generic methods at the moment");

                methodSourceType = genericArguments.First();
            }

            methodSourceType = UdonSharpUtils.RemapBaseType(methodSourceType);

            bool isUdonSharpBehaviour = false;

            if (methodSourceType == typeof(UdonSharpBehaviour) || methodSourceType.IsSubclassOf(typeof(UdonSharpBehaviour)))
            {
                methodSourceType = typeof(VRC.Udon.UdonBehaviour);
                isUdonSharpBehaviour = true;
            }

            string functionNamespace = SanitizeTypeName(methodSourceType.FullName ?? methodSourceType.Namespace + methodSourceType.Name).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            string methodName = $"__{externMethod.Name.Trim('_').TrimStart('.')}";
            ParameterInfo[] methodParams = externMethod.GetParameters();

            if (isUdonSharpBehaviour
                && methodName == "__VRCInstantiate")
            {
                functionNamespace = "VRCInstantiate";
                methodName = "__Instantiate";
            }

            string paramStr = "";
            
            if (methodParams.Length > 0)
            {
                paramStr = "_"; // Arg separator
            
                foreach (ParameterInfo parameterInfo in methodParams)
                {
                    paramStr += $"_{GetUdonTypeName(parameterInfo.ParameterType, true)}";
                }
            }
            else if (externMethod is ConstructorInfo)
                paramStr = "__";

            string returnStr = "";

            if (externMethod is MethodInfo)
            {
                returnStr = $"__{GetUdonTypeName(((MethodInfo)externMethod).ReturnType, true)}";
            }
            else if (externMethod is ConstructorInfo)
            {
                returnStr = $"__{GetUdonTypeName(((ConstructorInfo)externMethod).DeclaringType)}";
            }
            else
                throw new System.Exception("Invalid extern method type for getting Udon name");

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}{returnStr}";

            if (validate && !nodeDefinitionLookup.Contains(finalFunctionSig))
            {
                throw new System.Exception($"Method {finalFunctionSig} is not exposed in Udon");
            }

            return finalFunctionSig;
        }

        public string GetUdonFieldAccessorName(FieldInfo externField, FieldAccessorType accessorType, bool validate = true)
        {
            System.Type fieldType = UdonSharpUtils.RemapBaseType(externField.DeclaringType);

            string functionNamespace = SanitizeTypeName(fieldType.FullName).Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");
            string methodName = $"__{(accessorType == FieldAccessorType.Get ? "get" : "set")}_{externField.Name.Trim('_')}";

            string paramStr = $"__{GetUdonTypeName(externField.FieldType)}";

            string finalFunctionSig = $"{functionNamespace}.{methodName}{paramStr}";

            if (validate && !nodeDefinitionLookup.Contains(finalFunctionSig))
            {
                throw new System.Exception($"Field accessor {finalFunctionSig} is not exposed in Udon");
            }

            return finalFunctionSig;
        }

        public bool IsValidUdonMethod(string udonMethodStr)
        {
            return nodeDefinitionLookup.Contains(udonMethodStr);
        }
        
        private int ScoreMethodParamArgPair(ParameterInfo methodParam, System.Type argType)
        {
            // This doesn't yet handle implicit user defined casts... there are probably other things this should handle too.
            int score = 1000000;
            
            if (methodParam.ParameterType == argType)
            {
                score = 0;
            }
            else if (methodParam.HasDefaultValue && argType == null)
            {
                score = 5; // Avoid unused default args
            }
            else if (methodParam.ParameterType.IsValidNumericImplicitCastTargetType() && argType.IsValidNumericImplictCastSourceType())
            {
                score = UdonSharpUtils.GetImplicitNumericCastDistance(methodParam.ParameterType, argType);
            }
            else if (methodParam.ParameterType == typeof(object))
            {
                score = 30; // We want to avoid object args as much as possible
            }
            else if (argType.IsSubclassOf(methodParam.ParameterType) )
            {
                // Count the distance in the inheritance

                System.Type currentType = argType;

                score = 0;
                while (currentType != methodParam.ParameterType && score < 20)
                {
                    score++;
                    currentType = currentType.BaseType;
                }
            }

            return score;
        }

        // Mostly copy paste of above adapted for just checking the types in `params` args
        private int ScoreMethodParamArgPair(System.Type methodParam, System.Type argType)
        {
            // This doesn't yet handle implicit user defined casts... there are probably other things this should handle too.
            int score = 1000000;

            if (methodParam == argType)
            {
                score = 0;
            }
            else if (methodParam.IsValidNumericImplicitCastTargetType() && argType.IsValidNumericImplictCastSourceType())
            {
                score = UdonSharpUtils.GetImplicitNumericCastDistance(methodParam, argType);
            }
            else if (methodParam == typeof(object))
            {
                score = 30; // We want to avoid object args as much as possible
            }
            else if (argType.IsSubclassOf(methodParam))
            {
                // Count the distance in the inheritance

                System.Type currentType = argType;

                score = 0;
                while (currentType != methodParam && score < 20)
                {
                    score++;
                    currentType = currentType.BaseType;
                }
            }

            return score;
        }

        public MethodBase FindBestOverloadFunction(MethodBase[] methods, List<System.Type> methodArgs, bool checkIfInUdon = true)
        {
            if (methods.Length == 0)
                throw new System.ArgumentException("Cannot find overload from 0 length method array");

            List<MethodBase> validMethods = new List<MethodBase>();

            foreach (MethodBase method in methods)
            {
                ParameterInfo[] methodParams = method.GetParameters();

                bool isMethodValid = true;

                for (int i = 0; i < methodParams.Length; ++i)
                {
                    ParameterInfo currentParam = methodParams[i];

                    // Check method arg count
                    if (i >= methodArgs.Count && !currentParam.HasDefaultValue)
                    {
                        isMethodValid = false;
                        break;
                    }
                    else if (currentParam.HasDefaultValue)
                    {
                        continue;
                    }

                    System.Type argType = methodArgs[i];

                    if (!currentParam.ParameterType.IsImplicitlyAssignableFrom(argType) && !currentParam.HasParamsParameter() && !currentParam.ParameterType.IsByRef)
                    {
                        // Handle implicit upcasts to int from lower precision types
                        if (method is OperatorMethodInfo operatorParam && 
                            (operatorParam.operatorType == BuiltinOperatorType.LeftShift || operatorParam.operatorType == BuiltinOperatorType.RightShift) &&
                            (argType != typeof(uint) && argType != typeof(ulong) && argType != typeof(long)))
                        {
                            if (UdonSharpUtils.GetNumericConversionMethod(currentParam.ParameterType, argType) == null)
                            {
                                isMethodValid = false;
                                break;
                            }
                        }
                        else
                        {
                            isMethodValid = false;
                            break;
                        }
                    }
                    else if (currentParam.HasParamsParameter()) // Make sure all params args can be assigned to the param type
                    {
                        if (!(currentParam.ParameterType.IsImplicitlyAssignableFrom(methodArgs[i]) && i == methodArgs.Count - 1)) // Handle passing in the actual array type for the params parameter
                        {
                            System.Type paramType = currentParam.ParameterType.GetElementType();

                            for (int j = i; j < methodArgs.Count; ++j)
                            {
                                if (!paramType.IsImplicitlyAssignableFrom(methodArgs[j]))
                                {
                                    isMethodValid = false;
                                    break;
                                }
                            }
                        }

                        break;
                    }
                    else if (currentParam.ParameterType.IsByRef) // ref/out params need to be exactly the same since they are passing in the actual variable
                    {
                        if (!currentParam.ParameterType.GetElementType().IsAssignableFrom(argType))
                        {
                            isMethodValid = false;
                            break;
                        }
                    }
                }

                // There are 0 method parameters but the user has passed in more than 0 arguments which is invalid
                if (methodParams.Length == 0 && methodArgs.Count > 0)
                {
                    isMethodValid = false;
                }

                // If we passed in more arguments than a normal function can take, and the last param isn't a `params` arg then the arguments can't fit into the method call
                if (methodParams.Length < methodArgs.Count && methodParams.Length > 0 && !methodParams.Last().HasParamsParameter())
                {
                    isMethodValid = false;
                }

                if (isMethodValid && (!checkIfInUdon || IsValidUdonMethod(GetUdonMethodName(method, false)))) // Only add methods that exist in Udon's context
                {
                    validMethods.Add(method);
                }
            }

            if (validMethods.Count == 0)
                return null;
            else if (validMethods.Count == 1) // There's only one option so just return it ez
                return validMethods.First();

            // Filter out duplicate methods
            // Still not sure if I want this or want to use it to highlight shortcomings in other areas
            //validMethods = validMethods.Distinct().ToList();

            //if (validMethods.Count == 1)
            //    return validMethods.First();

            // We found multiple potential overloads so we need to find the best one
            // See section 7.5.3.2 of the C# 5.0 language specification for the outline this search roughly follows, 
            //  there are some things it doesn't handle, and the "better" type checking is probably not quite the same.
            // Also the specification indicates that we need to do these checks on a function vs function basis until 1 remains.
            // This does not do that at the moment, it considers all remaining functions and classifies them as groups. 
            // There may be some cases where considering all functions vs all other functions would work better.
            // Roslyn does this more complex pair-based quadratic time check if you look at their PerformMemberOverloadResolution function in `OverloadResolution.cs` of the github

            // If there are non-generic forms of the method that match, use those
            int genericCount = 0, nonGenericCount = 0;
            foreach (MethodBase methodInfo in validMethods)
            {
                if (methodInfo.IsGenericMethod)
                    genericCount++;
                else
                    nonGenericCount++;
            }

            if (nonGenericCount > 0 && genericCount > 0)
                validMethods = validMethods.Where(e => !e.IsGenericMethod).ToList();

            if (validMethods.Count == 1)
                return validMethods.First();

            // Special case for UsonSharp operators. If we found a valid operator that's defined by the type, and an operator defined by UdonSharp, then use the operator defined on the type
            int normalOperatorCount = 0, udonSharpOperatorCount = 0;
            foreach (MethodBase methodInfo in validMethods)
            {
                if (methodInfo is OperatorMethodInfo)
                    udonSharpOperatorCount++;
                else
                    normalOperatorCount++;
            }

            if (normalOperatorCount > 0 && udonSharpOperatorCount > 0)
                validMethods = validMethods.Where(e => !(e is OperatorMethodInfo)).ToList();

            // Count the params using methods in this pass
            // todo: this still needs a chunk of work to handle when users don't pass anything for params along with handling default arguments before the params args, 
            //   but Udon doesn't expose any methods where this matters so it can wait till user methods need to support params and default arguments
            int paramsArgCount = 0, nonParamsArgCount = 0;
            foreach (MethodBase methodInfo in validMethods)
            {
                ParameterInfo[] methodParameters = methodInfo.GetParameters();

                if (methodParameters.Length > 0 &&
                    (methodParameters.Length > methodArgs.Count || !methodParameters.Last().ParameterType.IsImplicitlyAssignableFrom(methodArgs.Last())) && // Ignore params when the user is passing an array directly in for the params array
                    methodParameters.Last().HasParamsParameter())
                    paramsArgCount++;
                else
                    nonParamsArgCount++;
            }

            // If we have variants with `params` arguments and variants with normal arguments that fit requirements, then use the ones without the params
            if (paramsArgCount > 0 && nonParamsArgCount > 0)
            {
                validMethods = validMethods.Where(e => {
                    ParameterInfo[] parameters = e.GetParameters();
                    return parameters.Length == 0 || !parameters.Last().HasParamsParameter() || parameters.Last().ParameterType.IsImplicitlyAssignableFrom(methodArgs.Last());
                }).ToList();
            }

            if (validMethods.Count == 1)
                return validMethods.First();

            // Prefer methods that can be fully satisfied without default arguments
            int defaultArgMethodCount = 0, fullySatisfiedArgMethodCount = 0;
            foreach (MethodBase methodInfo in validMethods)
            {
                ParameterInfo[] methodParams = methodInfo.GetParameters();
                
                if (methodParams.Length > 0 && methodParams.Length > methodArgs.Count && methodParams.Last().HasDefaultValue)
                    defaultArgMethodCount++;
                else
                    fullySatisfiedArgMethodCount++;
            }

            if (defaultArgMethodCount > 0 && fullySatisfiedArgMethodCount > 0)
            {
                validMethods = validMethods.Where(e => {
                    ParameterInfo[] methodParams = e.GetParameters();
                    return methodParams.Length == 0 || !methodParams.Last().HasDefaultValue;
                }).ToList();
            }

            if (validMethods.Count == 1)
                return validMethods.First();

            // Now finally we try to find what has more specific types for the arguments
            List<MethodBase> exactTypeMatches = new List<MethodBase>();
            int nonExactTypeMatchCount = 0;

            foreach (MethodBase methodInfo in validMethods)
            {
                ParameterInfo[] methodParams = methodInfo.GetParameters();

                bool hasExactMatch = false;

                for (int i = 0; i < methodParams.Length; ++i)
                {
                    if (i > methodArgs.Count) // Can happen with default arguments, don't consider them as exact matches
                    {
                        break;
                    }

                    if (methodParams[i].ParameterType == methodArgs[i])
                    {
                        hasExactMatch = true;
                        break;
                    }
                }

                if (hasExactMatch)
                    exactTypeMatches.Add(methodInfo);
                else
                    nonExactTypeMatchCount++;
            }

            if (exactTypeMatches.Count > 0 && nonExactTypeMatchCount > 0)
                validMethods = exactTypeMatches;

            if (validMethods.Count == 1)
                return validMethods.First();

            // Remove methods if they have a more specific reflected type
            // This is mostly to remove ambiguity when we have multiple methods added manually from base types in places like HandleLocalUdonBehaviourMethodLookup()
            List<MethodBase> reflectedTypeMatches = new List<MethodBase>();

            foreach (MethodBase methodInfo in validMethods)
            {
                bool skipMethod = false;

                foreach (MethodBase checkedInfo in validMethods)
                {
                    if (methodInfo == checkedInfo)
                        continue;

                    if (methodInfo.AreMethodsEqualForDeclaringType(checkedInfo))
                    {
                        if (checkedInfo.ReflectedType.IsSubclassOf(methodInfo.ReflectedType))
                        {
                            skipMethod = true;
                            break;
                        }
                    }
                }

                if (skipMethod)
                    continue;

                reflectedTypeMatches.Add(methodInfo);
            }

            validMethods = reflectedTypeMatches;

            // Now start scoring which overrides are the "best"
            // A 0 score is the best, meaning it's a perfect match for all types
            // We will count how 'far' away a cast is if it's an implicit numeric cast. This is defined by the order of the cast types in implicitBuiltinConversions
            // For non-numeric conversions we count how far away a type is from the method parameter type in the given function.
            // For example, If we have BaseClassA -> InheretedClassB -> InheretedClassC, with an input argument type of InheretedClassC going to a method that takes a BaseClassA argument
            //  then we would score the type difference as 2 since it'd be 0 for an arg of BaseClassA and 1 for an arg of InheretedClassB
            // I don't think this is particularly great, but it should hopefully cover the majority of cases that Udon runs into
            // Using Roslyn to find the correct overload is an option since they have the function PerformMemberOverloadResolution, but it's all internal and built on internal types, 
            //  so it's a non-trivial thing to call into.

            List<System.Tuple<MethodBase, float>> scoredMethods = new List<System.Tuple<MethodBase, float>>();

            foreach (MethodBase methodInfo in validMethods)
            {
                ParameterInfo[] methodParams = methodInfo.GetParameters();

                int totalScore = 0;

                for (int i = 0; i < methodParams.Length; ++i)
                {
                    System.Type argType = i < methodArgs.Count ? methodArgs[i] : null;

                    if (!methodParams[i].HasParamsParameter() || methodParams[i].ParameterType.IsImplicitlyAssignableFrom(argType))
                    {
                        totalScore += ScoreMethodParamArgPair(methodParams[i], argType);
                    }
                    else
                    {
                        System.Type paramsArg = methodParams[i].ParameterType.GetElementType();

                        for (int j = i; j < methodArgs.Count; ++j)
                        {
                            totalScore += ScoreMethodParamArgPair(paramsArg, methodArgs[j]);
                        }
                    }
                }

                float finalScore = totalScore / (1f + methodParams.Length);

                scoredMethods.Add(new System.Tuple<MethodBase, float>(methodInfo, finalScore));
            }

            scoredMethods = scoredMethods.OrderBy(e => e.Item2).ToList();

            //Debug.Log("Scoring");
            //foreach (var scoredMethod in scoredMethods)
            //    Debug.Log($"Score: {scoredMethod.Item2},{scoredMethod.Item1}");

            float minimumScore = scoredMethods.First().Item2;

            List<MethodBase> ambiguousMethods = new List<MethodBase>();

            for (int i = 1; i < scoredMethods.Count; ++i)
            {
                if (scoredMethods[i].Item2 == minimumScore) // Oh no there's still ambiguity! Gather the ambiguous functions and throw an exception.
                {
                    ambiguousMethods.Add(scoredMethods[i].Item1);
                }
            }

            if (ambiguousMethods.Count > 0)
            {
                ambiguousMethods.Add(scoredMethods.First().Item1);

                string methodListString = "";

                foreach (MethodBase methodInfo in ambiguousMethods)
                    methodListString += $"{methodInfo.DeclaringType}: {methodInfo}\n";

                throw new System.Exception("Ambiguous method overload reference, candidate methods:\n" + methodListString);
            }

            return scoredMethods.First().Item1;
        }

        public bool ValidateUdonTypeName(string typeName, UdonReferenceType referenceType)
        {
            typeName = typeName.Replace("VRCUdonUdonBehaviour", "VRCUdonCommonInterfacesIUdonEventReceiver");

            switch (referenceType)
            {
                case UdonReferenceType.Const:
                    typeName = $"Const_{typeName}";
                    break;
                case UdonReferenceType.Type:
                    typeName = $"Type_{typeName}";
                    break;
                case UdonReferenceType.Variable:
                    typeName = $"Variable_{typeName}";
                    break;
                default:
                    break;
            }

            return nodeDefinitionLookup.Contains(typeName);
        }

        public bool IsValidUdonType(System.Type type)
        {
            string udonTypeName = GetUdonTypeName(type);

            bool isUserDefinedType = UdonSharpUtils.IsUserDefinedType(type);

            if (!ValidateUdonTypeName(udonTypeName, UdonReferenceType.Variable) &&
                !ValidateUdonTypeName(udonTypeName, UdonReferenceType.Type) &&
                //VRC.Udon.Editor.UdonEditorManager.Instance.GetTypeFromTypeString(udonTypeName) != null && // I'd assume that this should work instead of the ValidateUdonTypeName calls, but it doesn't pick up a bunch of types
                !isUserDefinedType)
                return false;

            return true;
        }
    }
}