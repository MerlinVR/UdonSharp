using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace UdonSharp
{
#if false
    public class UdonSharpBehaviourMetadata
    {
        public int ScriptVersion { get; } = 0;
        public System.DateTime ScriptCompileDate { get; } = System.DateTime.Now;
        public string CompilerVersion { get; } = "v0.0.0+0";
        public string CompilerName { get; } = "Roslyn C# compiler"; // Just assume people are on the correct runtime version for Udon, since other runtimes won't compile anyways
        public int CompilerMajorVersion { get; } = 0;
        public int CompilerMinorVersion { get; } = 0;
        public int CompilerPatchVersion { get; } = 0;
        public int CompilerBuild { get; } = 0;
    }
#endif

    public abstract class UdonSharpBehaviour : MonoBehaviour
    {
        //protected UdonSharpBehaviourMetadata UdonMetadata { get; } = new UdonSharpBehaviourMetadata();

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

        // Stubs for builtin UdonSharp methods to get type info
        private static long GetUdonTypeID(System.Type type)
        {
            return Internal.UdonSharpInternalUtility.GetTypeID(type);
        }

        /// <summary>
        /// Returns the unique ID of the UdonBehavior user type. Will return 0 if the UdonBehavior has no ID, which usually means that it's a graph program.
        /// </summary>
        /// <returns></returns>
        public long GetUdonTypeID()
        {
            return GetUdonTypeID(GetType());
        }

        public static long GetUdonTypeID<T>()
        {
            return GetUdonTypeID(typeof(T));
        }

        private static string GetUdonTypeName(System.Type type)
        {
            return Internal.UdonSharpInternalUtility.GetTypeName(type);
        }

        public string GetUdonTypeName()
        {
            return GetUdonTypeName(GetType());
        }

        public static string GetUdonTypeName<T>()
        {
            return GetUdonTypeName(typeof(T));
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
