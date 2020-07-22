
using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp.Serialization
{
    public class UdonVariableStorageInterface : IHeapStorage
    {
        class VariableValueStorage<T> : ValueStorage<T>
        {
            public string elementKey;
            public IUdonVariableTable table;

            public VariableValueStorage(string elementKey, IUdonVariableTable table)
            {
                this.elementKey = elementKey;
                this.table = table;
            }

            public override T Value
            {
                get
                {
                    return GetVariable<T>(table, elementKey);
                }
                set
                {
                    SetVariable<T>(table, elementKey, value);
                }
            }
        }

        private static void SetVariable<T>(IUdonVariableTable table, string variableKey, T value)
        {
            System.Type type = typeof(T);
            if (type == typeof(GameObject) ||
                type == typeof(Transform) ||
                type == typeof(UdonBehaviour))
            {
                if (value == null)
                {
                    table.RemoveVariable(variableKey);
                }
                else
                {
                    if (!table.TrySetVariableValue<T>(variableKey, value))
                    {
                        UdonVariable<T> varVal = new UdonVariable<T>(variableKey, value);
                        if (!table.TryAddVariable(varVal))
                        {
                            Debug.LogError($"Could not write variable '{variableKey}' to public variables on UdonBehaviour");
                        }
                    }
                }
            }
            else
            {
                table.TrySetVariableValue<T>(variableKey, value);
            }
        }

        private static T GetVariable<T>(IUdonVariableTable table, string variableKey)
        {
            T output;
            if (table.TryGetVariableValue<T>(variableKey, out output))
                return output;

            return default;
        }

        UdonBehaviour udonBehaviour;
        IUdonVariableTable variableTable;

        public UdonVariableStorageInterface(UdonBehaviour udonBehaviour)
        {
            this.udonBehaviour = udonBehaviour;
            variableTable = udonBehaviour.publicVariables;
        }

        public IValueStorage GetElementStorage(string elementKey)
        {
            System.Type elementType;
            if (!variableTable.TryGetVariableType(elementKey, out elementType))
                return null;

            return (IValueStorage)System.Activator.CreateInstance(typeof(VariableValueStorage<>).MakeGenericType(elementType), elementKey, variableTable);
        }

        public object GetElementValueWeak(string elementKey)
        {
            object valueOut;
            variableTable.TryGetVariableValue(elementKey, out valueOut);
            return valueOut;
        }

        public T GetElementValue<T>(string elementKey)
        {
            T variableVal;
            if (variableTable.TryGetVariableValue<T>(elementKey, out variableVal))
                return variableVal;

            return default;
        }

        public void SetElementValueWeak(string elementKey, object value)
        {
            variableTable.TrySetVariableValue(elementKey, value);
        }

        public void SetElementValue<T>(string elementKey, T value)
        {
            variableTable.TrySetVariableValue<T>(elementKey, value);
        }
        
    }
}
