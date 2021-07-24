using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;

namespace UdonSharp.Compiler
{
    [Flags]
    public enum SymbolDeclTypeFlags
    {
        Public = 1, // Declared by the user as a public variable on a class
        Private = 2, // Declared by the user as a private variable on a class
        Local = 4, // Declared by the user as a variable local to a specific scope
        Internal = 8, // Generated as an intermediate variable that stores intermediate calculations
        Constant = 16, // Used to represent a constant value set by the compiler that does not change after compile time. Variables with const/readonly use the Readonly flag.
        Array = 32, // If this symbol is an array type
        This = 64, // defines one of the 3 builtin `this` assignments for UdonBehaviour, GameObject, and Transform
        Reflection = 128, // Metadata information for type checking and other editor time info
        Readonly = 256, // Symbols marked as either const or readonly by the user, treat them the same for now. 
        MethodParameter = 512, // Symbols used for passing around method parameters
        NeedsRecursivePush = 1024, // Internal symbols used for tracking flow control and such which need to be pushed to the recursive stack when a method is recursive. An example of this is the int counter for a foreach loop and the size of the array the foreach is iterating.
        BuiltinVar = 2048,
        PropertyBackingField = 4096, // Internal symbols used as backing field for user-defined property
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

        private COWValueInternal cowValue = null;

#if UDONSHARP_DEBUG
        private List<COWValueInternal> priorCowValues = new List<COWValueInternal>();
#endif

        public override string ToString()
        {
            return $"Symbol#{symbolUniqueName}";
        }
        public bool IsUserDefinedBehaviour()
        {
            return UdonSharpUtils.IsUserDefinedBehaviour(internalType);
        }

        public bool IsUserDefinedType()
        {
            return UdonSharpUtils.IsUserDefinedType(internalType);
        }

        /// <summary>
        /// Marks this symbol as dirty; any pending copy-on-write capture will proceed 
        /// to make a copy of the current value of this symbol if necessary. This should be
        /// done before any write to a (potentially) non-internal symbol.
        /// </summary>
        public void MarkDirty()
        {
            if (cowValue != null)
            {
#if UDONSHARP_DEBUG
                priorCowValues.Add(cowValue);
#endif
                cowValue.MarkDirty();
            }
        }
        public void AssertCOWClosed()
        {
            if (cowValue != null)
            {
                cowValue.AssertNoLeaks();
            }
#if UDONSHARP_DEBUG
            foreach (COWValueInternal prior in priorCowValues)
            {
                prior.AssertNoLeaks();
            }
#endif
        }

        public COWValue GetCOWValue(ASTVisitorContext visitorContext)
        {
            if (cowValue != null)
            {
                if (cowValue.visitorContext != visitorContext)
                {
                    // Hmm... new compilation context? Dirty it and get a new one.
                    cowValue.MarkDirty();
                    cowValue = null;
                }
                else if (cowValue.isDirty || cowValue.referenceCount == 0)
                {
                    // If the reference count is 0, we've probably moved scopes. We clear out the cowValue here to make sure that a cowValue is only used in one scope at a time. 
                    cowValue = null;
                }
                else
                {
                    return new COWValue(cowValue);
                }
            }

            cowValue = new COWValueInternal(visitorContext, this);
            return new COWValue(cowValue);
        }

        /// <summary>
        /// In some cases, we want to capture the current value in a symbol, do some other work (which may or may not modify the symbol),
        /// then use that value. This is important for e.g. binary expressions with side-effecting terms, such as (a = b + (b += 1)).
        /// We must capture the old value of 'b' here before evaluating the right-hand side. However, most such expressions
        /// don't in fact end up writing to these values, so we'd like to avoid the cost of these extra copies.
        /// 
        /// The COWSymbolValue class represents an observation of a symbol value at some point in the past.
        /// If the SymbolDefinition.MakeDirty() function is called before this observation is resolved (disposed),
        /// a COPY is generated to a temporary symbol. Otherwise, though, we use the symbol directly.
        /// </summary>
        internal class COWValueInternal
        {
            static int index = 0;

