
using JetBrains.Annotations;
using System;

namespace UdonSharp
{
    // At the moment Udon syncing is in a very early state.
    // This is very liable to be changed with changes to Udon syncing in the future.
    [PublicAPI]
    public enum UdonSyncMode
    {
        /// <summary>
        /// Not synced, this is the same as not adding the UdonSynced attribute
        /// </summary>
        NotSynced,
        /// <summary>
        /// Synced with no interpolation, this is the same as just using `[UdonSynced]`
        /// </summary>
        None,
        /// <summary>
        /// Lerped sync
        /// </summary>
        Linear,
        /// <summary>
        /// Smoothed sync
        /// </summary>
        Smooth,
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class UdonSyncedAttribute : Attribute
    {
        private UdonSyncMode networkSyncType;

        public UdonSyncedAttribute(UdonSyncMode networkSyncTypeIn = UdonSyncMode.None)
        {
            networkSyncType = networkSyncTypeIn;
        }
    }
    
    /// <summary>
    /// Used to enforce consistent sync modes per behaviour since Udon Behaviours are usually authored with a specific type of sync in mind
    ///   and it's tedious to set and make sure the sync type is correct on each behaviour.
    /// This also allows U# to verify that you're using types and variable tweening modes that are supported for the given sync mode
    /// </summary>
    [PublicAPI]
    public enum BehaviourSyncMode
    {
        /// <summary>
        /// Nothing is enforced and the behaviours can be set to either sync type by the user. This is the default when no BehaviourSyncTypeAttribute is specified on a behaviour
        /// </summary>
        Any,
        /// <summary>
        /// Enforces no synced variables on the behaviour and hides the selection dropdown in the UI for the sync mode. Nothing is synced and SendCustomNetworkEvent will not work on the behaviour
        /// </summary>
        None,
        /// <summary>
        /// Enforces no synced variables on the behaviour and hides the selection dropdown in the UI for the sync mode, SendCustomNetworkEvent() will still work on this behaviour
        /// </summary>
        NoVariableSync,
        /// <summary>
        /// Enforces continuous sync mode on the behaviour
        /// </summary>
        Continuous,
        /// <summary>
        /// Enforces manual sync mode on the behaviour
        /// </summary>
        Manual,
    }

    [PublicAPI]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class UdonBehaviourSyncModeAttribute : Attribute
    {
        private BehaviourSyncMode behaviourSyncMode = BehaviourSyncMode.Any;

        public UdonBehaviourSyncModeAttribute(BehaviourSyncMode behaviourSyncMode)
        {
            this.behaviourSyncMode = behaviourSyncMode;
        }
    }

    /// <summary>
    /// Marks a method that can be called recursively in U#. 
    /// This should be used on the methods that are being called recursively, you do not need to mark methods that are calling recursive methods with this.
    /// This attribute has a performance overhead which makes the marked method perform slower and usually generate more garbage. So use it only on methods that **need** to be called recursively.
    /// </summary>
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RecursiveMethodAttribute : Attribute
    {
        public RecursiveMethodAttribute()
        { }
    }

    /// <summary>
    /// Calls the target property's setter when the marked field is modified by network sync or SetProgramVariable().
    /// Fields marked with this will instead have the target property's setter called. The setter is expected to set the field if you want the field to change.
    /// </summary>
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class FieldChangeCallbackAttribute : Attribute
    {
        public string CallbackPropertyName { get; private set; }

        private FieldChangeCallbackAttribute() { }

        public FieldChangeCallbackAttribute(string targetPropertyName)
        {
            CallbackPropertyName = targetPropertyName;
        }
    }
}

