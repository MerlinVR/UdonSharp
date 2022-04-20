
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UdonSharp.Compiler.Udon;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace UdonSharp
{
    internal static class UdonSharpUtils
    {
        // https://stackoverflow.com/questions/6386202/get-type-name-without-any-generics-info
        public static string GetNameWithoutGenericArity(this System.Type t)
        {
            if (!t.IsGenericType)
                return t.Name;

            string name = t.Name;
            int index = name.IndexOf('`');
            return index == -1 ? name : name.Substring(0, index);
        }

        private static readonly HashSet<System.Type> _unsignedTypes = new HashSet<System.Type>()
        {
            typeof(byte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
        };

        private static readonly HashSet<System.Type> _signedTypes = new HashSet<System.Type>()
        {
            typeof(sbyte),
            typeof(short),
            typeof(int),
            typeof(long),
        };

        private static readonly HashSet<System.Type> _integerTypes = new HashSet<System.Type>()
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
        };

        private static readonly HashSet<System.Type> _floatTypes = new HashSet<System.Type>()
        {
            typeof(float),
            typeof(double),
            typeof(decimal),
        };

        public static bool IsSignedType(System.Type type)
        {
            return _signedTypes.Contains(type);
        }

        public static bool IsUnsignedType(System.Type type)
        {
            return _unsignedTypes.Contains(type);
        }

        public static bool IsIntegerType(System.Type type)
        {
            return _integerTypes.Contains(type);
        }

        public static bool IsFloatType(System.Type type)
        {
            return _floatTypes.Contains(type);
        }

        public static bool IsNumericType(System.Type type)
        {
            return IsIntegerType(type) || IsFloatType(type);
        }

        public static bool IsExplicitlyAssignableFrom(this System.Type targetType, System.Type assignee)
        {
            // Normal explicit assign
            if (targetType.IsAssignableFrom(assignee))
                return true;

            // Numeric conversions
            if (IsNumericType(targetType) && IsNumericType(assignee))
                return true;

            // Handle user-defined implicit conversion operators defined on both sides
            // Roughly follows https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#processing-of-user-defined-implicit-conversions

            // I doubt I'll ever deal with properly supporting nullable but ¯\_(ツ)_/¯
            if (System.Nullable.GetUnderlyingType(targetType) != null)
                targetType = System.Nullable.GetUnderlyingType(targetType);
            if (System.Nullable.GetUnderlyingType(assignee) != null)
                assignee = System.Nullable.GetUnderlyingType(assignee);

            List<System.Type> operatorTypes = new List<System.Type>();
            operatorTypes.Add(targetType);

            System.Type currentSourceType = assignee;
            while (currentSourceType != null)
            {
                operatorTypes.Add(currentSourceType);
                currentSourceType = currentSourceType.BaseType;
            }

            foreach (System.Type operatorType in operatorTypes)
            {
                IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Implicit");

                foreach (MethodInfo methodInfo in methods)
                {
                    if (methodInfo.ReturnType == targetType && (methodInfo.GetParameters()[0].ParameterType == assignee || methodInfo.GetParameters()[0].ParameterType == typeof(UnityEngine.Object)))
                        return true;
                }
            }

            foreach (System.Type operatorType in operatorTypes)
            {
                IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Explicit");

                foreach (MethodInfo methodInfo in methods)
                {
                    if (methodInfo.ReturnType == targetType && (methodInfo.GetParameters()[0].ParameterType == assignee || methodInfo.GetParameters()[0].ParameterType == typeof(UnityEngine.Object)))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Escapes property names into something Udon allows since C# will put '<' and '>' in property backing field names
        /// </summary>
        internal static string UnmanglePropertyFieldName(string propertyName)
        {
            return propertyName?.Replace('<', '_').Replace('>', '_');
        }

        public static bool IsUserDefinedBehaviour(System.Type type)
        {
            return type == typeof(UdonSharpBehaviour) ||
                   type.IsSubclassOf(typeof(UdonSharpBehaviour)) ||
                  (type.IsArray && (type.GetElementType() == typeof(UdonSharpBehaviour) || type.GetElementType().IsSubclassOf(typeof(UdonSharpBehaviour)) ||
                                    type.GetElementType() == typeof(UdonBehaviour) || type.GetElementType().IsSubclassOf(typeof(UdonBehaviour))));
        }
        
        public static bool IsUserJaggedArray(System.Type type)
        {
            return type.IsArray && type.GetElementType().IsArray;
        }

        public static bool IsUserDefinedEnum(System.Type type)
        {
            return (type.IsEnum && !CompilerUdonInterface.IsExternType(type)) || 
                   (type.IsArray && type.GetElementType().IsEnum && !CompilerUdonInterface.IsExternType(type.GetElementType()));
        }

        public static bool IsUserDefinedType(System.Type type)
        {
            return IsUserDefinedBehaviour(type) ||
                   IsUserJaggedArray(type) ||
                   IsUserDefinedEnum(type);
        }
        
        private static Dictionary<Type, Type> _inheritedTypeMap;
        private static readonly object _inheritedTypeMapLock = new object();

        private static Dictionary<Type, Type> GetInheritedTypeMap()
        {
            if (_inheritedTypeMap != null)
                return _inheritedTypeMap;
            
            lock (_inheritedTypeMapLock)
            {
                if (_inheritedTypeMap != null)
                    return _inheritedTypeMap;
                
                Dictionary<Type, Type> typeMap = new Dictionary<Type, Type>();

                IEnumerable<Type> typeList = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "VRCSDK3").GetTypes().Where(t => t != null && t.Namespace != null && t.Namespace.StartsWith("VRC.SDK3.Components"));

                foreach (Type childType in typeList)
                {
                    if (childType.BaseType != null && childType.BaseType.Namespace.StartsWith("VRC.SDKBase"))
                    {
                        typeMap.Add(childType.BaseType, childType);
                    }
                }

                typeMap.Add(typeof(VRC.SDK3.Video.Components.VRCUnityVideoPlayer), typeof(VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer));
                typeMap.Add(typeof(VRC.SDK3.Video.Components.AVPro.VRCAVProVideoPlayer), typeof(VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer));

                _inheritedTypeMap = typeMap;
            }

            return _inheritedTypeMap;
        }

        internal static Type RemapBaseType(Type type)
        {
            Dictionary<Type, Type> typeMap = GetInheritedTypeMap();

            int arrayDepth = 0;
            Type currentType = type;
            while (currentType.IsArray)
            {
                currentType = currentType.GetElementType();
                ++arrayDepth;
            }

            if (typeMap.ContainsKey(currentType))
            {
                type = typeMap[currentType];

                while (arrayDepth-- > 0)
                    type = type.MakeArrayType();
            }

            return type;
        }
        
        [ThreadStatic]
        private static Dictionary<Type, Type> _userTypeToUdonTypeCache;

        public static Type UserTypeToUdonType(Type type)
        {
            if (type == null)
                return null;

            if (_userTypeToUdonTypeCache == null)
                _userTypeToUdonTypeCache = new Dictionary<Type, Type>();

            if (_userTypeToUdonTypeCache.TryGetValue(type, out Type foundType))
                return foundType;
            
            Type udonType = null;

            if (IsUserDefinedType(type))
            {
                if (type.IsArray)
                {
                    if (!type.GetElementType().IsArray)
                    {
                        if (IsUserDefinedEnum(type))
                            udonType = type.GetElementType().GetEnumUnderlyingType().MakeArrayType();
                        else
                            udonType = typeof(UnityEngine.Component[]);// Hack because VRC doesn't expose the array type of UdonBehaviour
                    }
                    else // Jagged arrays
                    {
                        udonType = typeof(object[]);
                    }
                }
                else
                {
                    udonType = typeof(VRC.Udon.UdonBehaviour);
                }
            }

            if (udonType == null)
                udonType = type;

            udonType = RemapBaseType(udonType);
            
            _userTypeToUdonTypeCache.Add(type, udonType);

            return udonType;
        }

        public static void LogBuildError(string message, string filePath, int line, int character)
        {
            MethodInfo buildErrorLogMethod = typeof(Debug).GetMethod("LogPlayerBuildError", BindingFlags.NonPublic | BindingFlags.Static);

            string errorMessage = $"[<color=#FF00FF>UdonSharp</color>] {filePath}({line},{character}): {message}";

            buildErrorLogMethod.Invoke(null, new object[] {
                        errorMessage,
                        filePath,
                        line,
                        character });
        }

        public static void Log(object message)
        {
            Debug.Log($"[<color=#0c824c>UdonSharp</color>] {message}");
        }
        
        public static void Log(object message, UnityEngine.Object context)
        {
            Debug.Log($"[<color=#0c824c>UdonSharp</color>] {message}", context);
        }
        
        public static void LogWarning(object message)
        {
            Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] {message}");
        }
        
        public static void LogWarning(object message, UnityEngine.Object context)
        {
            Debug.LogWarning($"[<color=#FF00FF>UdonSharp</color>] {message}", context);
        }
        
        public static void LogError(object message)
        {
            Debug.LogError($"[<color=#FF00FF>UdonSharp</color>] {message}");
        }
        
        public static void LogError(object message, UnityEngine.Object context)
        {
            Debug.LogError($"[<color=#FF00FF>UdonSharp</color>] {message}", context);
        }

        private static readonly MethodInfo _displayProgressBar = typeof(Editor).Assembly.GetTypes().FirstOrDefault(e => e.Name == "AsyncProgressBar")?.GetMethod("Display");
        private static readonly MethodInfo _clearProgressBar = typeof(Editor).Assembly.GetTypes().FirstOrDefault(e => e.Name == "AsyncProgressBar")?.GetMethod("Clear");
        
        public static void ShowAsyncProgressBar(string text, float progress)
        {
            _displayProgressBar.Invoke(null, new object[] {text, progress});
        }

        public static void ClearAsyncProgressBar()
        {
            _clearProgressBar.Invoke(null, null);
        }

        public static void LogRuntimeError(string message, string prefix, string filePath, int line, int character)
        {
            MethodInfo buildErrorLogMethod = typeof(UnityEngine.Debug).GetMethod("LogPlayerBuildError", BindingFlags.NonPublic | BindingFlags.Static);

            string errorMessage = $"[<color=#FF00FF>UdonSharp</color>]{prefix} {filePath}({line + 1},{character}): {message}";

            buildErrorLogMethod.Invoke(null, new object[] {
                        errorMessage,
                        filePath,
                        line + 1,
                        character });
        }

        public static string ReadFileTextSync(string filePath, float timeoutSeconds = 2f)
        {
            bool sourceLoaded = false;

            string fileText = "";

            DateTime startTime = DateTime.Now;

            while (true)
            {
                System.IO.IOException exception = null;

                try
                {
                    fileText = System.IO.File.ReadAllText(filePath);
                    sourceLoaded = true;
                }
                catch (System.IO.IOException e)
                {
                    exception = e;

                    if (e is System.IO.FileNotFoundException ||
                        e is System.IO.DirectoryNotFoundException)
                        throw;
                }

                if (sourceLoaded)
                    break;
                
                System.Threading.Thread.Sleep(20);

                TimeSpan timeFromStart = DateTime.Now - startTime;

                if (timeFromStart.TotalSeconds > timeoutSeconds)
                {
                    Debug.LogError($"Timeout when attempting to read file {filePath}");
                    if (exception != null)
                        throw exception;
                }
            }

            return fileText;
        }

        internal static string HashString(string stringToHash)
        {
            using (SHA1Managed sha256 = new SHA1Managed())
            {
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(stringToHash))).Replace("-", "");
            }
        }
        
        internal static bool AllElementsMatch<T>(IEnumerable<T> collection)
        {
            IEnumerable<T> enumerable = collection as T[] ?? collection.ToArray();
            if (!enumerable.Any())
                return true;

            T firstValue = enumerable.First();

            return enumerable.All(e => e.Equals(firstValue));
        }

        /// <summary>
        /// Returns if a normal System.Object is null, and handles when a UnityEngine.Object referenced as a System.Object is null
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static bool IsUnityObjectNull(this object value)
        {
            if (value == null)
                return true;

            if (value is UnityEngine.Object unityEngineObject && unityEngineObject == null)
                return true;

            return false;
        }

        internal static string[] GetProjectDefines(bool editorBuild)
        {
            List<string> defines = new List<string>();

            foreach (string define in UnityEditor.EditorUserBuildSettings.activeScriptCompilationDefines)
            {
                if (!editorBuild)
                    if (define.StartsWith("UNITY_EDITOR"))
                        continue;

                defines.Add(define);
            }

            defines.Add("COMPILER_UDONSHARP");

            return defines.ToArray();
        }

        internal static void ShowEditorNotification(string notificationString)
        {
            typeof(SceneView).GetMethod("ShowNotification", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { notificationString });
        }

        public static bool DoesUnityProjectHaveCompileErrors()
        {
            Type logEntryType = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo getLinesAndModeMethod = logEntryType.GetMethod("GetLinesAndModeFromEntryInternal", BindingFlags.Public | BindingFlags.Static);
            
            bool hasCompileError = false;
            
            int logEntryCount = (int)logEntryType.GetMethod("StartGettingEntries", BindingFlags.Public | BindingFlags.Static).Invoke(null, Array.Empty<object>());

            try
            {
                object[] getLinesParams = { 0, 1, 0, "" };

                for (int i = 0; i < logEntryCount; ++i)
                {
                    getLinesParams[0] = i;
                    getLinesAndModeMethod.Invoke(null, getLinesParams);

                    int mode = (int)getLinesParams[2];

                    // 1 << 11 == ConsoleWindow.Mode.ScriptCompileError
                    if ((mode & (1 << 11)) != 0)
                    {
                        hasCompileError = true;
                        break;
                    }
                }
            }
            finally
            {
                logEntryType.GetMethod("EndGettingEntries").Invoke(null, Array.Empty<object>());
            }

            return hasCompileError;
        }

        internal static void SetDirty(UnityEngine.Object obj)
        {
            EditorUtility.SetDirty(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        private static PropertyInfo _getLoadedAssembliesProp;

        internal static IEnumerable<Assembly> GetLoadedEditorAssemblies()
        {
            if (_getLoadedAssembliesProp == null)
            {
                Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(e => e.GetName().Name == "UnityEditor");
                Type editorAssembliesType = editorAssembly.GetType("UnityEditor.EditorAssemblies");
                _getLoadedAssembliesProp = editorAssembliesType.GetProperty("loadedAssemblies", BindingFlags.Static | BindingFlags.NonPublic);
            }

            return (Assembly[])_getLoadedAssembliesProp.GetValue(null);
        }

        /// <summary>
        /// Used to prevent Odin's DefaultSerializationBinder from getting a callback to register an assembly in specific cases where it will explode due to https://github.com/mono/mono/issues/20968
        /// </summary>
        internal class UdonSharpAssemblyLoadStripScope : IDisposable
        {
            Delegate[] originalDelegates;

            public UdonSharpAssemblyLoadStripScope()
            {
                FieldInfo info = AppDomain.CurrentDomain.GetType().GetField("AssemblyLoad", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);

                AssemblyLoadEventHandler handler = info.GetValue(AppDomain.CurrentDomain) as AssemblyLoadEventHandler;

                originalDelegates = handler?.GetInvocationList();

                if (originalDelegates != null)
                {
                    foreach (Delegate del in originalDelegates)
                        AppDomain.CurrentDomain.AssemblyLoad -= (AssemblyLoadEventHandler)del;
                }
            }

            public void Dispose()
            {
                if (originalDelegates != null)
                {
                    foreach (Delegate del in originalDelegates)
                        AppDomain.CurrentDomain.AssemblyLoad += (AssemblyLoadEventHandler)del;
                }
            }
        }
    }
}
