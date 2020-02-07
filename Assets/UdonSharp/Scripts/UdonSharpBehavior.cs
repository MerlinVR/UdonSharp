using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    public abstract class UdonSharpBehavior : MonoBehaviour
    {
        // Stubs for the UdonBehaviour functions
        public object GetProgramVariable(string name) { return null; }
        public void SetProgramVariable(string name, object value) { }
        public void SendCustomEvent(string eventName) { }
        public void SendCustomNetworkEvent(NetworkEventTarget target, string eventName) { }
        public static GameObject VRCInstantiate(GameObject original) { return null; }

        // Method stubs for auto completion
        public virtual void Interact() { }
        public virtual void OnDrop() { }
        public virtual void OnOwnershipTransferred() { }
        public virtual void OnPickup() { }
        public virtual void OnPickupUseDown() { }
        public virtual void OnPickupUseUp() { }
        public virtual void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnSpawn() { }
        public virtual void OnStationEntered() { }
        public virtual void OnStationExited() { }
        public virtual void OnVideoEnd() { }
        public virtual void OnVideoPause() { }
        public virtual void OnVideoPlay() { }
        public virtual void OnVideoStart() { }
        public virtual void OnPreSerialization() { }
        public virtual void OnDeserialization() { }
    }
}
