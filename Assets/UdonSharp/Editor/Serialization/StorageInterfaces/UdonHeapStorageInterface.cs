
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Serialization
{
    public class UdonHeapStorageInterface : IHeapStorage
    {
        class UdonHeapValueStorage<T> : ValueStorage<T>
        {
            IUdonHeap heap;
            uint symbolAddress;

            public UdonHeapValueStorage(IUdonHeap heap, IUdonSymbolTable symbolTable, string symbolKey)
            {
                this.heap = heap;
                
                bool isValid = symbolTable.TryGetAddressFromSymbol(symbolKey, out symbolAddress) && 
                               heap.GetHeapVariableType(symbolAddress) == typeof(T) &&
                               heap.TryGetHeapVariable<T>(symbolAddress, out var validityCheckPlaceholder);

                if (!isValid)
                    symbolAddress = 0xFFFFFFFF;
            }

            public override T Value
            {
                get
                {
                    if (symbolAddress == 0xFFFFFFFF)
                        return default;

                    return heap.GetHeapVariable<T>(symbolAddress);
                }
                set
                {
                    if (symbolAddress == 0xFFFFFFFF)
                        return;

                    heap.SetHeapVariable<T>(symbolAddress, value);
                }
            }
        }

        UdonBehaviour behaviour;
        IUdonHeap heap;
        IUdonSymbolTable symbolTable;
        List<IValueStorage> heapValueRefs = new List<IValueStorage>();

        public bool IsValid { get; } = false;

        static FieldInfo programField;

        public UdonHeapStorageInterface(UdonBehaviour udonBehaviour)
        {
            behaviour = udonBehaviour;

            if (programField == null)
                programField = typeof(UdonBehaviour).GetField("_program", BindingFlags.NonPublic | BindingFlags.Instance);

            IUdonProgram sourceProgram = (IUdonProgram)programField.GetValue(udonBehaviour);

            if (sourceProgram != null)
            {
                heap = sourceProgram.Heap;
                symbolTable = sourceProgram.SymbolTable;
                IsValid = true;
            }
            else
            {
                IsValid = false;
            }
        }

        void IHeapStorage.SetElementValue<T>(string elementKey, T value)
        {
            uint symbolAddress;
            System.Type symbolType = null;

            if (symbolTable.TryGetAddressFromSymbol(elementKey, out symbolAddress))
            {
                symbolType = heap.GetHeapVariableType(symbolAddress);

                if (symbolType.IsAssignableFrom(typeof(T)))
                {
                    heap.SetHeapVariable<T>(symbolAddress, value);
                }
            }
        }

        T IHeapStorage.GetElementValue<T>(string elementKey)
        {
            uint symbolAddress;
            System.Type symbolType = null;

            if (symbolTable.TryGetAddressFromSymbol(elementKey, out symbolAddress))
            {
                symbolType = heap.GetHeapVariableType(symbolAddress);

                if (symbolType.IsAssignableFrom(typeof(T)))
                {
                    return heap.GetHeapVariable<T>(symbolAddress);
                }
            }

            return default;
        }

        public void SetElementValueWeak(string elementKey, object value)
        {
            uint symbolAddress;
            System.Type symbolType = null;

            if (symbolTable.TryGetAddressFromSymbol(elementKey, out symbolAddress))
            {
                symbolType = heap.GetHeapVariableType(symbolAddress);

                if (symbolType.IsAssignableFrom(value.GetType()))
                {
                    heap.SetHeapVariable(symbolAddress, value, symbolType);
                }
            }
        }

        public object GetElementValueWeak(string elementKey)
        {
            uint symbolAddress;

            if (symbolTable.TryGetAddressFromSymbol(elementKey, out symbolAddress))
            {
                return heap.GetHeapVariable(symbolAddress);
            }

            return null;
        }

        public void InvalidateInterface()
        {

        }

        public IValueStorage GetElementStorage(string elementKey)
        {
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)behaviour.programSource;

            if (!programAsset.fieldDefinitions.TryGetValue(elementKey, out Compiler.FieldDefinition fieldDefinition))
            {
                Debug.LogError($"Could not find definition for field {elementKey}");
                return null;
            }

            IValueStorage udonHeapValue = (IValueStorage)System.Activator.CreateInstance(typeof(UdonHeapValueStorage<>).MakeGenericType(fieldDefinition.fieldSymbol.symbolCsType), heap, symbolTable, elementKey);

            heapValueRefs.Add(udonHeapValue);

            return udonHeapValue;
        }
    }
}
