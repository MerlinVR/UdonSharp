
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
                FieldChangeCallbackAttribute fieldChangeCallback = variableField.GetCustomAttribute<FieldChangeCallbackAttribute>();

                if (fieldChangeCallback != null)
                {
                    PropertyInfo targetProperty = variableField.DeclaringType.GetProperty(fieldChangeCallback.CallbackPropertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                    if (targetProperty == null)
                        return;

                    MethodInfo setMethod = targetProperty.GetSetMethod(true);

                    if (setMethod == null)
                        return;

                    setMethod.Invoke(this, new object[] { value });
                }
                else
                {
                    variableField.SetValue(this, value);
                }
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

            MethodInfo eventmethod = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(e => e.Name == eventName && e.GetParameters().Length == 0);

            if (eventmethod != null)
            {
                eventmethod.Invoke(this, new object[] { });
            }
        }

        public void SendCustomNetworkEvent(NetworkEventTarget target, string eventName)
        {
            SendCustomEvent(eventName);
        }

        /// <summary>
        /// Executes target event after delaySeconds. If 0.0 delaySeconds is specified, will execute the following frame
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="delaySeconds"></param>
        /// <param name="eventTiming"></param>
        public void SendCustomEventDelayedSeconds(string eventName, float delaySeconds, VRC.Udon.Common.Enums.EventTiming eventTiming = VRC.Udon.Common.Enums.EventTiming.Update) { }

        /// <summary>
        /// Executes target event after delayFrames have passed. If 0 frames is specified, will execute the following frame. In effect 0 frame delay and 1 fame delay are the same on this method.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="delayFrames"></param>
        /// <param name="eventTiming"></param>
        public void SendCustomEventDelayedFrames(string eventName, int delayFrames, VRC.Udon.Common.Enums.EventTiming eventTiming = VRC.Udon.Common.Enums.EventTiming.Update) { }

        /// <summary>
        /// Disables Interact events on this UdonBehaviour and disables the interact outline on the object this is attached to
        /// </summary>
        public bool DisableInteractive { get; set; }

        public static GameObject VRCInstantiate(GameObject original)
        {
            return Instantiate(original);
        }
        
        public void RequestSerialization() { }

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
        public virtual void PostLateUpdate() { }
        public virtual void Interact() { }
        public virtual void OnDrop() { }
        public virtual void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi player) { }
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
        public virtual void OnPlayerRespawn(VRC.SDKBase.VRCPlayerApi player) { }
        
        public virtual void OnPostSerialization(VRC.Udon.Common.SerializationResult result) { }
        public virtual bool OnOwnershipRequest(VRC.SDKBase.VRCPlayerApi requestingPlayer, VRC.SDKBase.VRCPlayerApi requestedOwner) => true;

        public virtual void MidiNoteOn(int channel, int number, int velocity) { }
        public virtual void MidiNoteOff(int channel, int number, int velocity) { }
        public virtual void MidiControlChange(int channel, int number, int value) { }

        public virtual void InputJump(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputUse(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputDrop(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputMoveHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputMoveVertical(float value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputLookHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args) { }
        public virtual void InputLookVertical(float value, VRC.Udon.Common.UdonInputEventArgs args) { }

        [Obsolete("The OnStationEntered() event is deprecated use the OnStationEntered(VRCPlayerApi player) event instead, this event will be removed in a future release.")]
        public virtual void OnStationEntered() { }

        [Obsolete("The OnStationExited() event is deprecated use the OnStationExited(VRCPlayerApi player) event instead, this event will be removed in a future release.")]
        public virtual void OnStationExited() { }

        [Obsolete("The OnOwnershipTransferred() event is deprecated use the OnOwnershipTransferred(VRCPlayerApi player) event instead, this event will be removed in a future release.")]
        public virtual void OnOwnershipTransferred() { }

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
