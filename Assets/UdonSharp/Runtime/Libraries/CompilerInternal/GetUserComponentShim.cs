
using JetBrains.Annotations;
using UnityEngine;
using VRC.Udon;

namespace UdonSharp.Lib.Internal
{
    public static class GetUserComponentShim
    {
        #region GetComponent
        [UsedImplicitly]
        internal static Component GetComponent(Component instance, long behaviourType)
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponents(typeof(UdonBehaviour));
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == behaviourType)
                    return behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static Component GetComponentInChildren(Component instance, long behaviourType)
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour));
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == behaviourType)
                    return behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static Component GetComponentInChildren(Component instance, long behaviourType, bool includeInactive)
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive);
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == behaviourType)
                    return behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static Component GetComponentInParent(Component instance, long behaviourType)
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour));
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == behaviourType)
                    return behaviour;
            }
            return null;
        }
        
        [UsedImplicitly]
        internal static Component GetComponentInParent(Component instance, long behaviourType, bool includeInactive)
        {
            UdonBehaviour[] udonBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive);
            foreach (UdonBehaviour behaviour in udonBehaviours)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object idValue = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (idValue != null && (long) idValue == behaviourType)
                    return behaviour;
            }
            return null;
        }
        #endregion

        #region GetComponents

        private static UdonBehaviour[] GetComponentsOfType(long behaviourType, UdonBehaviour[] inputArray)
        {
            int arraySize = 0;
            foreach (UdonBehaviour behaviour in inputArray)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object typeID = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (typeID != null && (long) typeID == behaviourType)
                    arraySize++;
            }

            UdonBehaviour[] foundBehaviours = new UdonBehaviour[arraySize];
            int targetIdx = 0;
            
            foreach (UdonBehaviour behaviour in inputArray)
            {
            #if UNITY_EDITOR
                if (behaviour.GetProgramVariableType(CompilerConstants.UsbTypeIDHeapKey) == null)
                    continue;
            #endif
                object typeID = behaviour.GetProgramVariable(CompilerConstants.UsbTypeIDHeapKey);
                if (typeID != null && (long) typeID == behaviourType)
                    foundBehaviours[targetIdx++] = behaviour;
            }

            return foundBehaviours;
        }

        [UsedImplicitly]
        internal static UdonBehaviour[] GetComponents(Component instance, long behaviourType)
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponents(typeof(UdonBehaviour));
            return GetComponentsOfType(behaviourType, instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static UdonBehaviour[] GetComponentsInChildren(Component instance, long behaviourType)
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour));
            return GetComponentsOfType(behaviourType, instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static UdonBehaviour[] GetComponentsInChildren(Component instance, long behaviourType, bool includeInactive)
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInChildren(typeof(UdonBehaviour), includeInactive);
            return GetComponentsOfType(behaviourType, instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static UdonBehaviour[] GetComponentsInParent(Component instance, long behaviourType)
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour));
            return GetComponentsOfType(behaviourType, instanceBehaviours);
        }
        
        [UsedImplicitly]
        internal static UdonBehaviour[] GetComponentsInParent(Component instance, long behaviourType, bool includeInactive)
        {
            UdonBehaviour[] instanceBehaviours = (UdonBehaviour[])instance.GetComponentsInParent(typeof(UdonBehaviour), includeInactive);
            return GetComponentsOfType(behaviourType, instanceBehaviours);
        }

        #endregion
    }
}
