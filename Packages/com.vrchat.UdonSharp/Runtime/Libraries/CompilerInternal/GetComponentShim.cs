
using System;
using JetBrains.Annotations;
using UnityEngine;
using VRC.Udon;

// These are invalid in C#, but valid in U# because UdonSharpBehaviours are weakly considered UdonBehaviours
// ReSharper disable PossibleInvalidCastException
// ReSharper disable SuspiciousTypeConversion.Global

namespace UdonSharp.Lib.Internal
{
    public static class GetComponentShim
    {
    #region GetComponent
        [UsedImplicitly]
        internal static T GetComponent<T>(Component instance) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponents(typeof(UdonBehaviour));
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == targetID)
                    return (T)(Component)behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static T GetComponentInChildren<T>(Component instance) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour));
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == targetID)
                    return (T)(Component)behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static T GetComponentInChildren<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive);
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == targetID)
                    return (T)(Component)behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static T GetComponentInParent<T>(Component instance) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour));
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == targetID)
                    return (T)(Component)behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static T GetComponentInParent<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive);
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == targetID)
                    return (T)(Component)behaviour;
            }
            return null;
        }
    #endregion

    #region GetComponents

        private static T[] GetComponentsOfType<T>(UdonBehaviour[] inputArray) where T : UdonSharpBehaviour
        {
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            int arraySize = 0;
            foreach (UdonBehaviour behaviour in inputArray)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object typeID = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (typeID != null && (long) typeID == targetID)
                    arraySize++;
            }

            Component[] foundBehaviours = new Component[arraySize];
            int targetIdx = 0;
            
            foreach (UdonBehaviour behaviour in inputArray)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object typeID = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (typeID != null && (long) typeID == targetID)
                    foundBehaviours[targetIdx++] = behaviour;
            }

            return (T[])foundBehaviours;
        }

        [UsedImplicitly]
        internal static T[] GetComponents<T>(Component instance) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponents(typeof(UdonBehaviour));
            return GetComponentsOfType<T>(instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInChildren<T>(Component instance) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour));
            return GetComponentsOfType<T>(instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInChildren<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive);
            return GetComponentsOfType<T>(instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInParent<T>(Component instance) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour));
            return GetComponentsOfType<T>(instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInParent<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive);
            return GetComponentsOfType<T>(instanceBehaviours);
        }

    #endregion

    #region Get UdonSharpBehaviour components
        // For doing GetComponent(s)<UdonSharpBehaviour>() specifically, just checks for existence of ID variable
        private static UdonSharpBehaviour GetUdonSharpComponent(Component[] behaviours)
        {
            foreach (UdonBehaviour behaviour in (UdonBehaviour[])behaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null)
                    return (UdonSharpBehaviour)(Component)behaviour;
            }
            
            return null;
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour GetComponentUSB(Component instance)
        {
            return GetUdonSharpComponent(instance.GetComponents(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour GetComponentInChildrenUSB(Component instance)
        {
            return GetUdonSharpComponent(instance.GetComponentsInChildren(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour GetComponentInChildrenUSB(Component instance, bool includeInactive)
        {
            return GetUdonSharpComponent(instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive));
        }
        
        [UsedImplicitly]
        internal static UdonSharpBehaviour GetComponentInParentUSB(Component instance)
        {
            return GetUdonSharpComponent(instance.GetComponentsInParent(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour GetComponentInParentUSB(Component instance, bool includeInactive)
        {
            return GetUdonSharpComponent(instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive));
        }
        
        // GetComponents
        private static UdonSharpBehaviour[] GetUdonSharpComponents(Component[] inputArray)
        {
            int arraySize = 0;
            foreach (UdonBehaviour behaviour in (UdonBehaviour[])inputArray)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object typeID = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (typeID != null)
                    arraySize++;
            }

            Component[] foundBehaviours = new Component[arraySize];
            int targetIdx = 0;
            
            foreach (UdonBehaviour behaviour in (UdonBehaviour[])inputArray)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object typeID = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (typeID != null)
                    foundBehaviours[targetIdx++] = behaviour;
            }

            return (UdonSharpBehaviour[])foundBehaviours;
        }
        
        [UsedImplicitly]
        internal static UdonSharpBehaviour[] GetComponentsUSB(Component instance)
        {
            return GetUdonSharpComponents(instance.GetComponents(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour[] GetComponentsInChildrenUSB(Component instance)
        {
            return GetUdonSharpComponents(instance.GetComponentsInChildren(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour[] GetComponentsInChildrenUSB(Component instance, bool includeInactive)
        {
            return GetUdonSharpComponents(instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive));
        }
        
        [UsedImplicitly]
        internal static UdonSharpBehaviour[] GetComponentsInParentUSB(Component instance)
        {
            return GetUdonSharpComponents(instance.GetComponentsInParent(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static UdonSharpBehaviour[] GetComponentsInParentUSB(Component instance, bool includeInactive)
        {
            return GetUdonSharpComponents(instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive));
        }
    #endregion

    #region Get user component that has some inheritance
        // Types with inheritance build a type array that we can do a lookup into
        private static T GetUdonSharpComponentInherited<T>(Component[] behaviours) where T : UdonSharpBehaviour
        {
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            foreach (UdonBehaviour behaviour in (UdonBehaviour[])behaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDArrayHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDArrayHeapKey);
                if (idValue != null)
                {
                    if (Array.IndexOf((Array)idValue, targetID) != -1)
                        return (T)(Component)behaviour;
                }
            }
            
            return null;
        }
        
        [UsedImplicitly]
        internal static T GetComponentI<T>(Component instance) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentInherited<T>(instance.GetComponents(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static T GetComponentInChildrenI<T>(Component instance) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentInherited<T>(instance.GetComponentsInChildren(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static T GetComponentInChildrenI<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentInherited<T>(instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive));
        }
        
        [UsedImplicitly]
        internal static T GetComponentInParentI<T>(Component instance) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentInherited<T>(instance.GetComponentsInParent(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static T GetComponentInParentI<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentInherited<T>(instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive));
        }
        
        // GetComponents
        private static T[] GetUdonSharpComponentsInherited<T>(Component[] behaviours) where T : UdonSharpBehaviour
        {
            long targetID = UdonSharpBehaviour.GetUdonTypeID<T>();
            
            int arraySize = 0;
            foreach (UdonBehaviour behaviour in (UdonBehaviour[])behaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDArrayHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDArrayHeapKey);
                if (idValue != null)
                {
                    if (Array.IndexOf((Array)idValue, targetID) != -1)
                        arraySize++;
                }
            }

            Component[] foundBehaviours = new Component[arraySize];
            int targetIdx = 0;
            
            foreach (UdonBehaviour behaviour in (UdonBehaviour[])behaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDArrayHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDArrayHeapKey);
                if (idValue != null)
                {
                    if (Array.IndexOf((Array)idValue, targetID) != -1)
                        foundBehaviours[targetIdx++] = behaviour;
                }
            }

            return (T[])foundBehaviours;
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsI<T>(Component instance) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentsInherited<T>(instance.GetComponents(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static T[] GetComponentsInChildrenI<T>(Component instance) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentsInherited<T>(instance.GetComponentsInChildren(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static T[] GetComponentsInChildrenI<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentsInherited<T>(instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive));
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInParentI<T>(Component instance) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentsInherited<T>(instance.GetComponentsInParent(typeof(UdonBehaviour)));
        }

        [UsedImplicitly]
        internal static T[] GetComponentsInParentI<T>(Component instance, bool includeInactive) where T : UdonSharpBehaviour
        {
            return GetUdonSharpComponentsInherited<T>(instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive));
        }
    #endregion

    #region Broken VRC component getcomponent workaround

        [UsedImplicitly]
        internal static T GetComponentVRC<T>(Component instance) where T : Component
        {
            return (T)instance.GetComponent(typeof(T));
        }
        
        [UsedImplicitly]
        internal static T GetComponentInChildrenVRC<T>(Component instance) where T : Component
        {
            return (T)instance.GetComponentInChildren(typeof(T));
        }
        
        [UsedImplicitly]
        internal static T GetComponentInChildrenVRC<T>(Component instance, bool includeInactive) where T : Component
        {
            return (T)instance.GetComponentInChildren(typeof(T), includeInactive);
        }
        
        [UsedImplicitly]
        internal static T GetComponentInParentVRC<T>(Component instance) where T : Component
        {
            return (T)instance.GetComponentInParent(typeof(T));
        }

        private static T[] ConvertToTArray<T>(Component[] components) where T : Component
        {
            T[] newArr = new T[components.Length];
            Array.Copy(components, newArr, components.Length);

            return newArr;
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsVRC<T>(Component instance) where T : Component
        {
            return ConvertToTArray<T>(instance.GetComponents(typeof(T)));
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInChildrenVRC<T>(Component instance) where T : Component
        {
            return ConvertToTArray<T>(instance.GetComponentsInChildren(typeof(T)));
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInChildrenVRC<T>(Component instance, bool includeInactive) where T : Component
        {
            return ConvertToTArray<T>(instance.GetComponentsInChildren(typeof(T), includeInactive));
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInParentVRC<T>(Component instance) where T : Component
        {
            return ConvertToTArray<T>(instance.GetComponentsInParent(typeof(T)));
        }
        
        [UsedImplicitly]
        internal static T[] GetComponentsInParentVRC<T>(Component instance, bool includeInactive) where T : Component
        {
            return ConvertToTArray<T>(instance.GetComponentsInParent(typeof(T), includeInactive));
        }

    #endregion
    }
}
