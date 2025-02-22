﻿
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Serialization
{
    public class UdonHeapStorageInterface : IHeapStorage
    {
        private class UdonHeapValueStorage<T> : ValueStorage<T>
        {
            private IUdonHeap heap;
            private uint symbolAddress;

            public UdonHeapValueStorage(IUdonHeap heap, IUdonSymbolTable symbolTable, string symbolKey)
            {
                this.heap = heap;
                
                bool isValid = symbolTable.TryGetAddressFromSymbol(UdonSharpUtils.UnmanglePropertyFieldName(symbolKey), out symbolAddress) && 
                               heap.GetHeapVariableType(symbolAddress) == typeof(T) &&
                               heap.TryGetHeapVariable<T>(symbolAddress, out _);

                if (!isValid)
                    symbolAddress = 0xFFFFFFFF;
            }

            public override T Value
            {
                get => symbolAddress == 0xFFFFFFFF ? default : heap.GetHeapVariable<T>(symbolAddress);
                set
                {
                    if (symbolAddress == 0xFFFFFFFF)
                        return;

                    if (UsbSerializationContext.CollectDependencies)
                        return;

                    heap.SetHeapVariable<T>(symbolAddress, value);
                }
            }
        }

        private UdonBehaviour behaviour;
        private IUdonHeap heap;
        private IUdonSymbolTable symbolTable;
        private List<IValueStorage> heapValueRefs = new List<IValueStorage>();

        public bool IsValid { get; }

        private static readonly FieldInfo _programField = typeof(UdonBehaviour).GetField("_program", BindingFlags.NonPublic | BindingFlags.Instance);

        public UdonHeapStorageInterface(UdonBehaviour udonBehaviour)
        {
            behaviour = udonBehaviour;

            IUdonProgram sourceProgram = (IUdonProgram)_programField.GetValue(udonBehaviour);

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
            if (symbolTable.TryGetAddressFromSymbol(elementKey, out uint symbolAddress))
            {
                var symbolType = heap.GetHeapVariableType(symbolAddress);

                if (symbolType.IsAssignableFrom(typeof(T)))
                {
                    heap.SetHeapVariable<T>(symbolAddress, value);
                }
            }
        }

        T IHeapStorage.GetElementValue<T>(string elementKey)
        {
            if (symbolTable.TryGetAddressFromSymbol(elementKey, out uint symbolAddress))
            {
                var symbolType = heap.GetHeapVariableType(symbolAddress);

                if (symbolType.IsAssignableFrom(typeof(T)))
                {
                    return heap.GetHeapVariable<T>(symbolAddress);
                }
            }

            return default;
        }

        public void SetElementValueWeak(string elementKey, object value)
        {
            if (symbolTable.TryGetAddressFromSymbol(elementKey, out uint symbolAddress))
            {
                var symbolType = heap.GetHeapVariableType(symbolAddress);

                if (symbolType.IsInstanceOfType(value))
                {
                    heap.SetHeapVariable(symbolAddress, value, symbolType);
                }
            }
        }

        public object GetElementValueWeak(string elementKey)
        {
            if (symbolTable.TryGetAddressFromSymbol(elementKey, out uint symbolAddress))
            {
                return heap.GetHeapVariable(symbolAddress);
            }

            return null;
        }
        
        public IValueStorage GetElementStorage(string elementKey)
        {
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)behaviour.programSource;

            if (!programAsset.fieldDefinitions.TryGetValue(elementKey, out Compiler.FieldDefinition fieldDefinition))
            {
                Debug.LogError($"Could not find definition for field {elementKey}");
                return null;
            }

            IValueStorage udonHeapValue = (IValueStorage)System.Activator.CreateInstance(typeof(UdonHeapValueStorage<>).MakeGenericType(fieldDefinition.SystemType), heap, symbolTable, elementKey);

            heapValueRefs.Add(udonHeapValue);

            return udonHeapValue;
        }
    }
}
