
using System;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Serialization
{
    public class UdonVariableStorageInterface : IHeapStorage
    {
        UdonBehaviour udonBehaviour;
        IUdonVariableTable variableTable;

        public UdonVariableStorageInterface(UdonBehaviour udonBehaviour)
        {
            this.udonBehaviour = udonBehaviour;
            variableTable = udonBehaviour.publicVariables;
        }

        public IValueStorage GetElementStorage(string elementKey)
        {
            throw new NotImplementedException();
        }

        public object GetElementValueWeak(string elementKey)
        {
            throw new NotImplementedException();
        }

        public T GetElementValue<T>(string elementKey)
        {
            T variableVal;
            if (variableTable.TryGetVariableValue<T>(elementKey, out variableVal))
                return variableVal;

            return default;
        }

        public void InvalidateInterface()
        {
            throw new NotImplementedException();
        }

        public void SetElementValueWeak(string elementKey, object value)
        {
            throw new NotImplementedException();
        }

        public void SetElementValue<T>(string elementKey, T value)
        {
            variableTable.TrySetVariableValue<T>(elementKey, value);
        }
        
    }
}