            public int instanceIndex = Interlocked.Increment(ref index);

            public int referenceCount = 0;
            public bool isDirty = false;

            public SymbolDefinition symbol { get; private set; } = null;
            public SymbolDefinition originalSymbol { get; private set; } = null;

            public ASTVisitorContext visitorContext { get; private set; } = null;

#if UDONSHARP_DEBUG
            private HashSet<COWValue> holders = new HashSet<COWValue>();
#endif
            private SymbolTable tableCreationScope;

            public COWValueInternal(ASTVisitorContext visitorContext, SymbolDefinition symbol)
            {
                this.symbol = this.originalSymbol = symbol;
                this.visitorContext = visitorContext;

                tableCreationScope = visitorContext.topTable;
                
                tableCreationScope.AddSymbolCOW(this);
            }

            public void AddRef(COWValue holder)
            {
                referenceCount++;
#if UDONSHARP_DEBUG
                holders.Add(holder);
#endif

                if (visitorContext.topTable != tableCreationScope)
                {
                    throw new Exception($"COWSymbolValue for {originalSymbol} has had ref added from different symbol table scope.");
                }
            }

            public void ClearRef(COWValue holder)
            {
                referenceCount--;
#if UDONSHARP_DEBUG
                if (!holders.Remove(holder))
                {
                    throw new Exception("No matching holder for COWValue");
                }
#endif

                if (visitorContext.topTable != tableCreationScope)
                {
                    throw new Exception($"COWSymbolValue for {originalSymbol} has been disposed from different symbol table scope.");
                }
            }

            public void AssertNoLeaks()
            {
                if (referenceCount != 0)
                {
#if UDONSHARP_DEBUG
                    foreach (COWValue holder in holders) {
                        Debug.LogError($"Value reference for symbol {originalSymbol} ref count: {referenceCount} leaked at:\n\n{holder.stackTrace}");
                    }
#endif
                    throw new Exception($"UdonSharp internal error: Leaked COWValue reference for symbol {originalSymbol}");
                }
            }

            public void MarkDirty()
            {
                if (referenceCount == 0)
                {
                    isDirty = true;
                    symbol = null;
                    return;
                }

                if (!isDirty)
                {
                    SymbolDefinition temporary = visitorContext.topTable.CreateUnnamedSymbol(symbol.internalType, SymbolDeclTypeFlags.Internal | SymbolDeclTypeFlags.Local);
                    visitorContext.uasmBuilder.AddCopy(temporary, symbol, " Copy-on-write symbol value dirtied");
                    symbol = temporary;
                    isDirty = true;
                }
            }
        }

        public class COWValue : IDisposable
        {
            private bool isDisposed = false;
            private COWValueInternal backer;
#if UDONSHARP_DEBUG
            public System.Diagnostics.StackTrace stackTrace;
#endif

            internal COWValue(COWValueInternal backer)
            {
                this.backer = backer;
#if UDONSHARP_DEBUG
                stackTrace = new System.Diagnostics.StackTrace(true);
#endif
                backer.AddRef(this);
            }

            public SymbolDefinition symbol
            {
                get
                {
                    if (isDisposed)
                    {
                        throw new Exception($"COWSymbolValue for {backer.originalSymbol} has been disposed");
                    }
                    return backer.symbol;
                }
            }

            public COWValue AddRef()
            {
                if (isDisposed)
                {
                    throw new Exception($"COWSymbolValue for {backer.originalSymbol} has been disposed");
                }

                return new COWValue(backer);
            }

            public void Dispose()
            {
                if (isDisposed) return;

                isDisposed = true;

                backer.ClearRef(this);
            }
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

        private bool IsTableReadOnly = true;

#if UDONSHARP_DEBUG
        private System.Diagnostics.StackTrace creationTrace;
#endif

        private List<(SymbolTable, Dictionary<string, int>)> initialSymbolCounters = new List<(SymbolTable, Dictionary<string, int>)>();

