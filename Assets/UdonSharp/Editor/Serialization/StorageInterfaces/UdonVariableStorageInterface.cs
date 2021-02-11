
using System;
using System.Collections.Generic;
using UdonSharp.Compiler;
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
            public UdonBehaviour behaviour;

            public VariableValueStorage(string elementKey, UdonBehaviour behaviour)
            {
                this.elementKey = elementKey;
                this.behaviour = behaviour;
            }

            public override T Value
            {
                get
                {
                    return GetVariable<T>(behaviour, elementKey); 
                }
                set
                {
                    SetVariable<T>(behaviour, elementKey, value);
                }
            }
        }

        private static void SetVarInternal<T>(UdonBehaviour behaviour, string variableKey, T value)
        {
            if (!behaviour.publicVariables.TrySetVariableValue<T>(variableKey, value))
            {
                UdonVariable<T> varVal = new UdonVariable<T>(variableKey, value);
                if (!behaviour.publicVariables.TryAddVariable(varVal))
                {
                    if (!behaviour.publicVariables.RemoveVariable(variableKey) || !behaviour.publicVariables.TryAddVariable(varVal)) // Fallback in case the value already exists for some reason
                        Debug.LogError($"Could not write variable '{variableKey}' to public variables on UdonBehaviour");
                    else
                        Debug.LogWarning($"Storage for variable '{variableKey}' of type '{typeof(T)}' did not match, updated storage type");
                }
            }
        }

        private static void SetVariable<T>(UdonBehaviour behaviour, string variableKey, T value)
        {
            System.Type type = typeof(T);

            bool isNull = false;
            if ((value is UnityEngine.Object unityEngineObject && unityEngineObject == null) || value == null)
                isNull = true;

            if (isNull)
            {
                bool isRemoveType = (type == typeof(GameObject) ||
                                     type == typeof(Transform) ||
                                     type == typeof(UdonBehaviour));

                if (isRemoveType)
                    behaviour.publicVariables.RemoveVariable(variableKey);
                else
                    SetVarInternal<T>(behaviour, variableKey, value);
            }
            else
            {
                SetVarInternal<T>(behaviour, variableKey, value);
            }
        }

        private static T GetVariable<T>(UdonBehaviour behaviour, string variableKey)
        {
            T output;
            if (behaviour.publicVariables.TryGetVariableValue<T>(variableKey, out output))
                return output;

            // The type no longer matches exactly, but is trivially convertible
            // This will usually flow into a reassignment of the public variable type in SetVarInternal() when the value gets copied back to Udon
            if (behaviour.publicVariables.TryGetVariableValue(variableKey, out object outputObj) && !outputObj.IsUnityObjectNull() && outputObj is T)
                return (T)outputObj;

            // Try to get the default value if there's no custom value specified
            if (behaviour.programSource != null && behaviour.programSource is UdonSharpProgramAsset udonSharpProgramAsset)
            {
                udonSharpProgramAsset.UpdateProgram();

                IUdonProgram program = udonSharpProgramAsset.GetRealProgram();

                uint varAddress;
                if (program.SymbolTable.TryGetAddressFromSymbol(variableKey, out varAddress))
                {
                    if (program.Heap.TryGetHeapVariable<T>(varAddress, out output))
                        return output;
                }
            }

            return default;
        }

        UdonBehaviour udonBehaviour;
        static Dictionary<UdonSharpProgramAsset, Dictionary<string, System.Type>> variableTypeLookup = new Dictionary<UdonSharpProgramAsset, Dictionary<string, Type>>();
        private System.Type GetElementType(string elementKey)
        {
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)udonBehaviour.programSource;

            Dictionary<string, System.Type> programTypeLookup;
            if (!variableTypeLookup.TryGetValue(programAsset, out programTypeLookup))
            {
                programTypeLookup = new Dictionary<string, Type>();
                foreach (FieldDefinition def in programAsset.fieldDefinitions.Values)
                {
                    if (def.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public) || def.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Private))
                        programTypeLookup.Add(def.fieldSymbol.symbolOriginalName, def.fieldSymbol.symbolCsType);
                }
                variableTypeLookup.Add(programAsset, programTypeLookup);
            }

            System.Type fieldType;
            if (!programTypeLookup.TryGetValue(elementKey, out fieldType))
                return null;

            return fieldType;
        }

        public UdonVariableStorageInterface(UdonBehaviour udonBehaviour)
        {
            this.udonBehaviour = udonBehaviour;
        }

        public IValueStorage GetElementStorage(string elementKey)
        {
            System.Type elementType = GetElementType(elementKey);
            if (elementType == null)
                return null;

            return (IValueStorage)System.Activator.CreateInstance(typeof(VariableValueStorage<>).MakeGenericType(elementType), elementKey, udonBehaviour);
        }

        public object GetElementValueWeak(string elementKey)
        {
            object valueOut;
            udonBehaviour.publicVariables.TryGetVariableValue(elementKey, out valueOut);
            return valueOut;
        }

        public T GetElementValue<T>(string elementKey)
        {
            T variableVal;
            if (udonBehaviour.publicVariables.TryGetVariableValue<T>(elementKey, out variableVal))
                return variableVal;

            return default;
        }

        public void SetElementValueWeak(string elementKey, object value)
        {
            udonBehaviour.publicVariables.TrySetVariableValue(elementKey, value);
        }

        public void SetElementValue<T>(string elementKey, T value)
        {
            udonBehaviour.publicVariables.TrySetVariableValue<T>(elementKey, value);
        }
        
    }
}
