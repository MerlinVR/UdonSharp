
using System.Collections.Generic;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Serialization
{
    public class UdonHeapStorageInterface : IHeapStorage
    {
        class UdonHeapValueStorage<T> : ValueStorage<T>
        {
            IUdonHeap heap;
            IUdonSymbolTable symbolTable;
            uint symbolAddress;
            bool isValid;

            public UdonHeapValueStorage(IUdonHeap heap, IUdonSymbolTable symbolTable, string symbolKey)
            {
                this.heap = heap;
                this.symbolTable = symbolTable;
                
                isValid = symbolTable.TryGetAddressFromSymbol(symbolKey, out symbolAddress) && 
                          heap.GetHeapVariableType(symbolAddress) == typeof(T) &&
                          heap.TryGetHeapVariable<T>(symbolAddress, out var validityCheckPlaceholder);
            }

            public override T Value
            {
                get
                {
                    if (!isValid)
                        return default;

                    return heap.GetHeapVariable<T>(symbolAddress);
                }
                set
                {
                    if (!isValid)
                        return;

                    heap.SetHeapVariable<T>(symbolAddress, value);
                }
            }

            public override void InvalidateStorage()
            {
                throw new System.NotImplementedException();
            }
        }


        IUdonHeap heap;
        IUdonSymbolTable symbolTable;
        IUdonProgram sourceProgram;
        List<IValueStorage> heapValueRefs = new List<IValueStorage>();

        public UdonHeapStorageInterface(IUdonProgram program)
        {
            sourceProgram = program;

            heap = program.Heap;
            symbolTable = program.SymbolTable;
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
            foreach (IValueStorage value in heapValueRefs)
            {
                value.InvalidateStorage();
            }
        }

        public IValueStorage GetElementStorage(string elementKey)
        {
            IValueStorage udonHeapValue = (IValueStorage)System.Activator.CreateInstance(heap.GetHeapVariableType(symbolTable.GetAddressFromSymbol(elementKey)), heap, symbolTable, elementKey);

            heapValueRefs.Add(udonHeapValue);

            return udonHeapValue;
        }
    }
}