        private List<SymbolDefinition.COWValueInternal> scopeCOWValues = new List<SymbolDefinition.COWValueInternal>();
        
        int expressionScopeDepth = 0;

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

#if UDONSHARP_DEBUG
            creationTrace = new System.Diagnostics.StackTrace(true);
#endif
        }

        public void OpenSymbolTable()
        {
            IsTableReadOnly = false;

            // Copy the current symbol counters for checking when the symbol table has been closed
            SymbolTable currentTable = parentSymbolTable;
            while (currentTable != null)
            {
                initialSymbolCounters.Add((currentTable, new Dictionary<string, int>(currentTable.namedSymbolCounters)));
                currentTable = currentTable.parentSymbolTable;
            }
        }

        public void CloseSymbolTable()
        {
            IsTableReadOnly = true;
            
            ValidateParentTableCounters();

            Debug.Assert(expressionScopeDepth == 0, "Symbol table scope depth must be 0");
            Debug.Assert(scopeCOWValues.Count == 0, "Symbol table COW values must be empty");
        }

        public void EnterExpressionScope()
        {
            ++expressionScopeDepth;
        }

        public void ExitExpressionScope()
        {
            --expressionScopeDepth;

            if (expressionScopeDepth == 0)
                scopeCOWValues.Clear();

            Debug.Assert(expressionScopeDepth >= 0, "Expression scope cannot be negative");
        }

        public IEnumerable<SymbolDefinition> GetOpenCOWSymbols()
        {
            return scopeCOWValues.Where(e => e.symbol != null && e.referenceCount > 0 && !e.symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Constant) && !e.symbol.declarationType.HasFlag(SymbolDeclTypeFlags.Readonly)).Select(e => e.symbol);
        }

