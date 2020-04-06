using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;

namespace UdonSharp
{
    [Flags]
    public enum SymbolDeclTypeFlags
    {
        Public = 1, // Declared by the user as a public variable on a class
        Private = 2, // Declared by the user as a private variable on a class
        Local = 4, // Declared by the user as a variable local to a specific scope
        Internal = 8, // Generated as an intermediate variable that stores intermediate calculations
        Constant = 16, // Used to represent a constant value that does not change. This can either be statically defined constants 
        Array = 32, // If this symbol is an array type
        This = 64, // defines one of the 3 builtin `this` assignments for UdonBehaviour, GameObject, and Transform
        Reflection = 128, // Metadata information for type checking and other editor time info
    }

    [Serializable]
    public class SymbolDefinition
    {
        [OdinSerialize]
        private System.Type internalType;

        // The type of the symbol from the C# side
        public System.Type symbolCsType
        {
            get { return UdonSharpUtils.UserTypeToUdonType(internalType); }
            set { internalType = value; }
        }

        public System.Type userCsType { get { return internalType; } }

        // How the symbol was created
        public SymbolDeclTypeFlags declarationType;

        public UdonSyncMode syncMode = UdonSyncMode.NotSynced;

        // The name of the type used by Udon
        public string symbolResolvedTypeName;

        // Original name requested for the symbol, this is what it is named in code.
        public string symbolOriginalName;

        // The generated unique name for this symbol in a given scope to avoid overlapping declarations.
        public string symbolUniqueName;

        // The default value for the symbol that gets set on the heap
        // This is only used for global (public/private) symbols with a default value, and constant symbols
        public object symbolDefaultValue = null;
        
        public bool IsUserDefinedBehaviour()
        {
            return UdonSharpUtils.IsUserDefinedBehaviour(internalType);
        }

        public bool IsUserDefinedType()
        {
            return UdonSharpUtils.IsUserDefinedType(internalType);
        }
    }

    /// <summary>
    /// Symbol tables keep track of all variables in the given context
    /// Symbol tables can be nested, the normal use case is if you have a function define variables in its local context,
    ///  but also needs to reference symbols in its parent class.
    /// This abstraction can extend to any body of code enclosed in {}, so loops and most control flow also create a local symbol table
    /// </summary>
    public class SymbolTable
    {
        public SymbolTable parentSymbolTable { get; private set; }
        public List<SymbolTable> childSymbolTables { get; private set; }

        public List<SymbolDefinition> symbolDefinitions { get; private set; }

        public bool IsGlobalSymbolTable { get { return parentSymbolTable == null; } }

        private ResolverContext resolver;

        private Dictionary<string, int> namedSymbolCounters;

        public SymbolTable GetGlobalSymbolTable()
        {
            SymbolTable currentTable = this;

            while (!currentTable.IsGlobalSymbolTable)
                currentTable = currentTable.parentSymbolTable;

            return currentTable;
        }

        public SymbolTable(ResolverContext resolverContext, SymbolTable parentTable)
        {
            resolver = resolverContext;
            parentSymbolTable = parentTable;

            childSymbolTables = new List<SymbolTable>();

            if (parentTable != null)
                parentTable.childSymbolTables.Add(this);

            symbolDefinitions = new List<SymbolDefinition>();
            namedSymbolCounters = new Dictionary<string, int>();
        }

        protected int IncrementUniqueNameCounter(string symbolName)
        {
            int currentValue = GetUniqueNameCounter(symbolName) + 1;

            if (!namedSymbolCounters.ContainsKey(symbolName))
            {
                namedSymbolCounters.Add(symbolName, currentValue);
            }
            else
            {
                namedSymbolCounters[symbolName] = currentValue;
            }

            return currentValue;
        }

        public int GetUniqueNameCounter(string symbolName)
        {
            int counter = 0;

            // If the current symbol table contains a symbol definition, then just return that.
            if (namedSymbolCounters.TryGetValue(symbolName, out counter))
                return counter;

            // The current symbol table doesn't have a symbol defined, so look in its parent scopes recursively
            if (parentSymbolTable != null)
                return parentSymbolTable.GetUniqueNameCounter(symbolName);

            // If nothing has defined a symbol with this name, then return -1 to signify that
            return -1;
        }

        /// <summary>
        /// This function expects the given symbolName to have some marker to indicate that they are global-only 
        ///  in order to prevent collisions with child symbol table symbols.
        /// </summary>
        /// <param name="symbolName"></param>
        /// <returns></returns>
        public int GetUniqueGlobalNameCounter(string symbolName)
        {
            SymbolTable globalSymbolTable = GetGlobalSymbolTable();

            return globalSymbolTable.GetUniqueNameCounter(symbolName);
        }

