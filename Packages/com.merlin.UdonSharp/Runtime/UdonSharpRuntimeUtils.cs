﻿using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("UdonSharp.Lib")]

namespace UdonSharp
{
    namespace Internal
    {
        public static class UdonSharpInternalUtility
        {
            public static long GetTypeID(System.Type type)
            {
                return GetTypeID(type.FullName);
            }
            
            public static long GetTypeID(string typeName)
            {
                SHA256 typeHash = new SHA256CryptoServiceProvider();
                byte[] hash = typeHash.ComputeHash(Encoding.UTF8.GetBytes(typeName));
                return BitConverter.ToInt64(hash, 0);
            }

            public static string GetTypeName(System.Type type)
            {
                return type.Name;
            }
            
            public static bool IsUserDefinedType<T>()
            {
                throw new InvalidOperationException("This method can only be called in the Udon runtime");
            }
            
            /// <summary>
            /// Checks if the type T is a user-defined type with an overridden Equals method.
            /// </summary>
            public static bool IsUserDefinedTypeWithEquals<T>()
            {
                throw new InvalidOperationException("This method can only be called in the Udon runtime");
            }
        }
    }

#if false
    public static class UdonSharpUtility
    {
        public static long GetTypeID<T>()
        {
            return Internal.UdonSharpInternalUtility.GetTypeID(typeof(T));
        }

        // These may be extended in the future to handle the edge cases with type names
        public static string GetTypeName(System.Type type)
        {
            return Internal.UdonSharpInternalUtility.GetTypeID(type);
        }

        //public static string GetTypeNamespace(System.Type type)
        //{
        //    return type.Namespace;
        //}

        // Placeholder stubs, won't give valid info unless used in the Udon runtime
        public static int GetUdonScriptVersion()
        {
            return 0;
        }

        public static System.DateTime GetLastCompileDate()
        {
            return System.DateTime.Now;
        }

        public static string GetCompilerVersionString()
        {
            return "v0.0.0+0";
        }

        // Just assume people are on the correct runtime version for Udon, since other runtimes won't compile anyways
        public static string GetCompilerName()
        {
            return "Roslyn C# compiler";
        }

        public static int GetCompilerMajorVersion()
        {
            return 0;
        }

        public static int GetCompilerMinorVersion()
        {
            return 0;
        }

        public static int GetCompilerPatchVersion()
        {
            return 0;
        }

        public static int GetCompilerBuild()
        {
            return 0;
        }
    }
#endif
}
