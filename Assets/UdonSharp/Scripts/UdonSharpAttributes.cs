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
}