        /// <summary>
        /// This function expects the given symbolName to have some marker to indicate that they are global-only 
        ///  in order to prevent collisions with child symbol table symbols.
        /// </summary>
        /// <param name="symbolName"></param>
        /// <returns></returns>
        protected int IncrementGlobalNameCounter(string symbolName)
        {
            SymbolTable globalSymbolTable = GetGlobalSymbolTable();

            return globalSymbolTable.IncrementUniqueNameCounter(symbolName);
        }

        // Slow list building for these. todo: add dictionary caches for these if they are too slow
        public List<SymbolDefinition> GetGlobalSymbols()
        {
            return GetGlobalSymbolTable().symbolDefinitions;
        }

        public List<SymbolDefinition> GetLocalSymbols()
        {
            List<SymbolDefinition> localSymbolDefinitions = new List<SymbolDefinition>();

            SymbolTable currentTable = this;

            while (!currentTable.IsGlobalSymbolTable)
            {
                localSymbolDefinitions.AddRange(currentTable.symbolDefinitions.Where(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.Local)));
                currentTable = currentTable.parentSymbolTable;
            }

            return localSymbolDefinitions;
        }

        public List<SymbolDefinition> GetAllSymbols(bool includeInternal = false)
        {
            List<SymbolDefinition> foundSymbols = new List<SymbolDefinition>();

            SymbolTable currentTable = this;

            while (currentTable != null)
            {
                foundSymbols.AddRange(currentTable.symbolDefinitions.Where(e => includeInternal ? true : !e.declarationType.HasFlag(SymbolDeclTypeFlags.Internal)));
                currentTable = currentTable.parentSymbolTable;
            }

            return foundSymbols;
        }

        /// <summary>
        /// Tries to find a global constant that already has a given constant value to avoid duplication
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="foundSymbol"></param>
        /// <returns></returns>
        public bool TryGetGlobalSymbol(System.Type type, object value, out SymbolDefinition foundSymbol, SymbolDeclTypeFlags flags)
        {
            SymbolTable globalSymTable = GetGlobalSymbolTable();

            foreach (SymbolDefinition definition in globalSymTable.symbolDefinitions)
            {
                bool hasFlags = ((int)flags & (int)definition.declarationType) == (int)flags;

                if (hasFlags &&
                    definition.symbolCsType == type &&
                    ((value == null && definition.symbolDefaultValue == null) ||
                    (definition.symbolDefaultValue != null && definition.symbolDefaultValue.Equals(value))))
                {
                    foundSymbol = definition;
                    return true;
                }
            }

            foundSymbol = null;
            return false;
        }

        public SymbolDefinition CreateConstSymbol(System.Type type, object value)
        {
            if (value != null && !type.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Non-compatible value given for type {type.FullName}");

            SymbolDefinition symbolDefinition;

            if (!TryGetGlobalSymbol(type, value, out symbolDefinition, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant))
            {
                symbolDefinition = CreateUnnamedSymbol(type, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant);
                symbolDefinition.symbolDefaultValue = value;
            }

            return symbolDefinition;
        }

        public SymbolDefinition GetReflectionSymbol(string name, System.Type type)
        {
            SymbolDefinition symbolDefinition = null;

            SymbolTable globalSymbols = GetGlobalSymbolTable();

            foreach (SymbolDefinition currentSymbol in globalSymbols.symbolDefinitions)
            {
                if (currentSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Reflection) &&
                    currentSymbol.symbolOriginalName == name &&
                    currentSymbol.symbolCsType == type)
                {
                    symbolDefinition = currentSymbol;
                    break;
                }
            }

            return symbolDefinition;
        }

