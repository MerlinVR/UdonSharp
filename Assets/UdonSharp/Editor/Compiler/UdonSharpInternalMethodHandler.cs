using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UdonSharp.Compiler
{
    public class InternalMethodHandler
    {
        enum InternalFunc
        {
            None,
            // Static functions
            MajorVersion,
            MinorVersion,
            PatchVersion,
            Build,
            VersionStr,
            TypeIDGeneric,
            TypeNameGeneric,
            ScriptVersion,
            ScriptCompileDate,
            ScriptCompilerName,
            // Instance functions
            TypeIDInstance,
            TypeNameInstance,
        }

        const int UDON_SHARP_MAJOR_VERSION = 0;
        const int UDON_SHARP_MINOR_VERSION = 8;
        const int UDON_SHARP_PATCH_VERSION = 3;
        const int UDON_SHARP_BUILD = 29;
        static readonly string UDON_SHARP_VERSION_STR = $"v{UDON_SHARP_MAJOR_VERSION}.{UDON_SHARP_MINOR_VERSION}.{UDON_SHARP_PATCH_VERSION}+{UDON_SHARP_BUILD}";

        ASTVisitorContext visitorContext;
        ExpressionCaptureScope captureScope;

        InternalFunc captureFunc = InternalFunc.None;
        System.Type genericType = null;

        public InternalMethodHandler(ASTVisitorContext visitorContextIn, ExpressionCaptureScope captureScopeIn)
        {
            visitorContext = visitorContextIn;
            captureScope = captureScopeIn;
        }

        public bool ResolveAccessToken(string accessToken)
        {
            if (captureScope.captureArchetype == ExpressionCaptureArchetype.Type)
            {
                return false;

#if false
                if (captureScope.captureType != typeof(UdonSharpUtility))
                    return false;

                switch (accessToken)
                {
                    case nameof(UdonSharpUtility.GetCompilerMajorVersion):
                        captureFunc = InternalFunc.MajorVersion;
                        break;
                    case nameof(UdonSharpUtility.GetCompilerMinorVersion):
                        captureFunc = InternalFunc.MinorVersion;
                        break;
                    case nameof(UdonSharpUtility.GetCompilerPatchVersion):
                        captureFunc = InternalFunc.PatchVersion;
                        break;
                    case nameof(UdonSharpUtility.GetCompilerBuild):
                        captureFunc = InternalFunc.Build;
                        break;
                    case nameof(UdonSharpUtility.GetCompilerVersionString):
                        captureFunc = InternalFunc.VersionStr;
                        break;
                    case nameof(UdonSharpUtility.GetTypeID):
                        captureFunc = InternalFunc.TypeIDGeneric;
                        break;
                    case nameof(UdonSharpUtility.GetUdonScriptVersion):
                        captureFunc = InternalFunc.ScriptVersion;
                        break;
                    case nameof(UdonSharpUtility.GetLastCompileDate):
                        captureFunc = InternalFunc.ScriptCompileDate;
                        break;
                    case nameof(UdonSharpUtility.GetCompilerName):
                        captureFunc = InternalFunc.ScriptCompilerName;
                        break;
                    default:
                        captureFunc = InternalFunc.None;
                        return false;
                }

                return captureFunc != InternalFunc.None;
#endif
            }
            else if (captureScope.captureArchetype == ExpressionCaptureArchetype.Unknown || 
                     captureScope.captureArchetype == ExpressionCaptureArchetype.This ||
                     captureScope.captureArchetype == ExpressionCaptureArchetype.LocalSymbol ||
                     captureScope.captureArchetype == ExpressionCaptureArchetype.Property ||
                     captureScope.captureArchetype == ExpressionCaptureArchetype.Field ||
                     captureScope.captureArchetype == ExpressionCaptureArchetype.ExternUserField ||
                     captureScope.captureArchetype == ExpressionCaptureArchetype.ArrayIndexer)
            {
                captureFunc = InternalFunc.None;

                switch (accessToken)
                {
                    case nameof(UdonSharpBehaviour.GetUdonTypeID):
                        captureFunc = InternalFunc.TypeIDInstance;
                        break;
                    case nameof(UdonSharpBehaviour.GetUdonTypeName):
                        captureFunc = InternalFunc.TypeNameInstance;
                        break;
                }

                if (captureFunc == InternalFunc.None)
                    return false;

                return true;
            }

            return false;
        }

        public void HandleGenericAccess(List<System.Type> genericTypeArgs)
        {
            if (captureFunc == InternalFunc.TypeIDInstance)
            {
                captureFunc = InternalFunc.TypeIDGeneric;
            }
            else if (captureFunc == InternalFunc.TypeNameInstance)
            {
                captureFunc = InternalFunc.TypeNameGeneric;
            }
            else
            {
                throw new System.ArgumentException($"Cannot call generic internal function {captureFunc}");
            }

            genericType = genericTypeArgs.First();
        }

        public SymbolDefinition Invoke(SymbolDefinition[] invokeParams)
        {
            SymbolDefinition methodResult = null;

            System.Type resultSymbolType = null;
            string lookupSymbolName = null;

            switch (captureFunc)
            {
                case InternalFunc.TypeIDInstance:
                case InternalFunc.TypeIDGeneric:
                    resultSymbolType = typeof(long);
                    lookupSymbolName = "udonTypeID";
                    break;
                case InternalFunc.TypeNameInstance:
                case InternalFunc.TypeNameGeneric:
                    resultSymbolType = typeof(string);
                    lookupSymbolName = "udonTypeName";
                    break;
                default:
                    throw new System.ArgumentException("Invalid internal method invocation");
            }

            methodResult = visitorContext.topTable.CreateUnnamedSymbol(resultSymbolType, SymbolDeclTypeFlags.Internal);

            if (captureFunc == InternalFunc.TypeIDInstance ||
                captureFunc == InternalFunc.TypeNameInstance)
            {
                SymbolDefinition invokeSymbol = captureScope.accessSymbol;

                using (ExpressionCaptureScope resultSetterScope = new ExpressionCaptureScope(visitorContext, null))
                {
                    resultSetterScope.SetToLocalSymbol(methodResult);

                    using (ExpressionCaptureScope getInvokeScope = new ExpressionCaptureScope(visitorContext, null))
                    {
                        getInvokeScope.SetToLocalSymbol(invokeSymbol);
                        getInvokeScope.ResolveAccessToken(nameof(VRC.Udon.UdonBehaviour.GetProgramVariable));

                        string symbolName = visitorContext.topTable.GetReflectionSymbol(lookupSymbolName, resultSymbolType).symbolUniqueName;

                        SymbolDefinition invokeResult = getInvokeScope.Invoke(new SymbolDefinition[] { visitorContext.topTable.CreateConstSymbol(typeof(string), symbolName) });
                        
                        JumpLabel exitBranchJump = visitorContext.labelTable.GetNewJumpLabel("exitUdonTypeIdLoc");
                        JumpLabel falseBranchLoc = visitorContext.labelTable.GetNewJumpLabel("falseUdonTypeIdLoc");

                        SymbolDefinition nullCheckSymbol = null;

                        using (ExpressionCaptureScope nullCheckCondition = new ExpressionCaptureScope(visitorContext, null))
                        {
                            nullCheckCondition.SetToMethods(UdonSharpUtils.GetOperators(typeof(object), BuiltinOperatorType.Inequality));
                            nullCheckSymbol = nullCheckCondition.Invoke(new SymbolDefinition[] { invokeResult, visitorContext.topTable.CreateConstSymbol(typeof(object), null) });
                        }

                        visitorContext.uasmBuilder.AddJumpIfFalse(falseBranchLoc, nullCheckSymbol);

                        resultSetterScope.ExecuteSet(captureScope.CastSymbolToType(invokeResult, resultSymbolType, true));
                        visitorContext.uasmBuilder.AddJump(exitBranchJump);

                        // If the value is null
                        visitorContext.uasmBuilder.AddJumpLabel(falseBranchLoc);

                        if (captureFunc == InternalFunc.TypeIDInstance)
                            resultSetterScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(long), 0L));
                        else
                            resultSetterScope.ExecuteSet(visitorContext.topTable.CreateConstSymbol(typeof(string), "UnknownType"));

                        visitorContext.uasmBuilder.AddJumpLabel(exitBranchJump);
                    }
                }
            }
            else
            {
                object resultSymbolValue = null;

                if (captureFunc == InternalFunc.TypeIDGeneric)
                    resultSymbolValue = Internal.UdonSharpInternalUtility.GetTypeID(genericType);
                else if (captureFunc == InternalFunc.TypeNameGeneric)
                    resultSymbolValue = Internal.UdonSharpInternalUtility.GetTypeName(genericType);

                methodResult = visitorContext.topTable.CreateConstSymbol(resultSymbolType, resultSymbolValue);
            }

            using (ExpressionCaptureScope propagateScope = new ExpressionCaptureScope(visitorContext, visitorContext.topCaptureScope))
            {
                propagateScope.SetToLocalSymbol(methodResult);
            }

            return methodResult;
        }
    }
}
