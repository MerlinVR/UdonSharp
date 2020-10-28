using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

#if UNITY_EDITOR
using System.Diagnostics;
using VRC.Udon.Serialization.OdinSerializer;
#endif

namespace UdonSharp
{
    public abstract class UdonSharpBehaviour : MonoBehaviour
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
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
#if UNITY_EDITOR
            if (_backingUdonBehaviour != null) // If this is a proxy, we need to check if this is a valid call to SendCustomEvent, since animation events can call it when they shouldn't
            {
                StackFrame frame = new StackFrame(1); // Get the frame of the calling method

                // If the calling method is null, this has been called from native code which indicates it was called by Unity, which we don't want on proxies
                if (frame.GetMethod() == null)
                    return;
            }
#endif

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

#if UDON_BETA_SDK
        public void RequestSerialization() { }
#endif

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
        public virtual void OnStationEntered(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnStationExited(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnVideoEnd() { }
        public virtual void OnVideoError(VRC.SDK3.Components.Video.VideoError videoError) { }
        public virtual void OnVideoLoop() { }
        public virtual void OnVideoPause() { }
        public virtual void OnVideoPlay() { }
        public virtual void OnVideoReady() { }
        public virtual void OnVideoStart() { }
        public virtual void OnPreSerialization() { }
        public virtual void OnDeserialization() { }
        public virtual void OnPlayerTriggerEnter(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerTriggerExit(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerTriggerStay(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerCollisionEnter(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerCollisionExit(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerCollisionStay(VRC.SDKBase.VRCPlayerApi player) { }
        public virtual void OnPlayerParticleCollision(VRC.SDKBase.VRCPlayerApi player) { }
#if UDON_BETA_SDK
        public virtual bool OnOwnershipRequest(VRC.SDKBase.VRCPlayerApi requestingPlayer, VRC.SDKBase.VRCPlayerApi requestedOwner) => true;
#endif

        [Obsolete("The OnStationEntered() event is deprecated use the OnStationEntered(VRCPlayerApi player) event instead, this event will be removed in a future release.")]
        public virtual void OnStationEntered() { }

        [Obsolete("The OnStationExited() event is deprecated use the OnStationExited(VRCPlayerApi player) event instead, this event will be removed in a future release.")]
        public virtual void OnStationExited() { }

#if UNITY_EDITOR
        // Used for tracking serialization data in editor
        // Odin serialization is needed to keep track of the _backingUdonBehaviour reference for undo/redo operations
        [SerializeField, HideInInspector]
        SerializationData serializationData;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
        }
        
        [OdinSerialize]
        private IUdonBehaviour _backingUdonBehaviour = null;

#pragma warning disable CS0414 // Referenced via reflection
        [SerializeField, HideInInspector]
        private bool _isValidForAutoCopy = false;

        private static bool _skipEvents = false;
#pragma warning restore CS0414

        private static bool ShouldSkipEvents() => _skipEvents;
#endif
    }
}