        public SymbolDefinition CreateReflectionSymbol(string name, System.Type type, object value)
        {
            if (value != null && !type.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Non-compatible value given for type {type.FullName}");

            SymbolDefinition symbolDefinition = GetReflectionSymbol(name, type);
            
            if (symbolDefinition == null)
            {
                symbolDefinition = CreateNamedSymbol(name, type, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Constant | SymbolDeclTypeFlags.Reflection);
                symbolDefinition.symbolDefaultValue = value;
            }

            return symbolDefinition;
        }

        public SymbolDefinition CreateThisSymbol(System.Type type)
        {
            SymbolTable globalSymTable = GetGlobalSymbolTable();

            System.Type udonType = type.IsSubclassOf(typeof(UdonSharpBehaviour)) ? typeof(VRC.Udon.UdonBehaviour) : type;

            foreach (SymbolDefinition definition in globalSymTable.symbolDefinitions)
            {
                if (definition.declarationType.HasFlag(SymbolDeclTypeFlags.This) && (definition.symbolCsType == udonType))
                    return definition;
            }

            return CreateUnnamedSymbol(type, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.This);
        }

        /// <summary>
        /// Used to get all symbols that need to be declared in the heap data.
        /// </summary>
        /// <returns>A set of tuples of (resolvedTypeName, variableName) </returns>
        public HashSet<Tuple<string, string>> GetAllUniqueChildSymbolNames()
        {
            HashSet<Tuple<string, string>> currentSet = new HashSet<Tuple<string, string>>();

            foreach (SymbolDefinition symbolDefinition in symbolDefinitions)
            {
                currentSet.Add(new Tuple<string, string>(symbolDefinition.symbolResolvedTypeName, symbolDefinition.symbolUniqueName));
            }

            foreach (SymbolTable childTable in childSymbolTables)
            {
                currentSet.UnionWith(childTable.GetAllUniqueChildSymbolNames());
            }

            return currentSet;
        }

        public List<SymbolDefinition> GetAllUniqueChildSymbols()
        {
            HashSet<string> uniqueNameSet = new HashSet<string>();

            List<SymbolDefinition> foundSymbols = new List<SymbolDefinition>();

            foreach (SymbolDefinition symbol in symbolDefinitions)
            {
                if (!uniqueNameSet.Contains(symbol.symbolUniqueName))
                {
                    foundSymbols.Add(symbol);
                    uniqueNameSet.Add(symbol.symbolUniqueName);
                }
            }

            foreach (SymbolTable symbolTable in childSymbolTables)
            {
                List<SymbolDefinition> childSymbols = symbolTable.GetAllUniqueChildSymbols();

                foreach (SymbolDefinition childSymbol in childSymbols)
                {
                    if (!uniqueNameSet.Contains(childSymbol.symbolUniqueName))
                    {
                        foundSymbols.Add(childSymbol);
                        uniqueNameSet.Add(childSymbol.symbolUniqueName);
                    }
                }
            }

            return foundSymbols;
        }

        /// <summary>
        /// Create a symbol given a name. At the moment assumes that only internal symbols get incremented, this means there's no masking at the moment for local variables.
        /// </summary>
        /// <param name="symbolName"></param>
        /// <param name="resolvedSymbolType"></param>
        /// <param name="declType"></param>
        /// <param name="appendType">Used to disable redundant type append from unnamed variable allocations</param>
        /// <returns></returns>
        private SymbolDefinition CreateNamedSymbolInternal(string symbolName, System.Type resolvedSymbolType, SymbolDeclTypeFlags declType, bool appendType = true)
        {
            if (resolvedSymbolType == null || symbolName == null)
                throw new System.ArgumentNullException();

            if (!declType.HasFlag(SymbolDeclTypeFlags.Internal) && symbolName.StartsWith("__"))
            {
                throw new System.ArgumentException($"Symbol {symbolName} cannot have name starting with \"__\", this naming is reserved for internal variables.");
            }

            string uniqueSymbolName = symbolName;

            bool hasGlobalDeclaration = false;
            
            if (declType.HasFlag(SymbolDeclTypeFlags.Internal))
            {
                uniqueSymbolName = $"intnl_{uniqueSymbolName}";
            }
            if (declType.HasFlag(SymbolDeclTypeFlags.Constant))
            {
                uniqueSymbolName = $"const_{uniqueSymbolName}";
                hasGlobalDeclaration = true;
            }
            if (declType.HasFlag(SymbolDeclTypeFlags.This))
            {
                uniqueSymbolName = $"this_{uniqueSymbolName}";
                hasGlobalDeclaration = true;
            }
            if (declType.HasFlag(SymbolDeclTypeFlags.Reflection))
            {
                uniqueSymbolName = $"__refl_{uniqueSymbolName}";
                hasGlobalDeclaration = true;
            }

            if (!declType.HasFlag(SymbolDeclTypeFlags.Public) && !declType.HasFlag(SymbolDeclTypeFlags.Private) && !declType.HasFlag(SymbolDeclTypeFlags.Reflection))
            {
                if (appendType)
                {
                    string sanitizedName = resolver.SanitizeTypeName(resolvedSymbolType.Name);
                    uniqueSymbolName += $"_{sanitizedName}";
                }

                if (hasGlobalDeclaration)
                    uniqueSymbolName = $"__{IncrementGlobalNameCounter(uniqueSymbolName)}_{uniqueSymbolName}";
                else
                    uniqueSymbolName = $"__{IncrementUniqueNameCounter(uniqueSymbolName)}_{uniqueSymbolName}";
            }

            System.Type typeForName = UdonSharpUtils.UserTypeToUdonType(resolvedSymbolType);

            string udonTypeName = resolver.GetUdonTypeName(typeForName);

            if (udonTypeName == null)
                throw new System.ArgumentException($"Could not locate Udon type for system type {resolvedSymbolType.FullName}");
            
            udonTypeName = udonTypeName.Replace("VRCUdonCommonInterfacesIUdonEventReceiver", "VRCUdonUdonBehaviour");

            SymbolDefinition symbolDefinition = new SymbolDefinition();
            symbolDefinition.declarationType = declType;
            symbolDefinition.symbolCsType = resolvedSymbolType;
            symbolDefinition.symbolOriginalName = symbolName;
            symbolDefinition.symbolResolvedTypeName = udonTypeName;
            symbolDefinition.symbolUniqueName = uniqueSymbolName;

            if (hasGlobalDeclaration)
            {
                GetGlobalSymbolTable().symbolDefinitions.Add(symbolDefinition);
            }
            else
            {
                symbolDefinitions.Add(symbolDefinition);
            }

            return symbolDefinition;
        }

        // For symbols that we want explicit names for
        // Used for internally named things based on the operation being performed or user-defined symbols
        public SymbolDefinition CreateNamedSymbol(string symbolName, string symbolType, SymbolDeclTypeFlags declType)
        {
            System.Type resolvedType = resolver.ResolveExternType(symbolType);
            if (declType.HasFlag(SymbolDeclTypeFlags.Array))
                resolvedType = resolvedType.MakeArrayType();

            return CreateNamedSymbol(symbolName, resolvedType, declType);
        }

        public SymbolDefinition CreateNamedSymbol(string symbolName, System.Type symbolType, SymbolDeclTypeFlags declType)
        {
            return CreateNamedSymbolInternal(symbolName, symbolType, declType);
        }

        public SymbolDefinition FindUserDefinedSymbol(string symbolName)
        {
            SymbolTable currentTable = this;

            while (currentTable != null)
            {
                //foreach (SymbolDefinition symbolDefinition in currentTable.symbolDefinitions)
                for (int i = currentTable.symbolDefinitions.Count - 1; i >= 0; --i)
                {
                    SymbolDefinition symbolDefinition = currentTable.symbolDefinitions[i];

                    if (!symbolDefinition.declarationType.HasFlag(SymbolDeclTypeFlags.Internal) &&
                        symbolDefinition.symbolOriginalName == symbolName)
                    {
                        return symbolDefinition;
                    }
                }

                currentTable = currentTable.parentSymbolTable;
            }

            // Found nothing, return null
            return null;
        }

        // Automatically infers the name of the symbol based on its type
        // Used for intermediate values usually
        public SymbolDefinition CreateUnnamedSymbol(string symbolType, SymbolDeclTypeFlags declType)
        {
            System.Type resolvedType = resolver.ResolveExternType(symbolType);
            if (declType.HasFlag(SymbolDeclTypeFlags.Array))
                resolvedType = resolvedType.MakeArrayType();

            return CreateUnnamedSymbol(resolvedType, declType);
        }

        public SymbolDefinition CreateUnnamedSymbol(System.Type type, SymbolDeclTypeFlags declType)
        {
            string typeName = resolver.GetUdonTypeName(type);

            if (type.IsArray)
                declType |= SymbolDeclTypeFlags.Array;

            // Not a valid Udon type
            if (typeName == null)
                return null;

            return CreateNamedSymbolInternal(typeName, type, declType | SymbolDeclTypeFlags.Internal | (IsGlobalSymbolTable ? 0 : SymbolDeclTypeFlags.Local), false);
        }

        public List<SymbolTable> GetAllChildSymbolTables()
        {
            List<SymbolTable> childTables = new List<SymbolTable>();

            foreach (SymbolTable childTable in childSymbolTables)
            {
                childTables.AddRange(childTable.GetAllChildSymbolTables());
            }

            childTables.Add(this);

            return childTables;
        }

        public void FlattenTableCountersToGlobal()
        {
            Dictionary<string, int> namedSymbolMaxCount = new Dictionary<string, int>();

            foreach (SymbolTable childTable in GetAllChildSymbolTables())
            {
                foreach (var childSymbolCounter in childTable.namedSymbolCounters)
                {
                    if (namedSymbolMaxCount.ContainsKey(childSymbolCounter.Key))
                        namedSymbolMaxCount[childSymbolCounter.Key] = Mathf.Max(namedSymbolMaxCount[childSymbolCounter.Key], childSymbolCounter.Value);
                    else
                        namedSymbolMaxCount.Add(childSymbolCounter.Key, childSymbolCounter.Value);
                }
            }

            SymbolTable globalTable = GetGlobalSymbolTable();

            foreach (var childSymbolNameCount in namedSymbolMaxCount)
            {
                if (globalTable.namedSymbolCounters.ContainsKey(childSymbolNameCount.Key))
                    globalTable.namedSymbolCounters[childSymbolNameCount.Key] = Mathf.Max(globalTable.namedSymbolCounters[childSymbolNameCount.Key], childSymbolNameCount.Value);
                else
                    globalTable.namedSymbolCounters.Add(childSymbolNameCount.Key, childSymbolNameCount.Value);
            }
        }
    }

}