        internal void AddSymbolCOW(SymbolDefinition.COWValueInternal value)
        {
            if (expressionScopeDepth > 0)
                scopeCOWValues.Add(value);
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

        public void ValidateParentTableCounters()
        {
            SymbolTable currentTable = parentSymbolTable;

            int tableIdx = 0;

            while (currentTable != null)
            {
                (SymbolTable, Dictionary<string, int>) counterPair = initialSymbolCounters[tableIdx];

                if (counterPair.Item1 != currentTable)
                    throw new Exception("Table mismatch, parent tables have changed during the lifetime of a symbol table.");

                Dictionary<string, int> initialCounters = counterPair.Item2;

                if (!currentTable.IsGlobalSymbolTable)
                {
                    foreach (var currentCounters in currentTable.namedSymbolCounters)
                    {
                        if (!initialCounters.ContainsKey(currentCounters.Key))
                            throw new Exception($"Counter for symbol {currentCounters.Key} has been added while table is not valid for modification.");

                        if (initialCounters[currentCounters.Key] != currentCounters.Value)
                            throw new Exception($"Counter for symbol {currentCounters.Key} in symbol table has been modified while table is not valid for modification.");
                    }
                }

                ++tableIdx;
                currentTable = currentTable.parentSymbolTable;
            }
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

        public List<SymbolDefinition> GetAllLocalSymbols()
        {
            List<SymbolDefinition> foundSymbols = new List<SymbolDefinition>();

            SymbolTable currentTable = this;

            while (currentTable != null && !currentTable.IsGlobalSymbolTable)
            {
                foundSymbols.AddRange(currentTable.symbolDefinitions.Where(e => !e.declarationType.HasFlag(SymbolDeclTypeFlags.Internal) && e.declarationType.HasFlag(SymbolDeclTypeFlags.Local)));
                currentTable = currentTable.parentSymbolTable;
            }

            return foundSymbols;
        }

        public List<SymbolDefinition> GetAllRecursiveSymbols()
        {
            List<SymbolDefinition> foundSymbols = new List<SymbolDefinition>();

            SymbolTable currentTable = this;

            while (currentTable != null && !currentTable.IsGlobalSymbolTable)
            {
                foundSymbols.AddRange(currentTable.symbolDefinitions.Where(e => (!e.declarationType.HasFlag(SymbolDeclTypeFlags.Internal) || e.declarationType.HasFlag(SymbolDeclTypeFlags.NeedsRecursivePush)) && e.declarationType.HasFlag(SymbolDeclTypeFlags.Local)));
                currentTable = currentTable.parentSymbolTable;
            }

            return foundSymbols;
        }

        public List<SymbolDefinition> GetCurrentMethodParameters()
        {
            List<SymbolDefinition> foundSymbols = new List<SymbolDefinition>();

            SymbolTable currentTable = this;

            while (currentTable != null && !currentTable.IsGlobalSymbolTable)
            {
                foundSymbols.AddRange(currentTable.symbolDefinitions.Where(e => e.declarationType.HasFlag(SymbolDeclTypeFlags.MethodParameter)));
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

            if (!declType.HasFlag(SymbolDeclTypeFlags.Internal) && !declType.HasFlag(SymbolDeclTypeFlags.BuiltinVar) && symbolName.StartsWith("__"))
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
            if (declType.HasFlag(SymbolDeclTypeFlags.MethodParameter))
            {
                uniqueSymbolName = $"mp_{uniqueSymbolName}";
            }
            if (declType.HasFlag(SymbolDeclTypeFlags.This))
            {
                uniqueSymbolName = $"this_{uniqueSymbolName}";
                hasGlobalDeclaration = true;
            }
            if (declType.HasFlag(SymbolDeclTypeFlags.PropertyBackingField))
            {
                uniqueSymbolName = $"bf_{uniqueSymbolName}";
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
            //udonTypeName = udonTypeName.Replace("VRCUdonUdonBehaviourArray", "VRCUdonCommonInterfacesIUdonEventReceiverArray");

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

#if UDONSHARP_DEBUG
            if (IsTableReadOnly)
                throw new Exception($"Cannot add symbol {symbolDefinition}, symbol table is readonly. Symbol Table creation stacktrace \n\n{creationTrace}");
#else
            if (IsTableReadOnly)
                throw new Exception($"Cannot add symbol {symbolDefinition}, symbol table is readonly.");
#endif

            if (IsGlobalSymbolTable)
            {
                bool anyChildTableOpen = false;
                foreach (SymbolTable childTable in childSymbolTables)
                {
                    if (!childTable.IsTableReadOnly)
                    {
                        anyChildTableOpen = true;
                        break;
                    }
                }

                if (anyChildTableOpen)
                {
                    if (!declType.HasFlag(SymbolDeclTypeFlags.Reflection) &&
                        !declType.HasFlag(SymbolDeclTypeFlags.Constant) &&
                        !declType.HasFlag(SymbolDeclTypeFlags.This) &&
                        !declType.HasFlag(SymbolDeclTypeFlags.BuiltinVar))
                        throw new Exception($"Cannot add symbol {symbolDefinition} to root table while other tables are in use.");
                }
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
                for (int i = currentTable.symbolDefinitions.Count - 1; i >= 0; --i)
                {
                    SymbolDefinition symbolDefinition = currentTable.symbolDefinitions[i];

                    if ((symbolDefinition.declarationType & SymbolDeclTypeFlags.Internal) == 0 &&
                        (!currentTable.IsGlobalSymbolTable || (symbolDefinition.declarationType & SymbolDeclTypeFlags.MethodParameter) == 0) && // Method parameters are declared globally, but only valid in their current local scope
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

        public void DirtyEverything(bool skipLocals = false)
        {
            foreach (SymbolDefinition symbol in GetAllSymbols(true))
            {
                if (skipLocals && (symbol.declarationType & SymbolDeclTypeFlags.Local) != 0)
                {
                    continue;
                }

                if ((symbol.declarationType & SymbolDeclTypeFlags.Constant) != 0)
                {
                    continue;
                }

                symbol.MarkDirty();
            }
        }
    }

}