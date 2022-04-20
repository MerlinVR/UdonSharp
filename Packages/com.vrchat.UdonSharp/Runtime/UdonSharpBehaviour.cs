
using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

using System.Diagnostics;
using UnityEngine.Serialization;
using VRC.Udon.Serialization.OdinSerializer;

namespace UdonSharp
{
    public abstract class UdonSharpBehaviour : MonoBehaviour, ISerializationCallbackReceiver, ISupportsPrefabSerialization
    {
        // Stubs for the UdonBehaviour functions that emulate Udon behavior
        
        /// <summary>
        /// Gets a field from the target UdonSharpBehaviour
        /// </summary>
        [PublicAPI]
        public object GetProgramVariable(string name)
        {
            FieldInfo variableField = GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (variableField == null)
                return null;

            return variableField.GetValue(this);
        }

        /// <summary>
        /// Sets a field on the target UdonSharpBehaviour.
        /// <remarks>Make sure you are setting a value on the behaviour with a compatible type to the given <paramref name="value"/>, or you may run into unexpected crashes.</remarks>
        /// </summary>
        [PublicAPI]
        public void SetProgramVariable(string name, object value)
        {
            FieldInfo variableField = GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (variableField == null)
                return;
            
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

        /// <summary>
        /// Calls the method with <paramref name="eventName"/> on the target UdonSharpBehaviour. The target method must be public and have no parameters.
        /// <remarks>The method is allowed to return a value, but the return value will not be accessible via this method.</remarks>
        /// </summary>
        /// <param name="eventName">Name of the method to call</param>
        [PublicAPI]
        public void SendCustomEvent(string eventName)
        {
        #if UNITY_EDITOR
            if (_udonSharpBackingUdonBehaviour != null) // If this is a proxy, we need to check if this is a valid call to SendCustomEvent, since animation events can call it when they shouldn't
            {
                StackFrame frame = new StackFrame(1); // Get the frame of the calling method

                // If the calling method is null, this has been called from native code which indicates it was called by Unity, which we don't want on proxies
                if (frame.GetMethod() == null)
                    return;
            }
        #endif

            MethodInfo eventMethod = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(e => e.Name == eventName && e.GetParameters().Length == 0);

            if (eventMethod != null)
            {
                eventMethod.Invoke(this, new object[] { });
            }
        }

        /// <summary>
        /// Sends a networked call to the method with <paramref name="eventName"/> on the target UdonSharpBehaviour. The target method must be public and have no parameters.
        /// <remarks>The method is allowed to return a value, but the return value will not be accessible via this method.
        /// Methods with an underscore as their first character will not be callable via SendCustomNetworkEvent.</remarks>
        /// </summary>
        /// <param name="target">Whether to send this event to only the owner of the target behaviour's GameObject, or to everyone in the instance</param>
        /// <param name="eventName">Name of the method to call</param>
        [PublicAPI]
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
        [PublicAPI] 
        public void SendCustomEventDelayedSeconds(string eventName, float delaySeconds, VRC.Udon.Common.Enums.EventTiming eventTiming = VRC.Udon.Common.Enums.EventTiming.Update) { }

        /// <summary>
        /// Executes target event after delayFrames have passed. If 0 frames is specified, will execute the following frame. In effect 0 frame delay and 1 fame delay are the same on this method.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="delayFrames"></param>
        /// <param name="eventTiming"></param>
        [PublicAPI] 
        public void SendCustomEventDelayedFrames(string eventName, int delayFrames, VRC.Udon.Common.Enums.EventTiming eventTiming = VRC.Udon.Common.Enums.EventTiming.Update) { }

        /// <summary>
        /// Disables Interact events on this UdonBehaviour and disables the interact outline on the object this is attached to
        /// </summary>
        [PublicAPI] 
        public bool DisableInteractive { get; set; }

        /// <summary>
        /// Access the text that shows up on interactable tooltips
        /// </summary>
        [PublicAPI]
        public string InteractionText { get; set; }

        [Obsolete("This method is obsolete, use Object.Instantiate(gameObject) instead")]
        protected static GameObject VRCInstantiate(GameObject original)
        {
            return Instantiate(original);
        }
        
        /// <summary>
        /// Requests a network serialization of the target UdonSharpBehaviour.
        /// <remarks>This will only function if the UdonSharpBehaviour is set to Manual sync mode and the person calling RequestSerialization() is the owner of the object.</remarks>
        /// </summary>
        [PublicAPI]
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
        [PublicAPI]
        public long GetUdonTypeID()
        {
            return GetUdonTypeID(GetType());
        }

        /// <summary>
        /// Gets the type ID for the given T, usually used for checking UdonSharpBehaviour type equality
        /// </summary>
        [PublicAPI]
        public static long GetUdonTypeID<T>() where T : UdonSharpBehaviour
        {
            return GetUdonTypeID(typeof(T));
        }

        private static string GetUdonTypeName(System.Type type)
        {
            return Internal.UdonSharpInternalUtility.GetTypeName(type);
        }

        [PublicAPI]
        public string GetUdonTypeName()
        {
            return GetUdonTypeName(GetType());
        }

        [PublicAPI]
        public static string GetUdonTypeName<T>() where T : UdonSharpBehaviour
        {
            return GetUdonTypeName(typeof(T));
        }

        // Method stubs for auto completion
        [PublicAPI] public virtual void PostLateUpdate() { }
        [PublicAPI] public virtual void Interact() { }
        [PublicAPI] public virtual void OnDrop() { }
        [PublicAPI] public virtual void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPickup() { }
        [PublicAPI] public virtual void OnPickupUseDown() { }
        [PublicAPI] public virtual void OnPickupUseUp() { }
        [PublicAPI] public virtual void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnSpawn() { }
        [PublicAPI] public virtual void OnStationEntered(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnStationExited(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnVideoEnd() { }
        [PublicAPI] public virtual void OnVideoError(VRC.SDK3.Components.Video.VideoError videoError) { }
        [PublicAPI] public virtual void OnVideoLoop() { }
        [PublicAPI] public virtual void OnVideoPause() { }
        [PublicAPI] public virtual void OnVideoPlay() { }
        [PublicAPI] public virtual void OnVideoReady() { }
        [PublicAPI] public virtual void OnVideoStart() { }
        [PublicAPI] public virtual void OnPreSerialization() { }
        [PublicAPI] public virtual void OnDeserialization() { }
        [PublicAPI] public virtual void OnPlayerTriggerEnter(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerTriggerExit(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerTriggerStay(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerCollisionEnter(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerCollisionExit(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerCollisionStay(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerParticleCollision(VRC.SDKBase.VRCPlayerApi player) { }
        [PublicAPI] public virtual void OnPlayerRespawn(VRC.SDKBase.VRCPlayerApi player) { }
        
        [PublicAPI] public virtual void OnPostSerialization(VRC.Udon.Common.SerializationResult result) { }
        [PublicAPI] public virtual bool OnOwnershipRequest(VRC.SDKBase.VRCPlayerApi requestingPlayer, VRC.SDKBase.VRCPlayerApi requestedOwner) => true;

        [PublicAPI] public virtual void MidiNoteOn(int channel, int number, int velocity) { }
        [PublicAPI] public virtual void MidiNoteOff(int channel, int number, int velocity) { }
        [PublicAPI] public virtual void MidiControlChange(int channel, int number, int value) { }
        
        [PublicAPI] public virtual void InputJump(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputUse(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputGrab(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputDrop(bool value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputMoveHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputMoveVertical(float value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputLookHorizontal(float value, VRC.Udon.Common.UdonInputEventArgs args) { }
        [PublicAPI] public virtual void InputLookVertical(float value, VRC.Udon.Common.UdonInputEventArgs args) { }

        [Obsolete("The OnStationEntered() event is deprecated use the OnStationEntered(VRCPlayerApi player) event instead", true)]
        public virtual void OnStationEntered() { }

        [Obsolete("The OnStationExited() event is deprecated use the OnStationExited(VRCPlayerApi player) event instead", true)]
        public virtual void OnStationExited() { }

        [Obsolete("The OnOwnershipTransferred() event is deprecated use the OnOwnershipTransferred(VRCPlayerApi player) event instead", true)]
        public virtual void OnOwnershipTransferred() { }

        // Used for tracking serialization data in editor
        // Also allows serializing user data on U# behaviours that is otherwise not supported by Unity, so stuff like jagged arrays and more complex collection types.
        [SerializeField, HideInInspector]
        private SerializationData serializationData;
        
        [SerializeField, HideInInspector, UsedImplicitly]
        private VRC.Udon.UdonBehaviour _udonSharpBackingUdonBehaviour;

        // Used to preserve backing behaviour on Reset, paste component value calls, and preset applications
        // http://answers.unity.com/answers/1754330/view.html
        private VRC.Udon.UdonBehaviour _backingUdonBehaviourDump;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
            _backingUdonBehaviourDump = _udonSharpBackingUdonBehaviour;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);

            if (_backingUdonBehaviourDump)
            {
                _udonSharpBackingUdonBehaviour = _backingUdonBehaviourDump;
            }
        }
        
        SerializationData ISupportsPrefabSerialization.SerializationData
        {
            get => serializationData;
            set => serializationData = value;
        }
#pragma warning disable CS0414 // Referenced via reflection
        [UsedImplicitly]
        private static bool _skipEvents;
#pragma warning restore CS0414

        [UsedImplicitly]
        private static bool ShouldSkipEvents() => _skipEvents;
    }
}
