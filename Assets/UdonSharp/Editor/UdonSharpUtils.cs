
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UdonSharp
{
    public static class UdonSharpUtils
    {
        /// <summary>
        /// Apparently anything that takes a parameter is 5, and anything that doesn't is 1. So These are probably 1 byte per instruction, and 4 bytes per parameter
        /// So some day if the assembly is extended we might get 9 size instructions
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        public static int GetUdonInstructionSize(string instruction)
        {
            switch (instruction)
            {
                case "LABEL": // The LABEL instruction gets replaced with a NOP by the end of compilation. It is just used to mark a target jump point
                case "NOP":
                case "POP":
                case "COPY":
                    return 1;
                case "PUSH":
                case "JUMP_IF_FALSE":
                case "JUMP":
                case "EXTERN":
                case "JUMP_INDIRECT":
                // The labeled variants of jump are fake and don't exist in Udon, 
                //  they get replaced towards the end of compilation with jumps to concrete addresses
                case "JUMP_LABEL":
                case "JUMP_IF_FALSE_LABEL":
                    return 5;
                case "ANNOTATION":
                    throw new System.NotImplementedException("ANNOTATION instruction is not yet implemented in Udon");
                default:
                    return 0;
            }
        }

        // https://stackoverflow.com/questions/6386202/get-type-name-without-any-generics-info
        public static string GetNameWithoutGenericArity(this System.Type t)
        {
            if (!t.IsGenericType)
                return t.Name;

            string name = t.Name;
            int index = name.IndexOf('`');
            return index == -1 ? name : name.Substring(0, index);
        }

        public static bool HasModifier(this SyntaxTokenList syntaxTokens, string modifier)
        {
            foreach (SyntaxToken token in syntaxTokens)
            {
                if (token.ValueText == modifier)
                    return true;
            }

            return false;
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#implicit-numeric-conversions
        private static readonly IReadOnlyDictionary<System.Type, System.Type[]> implicitBuiltinConversions = new Dictionary<System.Type, System.Type[]>()
        {
            { typeof(sbyte), new System.Type[] { typeof(short), typeof(int), typeof(long), typeof(double), typeof(decimal) } },
            { typeof(byte), new System.Type[] { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) } },
            { typeof(short), new System.Type[] { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) } },
            { typeof(ushort), new System.Type[] { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) } },
            { typeof(int), new System.Type[] { typeof(long), typeof(float), typeof(double), typeof(decimal) } },
            { typeof(uint), new System.Type[] { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) } },
            { typeof(long), new System.Type[] { typeof(float), typeof(double), typeof(decimal) } },
            { typeof(ulong), new System.Type[] { typeof(float), typeof(double), typeof(decimal) } },
            { typeof(char), new System.Type[] { typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(float), typeof(decimal) } },
            { typeof(float), new System.Type[] { typeof(double) } },
        };

        public static bool IsNumericImplicitCastValid(System.Type targetType, System.Type sourceType)
        {
            if (implicitBuiltinConversions.ContainsKey(sourceType) && implicitBuiltinConversions[sourceType].Contains(targetType))
                return true;

            return false;
        }

        public static MethodInfo GetNumericConversionMethod(System.Type targetType, System.Type sourceType)
        {
            MethodInfo[] foundMethods = typeof(System.Convert)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(e => e.Name == $"To{targetType.Name}")
                .Where(e => e.GetParameters().FirstOrDefault().ParameterType == sourceType).ToArray();

            if (foundMethods.Length > 0)
                return foundMethods[0];

            return null;
        }

        public static bool IsImplicitlyAssignableFrom(this System.Type targetType, System.Type assignee)
        {
            // Normal explicit assign
            if (targetType.IsAssignableFrom(assignee))
                return true;

            // Implicit numeric conversions
            if (IsNumericImplicitCastValid(targetType, assignee))
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
            while(currentSourceType != null)
            {
                operatorTypes.Add(currentSourceType);
                currentSourceType = currentSourceType.BaseType;
            }

            foreach (System.Type operatorType in operatorTypes)
            {
                IEnumerable<MethodInfo> methods = operatorType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "op_Implicit");

                foreach (MethodInfo methodInfo in methods)
                {
                    if (methodInfo.ReturnType == targetType && methodInfo.GetParameters()[0].ParameterType == assignee)
                        return true;
                }
            }

            return false;
        }

        public static bool IsValidNumericImplictCastSourceType(this System.Type sourceType)
        {
            return implicitBuiltinConversions.ContainsKey(sourceType);
        }

        public static bool IsValidNumericImplicitCastTargetType(this System.Type targetType)
        {
            foreach (var lookupKeyVal in implicitBuiltinConversions)
            {
                foreach (System.Type testTargetType in lookupKeyVal.Value)
                {
                    if (targetType == testTargetType)
                        return true;
                }
            }

            return false;
        }

        public static int GetImplicitNumericCastDistance(System.Type targetType, System.Type sourceType)
        {
            if (targetType == sourceType)
                return 0;

            System.Type[] targetTypes;

            if (!implicitBuiltinConversions.TryGetValue(sourceType, out targetTypes))
            {
                throw new System.ArgumentException("Could not find a implicit numeric cast for the source type");
            }

            for (int i = 0; i < targetTypes.Length; ++i)
            {
                if (targetTypes[i] == targetType)
                    return i + 1;
            }

            throw new System.ArgumentException("Could not find applicable cast for target type");
        }

        public static bool HasParamsParameter(this ParameterInfo parameterInfo)
        {
            return parameterInfo.GetCustomAttributes(typeof(System.ParamArrayAttribute), false).Length > 0;
        }
        
        // https://stackoverflow.com/questions/4168489/methodinfo-equality-for-declaring-type
        public static bool AreMethodsEqualForDeclaringType(this MethodBase first, MethodBase second)
        {
            first = first.ReflectedType == first.DeclaringType ? first : first.DeclaringType.GetMethod(first.Name, first.GetParameters().Select(p => p.ParameterType).ToArray());
            second = second.ReflectedType == second.DeclaringType ? second : second.DeclaringType.GetMethod(second.Name, second.GetParameters().Select(p => p.ParameterType).ToArray());
            return first == second;
        }

        private static readonly HashSet<System.Type> udonSyncTypes = new HashSet<System.Type>()
        {
            typeof(bool), // bool is apparently broken despite being listed as supported, not sure if it has been fixed yet.
            typeof(char),
            typeof(byte), typeof(sbyte),
            typeof(int), typeof(uint),
            typeof(long),
            typeof(float), typeof(double),
            typeof(short), typeof(ushort),
        };

        public static bool IsUdonSyncedType(System.Type type)
        {
            return udonSyncTypes.Contains(type);
        }
        
        public static void LogBuildError(string message, string filePath, int line, int character)
        {
            MethodInfo buildErrorLogMethod = typeof(UnityEngine.Debug).GetMethod("LogPlayerBuildError", BindingFlags.NonPublic | BindingFlags.Static);

            buildErrorLogMethod.Invoke(null, new object[] {
                        $"[UdonSharp] {message}",
                        filePath,
                        line + 1,
                        character });
        }
    }
}
