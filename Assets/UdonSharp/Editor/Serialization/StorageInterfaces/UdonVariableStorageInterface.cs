
using System;
using System.Collections.Generic;
using UdonSharp.Compiler;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using Object = UnityEngine.Object;

namespace UdonSharp.Serialization
{
    public class UdonVariableStorageInterface : IHeapStorage
    {
        private class VariableValueStorage<T> : ValueStorage<T>
        {
            private string elementKey;
            private UdonBehaviour behaviour;

            public VariableValueStorage(string elementKey, UdonBehaviour behaviour)
            {
                this.elementKey = elementKey;
                this.behaviour = behaviour;
            }

            public override T Value
            {
                get => GetVariable<T>(behaviour, elementKey);
                set => SetVariable<T>(behaviour, elementKey, value);
            }
        }

        private static void SetVarInternal<T>(UdonBehaviour behaviour, string variableKey, T value)
        {
            variableKey = UdonSharpUtils.UnmanglePropertyFieldName(variableKey);
            
            if (behaviour.publicVariables.TrySetVariableValue<T>(variableKey, value)) 
                return;
            
            UdonVariable<T> varVal = new UdonVariable<T>(variableKey, value);
            if (!behaviour.publicVariables.TryAddVariable(varVal))
            {
                if (!behaviour.publicVariables.RemoveVariable(variableKey) || !behaviour.publicVariables.TryAddVariable(varVal)) // Fallback in case the value already exists for some reason
                    Debug.LogError($"Could not write variable '{variableKey}' to public variables on UdonBehaviour");
                else
                    Debug.LogWarning($"Storage for variable '{variableKey}' of type '{typeof(T)}' did not match, updated storage type");
            }
        }

        private static void SetVariable<T>(UdonBehaviour behaviour, string variableKey, T value)
        {
            if (UsbSerializationContext.CollectDependencies)
                return;
            
            Type type = typeof(T);

            bool isNull = (value is Object unityEngineObject && unityEngineObject == null) || value == null;

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
            if (behaviour.publicVariables.TryGetVariableValue<T>(variableKey, out T output))
                return output;

            // The type no longer matches exactly, but is trivially convertible
            // This will usually flow into a reassignment of the public variable type in SetVarInternal() when the value gets copied back to Udon
            if (behaviour.publicVariables.TryGetVariableValue(variableKey, out object outputObj) && !outputObj.IsUnityObjectNull() && outputObj is T obj)
                return obj;

            // Try to get the default value if there's no custom value specified
            // We don't care about default values when we're doing dependency checks because the default heap currently does not contain any UnityEngine.Object references.
            // This may change in the future though, so we may want to look at caching only the 'serialized' heap values if that ever becomes the case.
            if (behaviour.programSource != null && behaviour.programSource is UdonSharpProgramAsset udonSharpProgramAsset && !UsbSerializationContext.CollectDependencies && !UsbSerializationContext.UseHeapSerialization)
            {
                udonSharpProgramAsset.UpdateProgram();

                IUdonProgram program = udonSharpProgramAsset.GetRealProgram();

                if (program.SymbolTable.TryGetAddressFromSymbol(variableKey, out uint varAddress))
                {
                    if (program.Heap.TryGetHeapVariable<T>(varAddress, out output))
                        return output;
                }
            }

            return default;
        }

        private UdonBehaviour udonBehaviour;
        private static Dictionary<UdonSharpProgramAsset, Dictionary<string, Type>> _variableTypeLookup = new Dictionary<UdonSharpProgramAsset, Dictionary<string, Type>>();
        private Type GetElementType(string elementKey)
        {
            UdonSharpProgramAsset programAsset = (UdonSharpProgramAsset)udonBehaviour.programSource;

            if (!_variableTypeLookup.TryGetValue(programAsset, out var programTypeLookup))
            {
                programTypeLookup = new Dictionary<string, Type>();
                foreach (FieldDefinition def in programAsset.fieldDefinitions.Values)
                {
                    // if (def.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Public) || def.fieldSymbol.declarationType.HasFlag(SymbolDeclTypeFlags.Private))
                        programTypeLookup.Add(def.Name, def.SystemType);
                }
                _variableTypeLookup.Add(programAsset, programTypeLookup);
            }

            if (!programTypeLookup.TryGetValue(elementKey, out var fieldType))
                return null;

            return fieldType;
        }

        public UdonVariableStorageInterface(UdonBehaviour udonBehaviour)
        {
            this.udonBehaviour = udonBehaviour;
        }

        public IValueStorage GetElementStorage(string elementKey)
        {
            Type elementType = GetElementType(elementKey);
            if (elementType == null)
                return null;

            return (IValueStorage)Activator.CreateInstance(typeof(VariableValueStorage<>).MakeGenericType(elementType), elementKey, udonBehaviour);
        }

        public object GetElementValueWeak(string elementKey)
        {
            udonBehaviour.publicVariables.TryGetVariableValue(elementKey, out object valueOut);
            return valueOut;
        }

        public T GetElementValue<T>(string elementKey)
        {
            if (udonBehaviour.publicVariables.TryGetVariableValue<T>(elementKey, out T variableVal))
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
