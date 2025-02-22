
using System;
using System.Collections.Generic;
using UdonSharp.Compiler.Assembly;
using UdonSharp.Compiler.Symbols;
using UnityEngine;

namespace UdonSharp.Compiler.Emit
{
    /// <summary>
    /// Represents a value that needs to be stored on the heap for a constant, user field/local variable, or internally generated value for expression results
    /// </summary>
    internal class ValueTable
    {
        public AssemblyModule Module { get; }
        public ValueTable ParentTable { get; }

        private ValueTable _lazyGlobalTable;
        public ValueTable GlobalTable
        {
            get
            {
                if (_lazyGlobalTable != null)
                    return _lazyGlobalTable;
                
                ValueTable currentTable = this;
                while (currentTable.ParentTable != null)
                    currentTable = currentTable.ParentTable;

                _lazyGlobalTable = currentTable;
                _lazyGlobalTable.LazyInitTable();

                return _lazyGlobalTable;
            }
        }

        public bool IsRoot => ParentTable == null;

        private List<ValueTable> _childTables;
        private List<Value> _values;
        private Dictionary<string, int> _uniqueIDTracker;
        private Dictionary<Symbol, Value> _userValues;

        private HashSet<string> _nameCollisionCheckSet;

        public List<Value> Values => _values ?? new List<Value>();

        public ValueTable(AssemblyModule module, ValueTable parent)
        {
            Module = module;
            ParentTable = parent;
            _nameCollisionCheckSet = ParentTable != null ? new HashSet<string>(ParentTable._nameCollisionCheckSet) : new HashSet<string>();
        }

        private bool _initialized;
        private void LazyInitTable()
        {
            if (!_initialized)
            {
                _childTables = new List<ValueTable>();
                _values = new List<Value>();

                if (ParentTable != null)
                    _uniqueIDTracker = new Dictionary<string, int>(ParentTable._uniqueIDTracker);
                else
                    _uniqueIDTracker = new Dictionary<string, int>();

                _userValues = new Dictionary<Symbol, Value>();

                _initialized = true;
            }
        }

        public void AddChildTable(ValueTable child)
        {
            LazyInitTable();
            _childTables.Add(child);
        }

        public Value CreateInternalValue(TypeSymbol type, string debugName = null)
        {
            return CreateValueInternal(type, null, Value.ValueFlags.Internal, debugName);
        }

        public Value GetConstantValue(TypeSymbol type, object value, string debugName = null)
        {
            foreach (Value globalValue in GlobalTable._values)
            {
                if ((globalValue.Flags & Value.ValueFlags.Constant) != 0 &&
                    ReferenceEquals(globalValue.UserType, type) &&
                    ((value == null && globalValue.DefaultValue == null) ||
                     (globalValue.DefaultValue != null && globalValue.DefaultValue.Equals(value))))
                {
                    return globalValue;
                }
            }

            Value constVal = GlobalTable.CreateValueInternal(type, null, Value.ValueFlags.Constant, debugName);

            constVal.DefaultValue = value;

            return constVal;
        }

        public Value CreateGlobalInternalValue(TypeSymbol type, string debugName = null)
        {
            return GlobalTable.CreateValueInternal(type, null, Value.ValueFlags.InternalGlobal, debugName);
        }

        public Value GetUdonThisValue(TypeSymbol type)
        {
            foreach (Value globalValue in GlobalTable._values)
            {
                if ((globalValue.Flags & Value.ValueFlags.UdonThis) != 0 &&
                    globalValue.UdonType == type)
                {
                    return globalValue;
                }
            }

            return GlobalTable.CreateValueInternal(type, null, 
                Value.ValueFlags.UdonThis | Value.ValueFlags.Constant, "this");
        }

        public Value GetUserValue(Symbol userSymbol)
        {
            Value userValue = FindUserValue(userSymbol);

            if (userValue != null)
                return userValue;

            switch (userSymbol)
            {
                case LocalSymbol localSymbol:
                    userValue = CreateValueInternal(localSymbol.Type, userSymbol, Value.ValueFlags.Local, userSymbol.Name);
                    break;
                case FieldSymbol fieldSymbol:
                    userValue = GlobalTable.CreateValueInternal(fieldSymbol.Type, userSymbol, Value.ValueFlags.Field, userSymbol.Name);
                    GlobalTable._userValues.Add(userSymbol, userValue);
                    
                    userValue.SetAssociatedSymbol(userSymbol);
                    break;
                default:
                    throw new InvalidOperationException("Was unable to create user value");
            }

            LazyInitTable();
            if (!_userValues.ContainsKey(userSymbol)) // Already added above
                _userValues.Add(userSymbol, userValue);
            
            return userValue;
        }

        private Value FindUserValue(Symbol userSymbol)
        {
            Value userValue = null;
            if (_initialized && _userValues.TryGetValue(userSymbol, out userValue))
                return userValue;

            if (ParentTable != null)
                userValue = ParentTable.FindUserValue(userSymbol);

            if (userValue == null)
                return null;
            
            LazyInitTable();
            _userValues.Add(userSymbol, userValue);

            return userValue;
        }

        public Value CreateParameterValue(string parameterID, TypeSymbol type)
        {
            return GlobalTable.CreateValueInternal(type, null, Value.ValueFlags.Parameter, parameterID);
        }

        public Value CreateReflectionValue(string parameterID, TypeSymbol type, object value)
        {
            Value newValue = GlobalTable.CreateValueInternal(type, null, Value.ValueFlags.Reflection, parameterID);
            newValue.DefaultValue = value;

            return newValue;
        }

