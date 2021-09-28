
using JetBrains.Annotations;
using UnityEngine;
using VRC.Udon;

namespace UdonSharp.Lib.Internal
{
    public static class GetUserComponentShim
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
    }
}
