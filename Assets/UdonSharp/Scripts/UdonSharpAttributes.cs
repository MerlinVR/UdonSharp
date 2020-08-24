using System;

namespace UdonSharp
{
    // At the moment Udon syncing is in a very early state.
    // This is very liable to be changed with changes to Udon syncing in the future.
    public enum UdonSyncMode
    {
        NotSynced,
        None, // No interpolation
        Linear, // Lerp
        Smooth, // Some kind of smoothed syncing, no idea what curve they apply to it
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class UdonSyncedAttribute : Attribute
    {
        private UdonSyncMode networkSyncType;

        public UdonSyncedAttribute(UdonSyncMode networkSyncTypeIn = UdonSyncMode.None)
        {
            networkSyncType = networkSyncTypeIn;
        }
    }

#if UDON_BETA_SDK
    public enum BehaviourSyncMode
    {
        Any, // Nothing is enforced and the behaviours can be set to either sync type by the user. This is the default when no BehaviourSyncTypeAttribute is specified
        Continuous,
        Manual,
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class UdonBehaviourSyncModeAttribute : Attribute
    {
        private BehaviourSyncMode behaviourSyncMode = BehaviourSyncMode.Any;

        public UdonBehaviourSyncModeAttribute(BehaviourSyncMode behaviourSyncMode)
        {
            this.behaviourSyncMode = behaviourSyncMode;
        }
    }
#endif
}