        private string GetUniqueValueName(TypeSymbol type, Value.ValueFlags flags, string symbolName)
        {
            string uniqueName;

            symbolName = UdonSharpUtils.UnmanglePropertyFieldName(symbolName);

            if ((flags & Value.ValueFlags.Field) != 0 || 
                (flags & Value.ValueFlags.Parameter) != 0 ||
                (flags & Value.ValueFlags.Reflection) != 0)
            {
                uniqueName = symbolName;
            }
            else
            {
                string namePrefix = "";

                switch (flags)
                {
                    case Value.ValueFlags.Constant:
                        namePrefix = "const_";
                        break;
                    case Value.ValueFlags.Internal:
                        namePrefix = "intnl_";
                        break;
                    case Value.ValueFlags.Local:
                        namePrefix = "lcl_";
                        break;
                    case Value.ValueFlags.Parameter:
                        namePrefix = "param_";
                        break;
                    case Value.ValueFlags.PropertyBackingField:
                        namePrefix = "prop_";
                        break;
                    case Value.ValueFlags.UdonThis:
                        namePrefix = "this_";
                        break;
                    case Value.ValueFlags.InternalGlobal:
                        namePrefix = "gintnl_";
                        break;
                }
                
                string valueName = $"__{namePrefix}{(symbolName != null ? $"{symbolName}_" : "")}{type.UdonType.ExternSignature}";

                if (!_uniqueIDTracker.TryGetValue(valueName, out int counter))
                {
                    _uniqueIDTracker.Add(valueName, 0);
                }

                uniqueName = $"{valueName}_{counter}";

                _uniqueIDTracker[valueName] = counter + 1;
            }

            if (_nameCollisionCheckSet.Contains(uniqueName))
                throw new InvalidOperationException("Cannot allocate a symbol with the same name twice");

            _nameCollisionCheckSet.Add(uniqueName);

            return uniqueName;
        }

        private Value CreateValueInternal(TypeSymbol type, Symbol associatedSymbol, Value.ValueFlags flags, string name = null)
        {
            LazyInitTable();

            if ((flags & Value.ValueFlags.Local) != 0 && IsRoot)
                throw new InvalidOperationException("Local values cannot be created in the root table.");
            
            Value newVal = new Value(this, GetUniqueValueName(type, flags, name), type, flags);
            
            _values.Add(newVal);

            return newVal;
        }

        public List<Value> GetAllUniqueChildValues()
        {
            List<Value> foundValues = new List<Value>();

            if (_initialized)
            {
                HashSet<string> uniqueNameSet = new HashSet<string>();

                foreach (Value value in _values)
                {
                    if (!uniqueNameSet.Contains(value.UniqueID))
                    {
                        foundValues.Add(value);
                        uniqueNameSet.Add(value.UniqueID);
                    }
                }

                foreach (ValueTable valueTable in _childTables)
                {
                    List<Value> childSymbols = valueTable.GetAllUniqueChildValues();

                    foreach (Value childValue in childSymbols)
                    {
                        if (!uniqueNameSet.Contains(childValue.UniqueID))
                        {
                            foundValues.Add(childValue);
                            uniqueNameSet.Add(childValue.UniqueID);
                        }
                    }
                }
            }

            return foundValues;
        }

        private List<ValueTable> GetAllChildValueTables()
        {
            List<ValueTable> currentChildren = new List<ValueTable>();

            if (_initialized)
            {
                currentChildren.Add(this);

                foreach (ValueTable childTable in _childTables)
                    currentChildren.AddRange(childTable.GetAllChildValueTables());
            }

            return currentChildren;
        }
        
        public void FlattenTableCountersToGlobal()
        {
            Dictionary<string, int> namedSymbolMaxCount = new Dictionary<string, int>();

            foreach (ValueTable childTable in GetAllChildValueTables())
            {
                foreach (var childSymbolCounter in childTable._uniqueIDTracker)
                {
                    if (namedSymbolMaxCount.ContainsKey(childSymbolCounter.Key))
                        namedSymbolMaxCount[childSymbolCounter.Key] = Mathf.Max(namedSymbolMaxCount[childSymbolCounter.Key], childSymbolCounter.Value);
                    else
                        namedSymbolMaxCount.Add(childSymbolCounter.Key, childSymbolCounter.Value);
                }
            }

            foreach (var childSymbolNameCount in namedSymbolMaxCount)
            {
                if (GlobalTable._uniqueIDTracker.ContainsKey(childSymbolNameCount.Key))
                    GlobalTable._uniqueIDTracker[childSymbolNameCount.Key] = Mathf.Max(GlobalTable._uniqueIDTracker[childSymbolNameCount.Key], childSymbolNameCount.Value);
                else
                    GlobalTable._uniqueIDTracker.Add(childSymbolNameCount.Key, childSymbolNameCount.Value);
            }
        }

        public void DirtyAllValues()
        {
            ValueTable currentTable = this;

            while (currentTable != null)
            {
                if (currentTable._values != null)
                {
                    Value[] iterationArray = currentTable._values.ToArray(); // Needed since dirtying can modify the _values list
                    
                    foreach (Value val in iterationArray)
                    {
                        if (val.IsConstant || val.IsLocal || val.IsInternal)
                            continue;

                        val.MarkDirty();
                    }
                }

                currentTable = currentTable.ParentTable;
            }
        }
    }
}
