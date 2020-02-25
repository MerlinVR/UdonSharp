using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
    public abstract class UdonSharpBehaviour : MonoBehaviour
    {
        // Stubs for the UdonBehaviour functions that emulate Udon behavior
        public object GetProgramVariable(string name)
        {
            FieldInfo variableField = GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (variableField == null)
                return null;

            return variableField.GetValue(this);
        }

        public void SetProgramVariable(string name, object value)
        {
            FieldInfo variableField = GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (variableField != null)
            {
                variableField.SetValue(this, value);
            }
        }

        public void SendCustomEvent(string eventName)
        {
            MethodInfo eventmethod = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == eventName && e.GetParameters().Length == 0).FirstOrDefault();

            if (eventmethod != null)
            {
                eventmethod.Invoke(this, new object[] { });
            }
        }

        public void SendCustomNetworkEvent(NetworkEventTarget target, string eventName) { }

        public static GameObject VRCInstantiate(GameObject original)
        {
            return Instantiate(original);
        }

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
