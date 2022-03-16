
using System;
using System.Linq;
using JetBrains.Annotations;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharpEditor
{
    public static class UdonSharpComponentExtensions
    {
    #region Serialization Helper extensions
        /// <summary>
        /// Updates the proxy representation from the underlying UdonBehaviour state
        /// </summary>
        /// <param name="behaviour"></param>
        [PublicAPI]
        public static void UpdateProxy(this UdonSharpBehaviour behaviour)
        {
            UdonSharpEditorUtility.CopyUdonToProxy(behaviour);
        }

        /// <summary>
        /// Updates the proxy representation from the underlying UdonBehaviour state
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="serializationPolicy"></param>
        [PublicAPI]
        public static void UpdateProxy(this UdonSharpBehaviour behaviour, ProxySerializationPolicy serializationPolicy)
        {
            UdonSharpEditorUtility.CopyUdonToProxy(behaviour, serializationPolicy);
        }
        
        /// <summary>
        /// Writes changes to the proxy's data to the underlying UdonBehaviour
        /// </summary>
        /// <param name="behaviour"></param>
        [PublicAPI]
        public static void ApplyProxyModifications(this UdonSharpBehaviour behaviour)
        {
            UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        }

        /// <summary>
        /// Writes changes to the proxy's data to the underlying UdonBehaviour
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="serializationPolicy"></param>
        [PublicAPI]
        public static void ApplyProxyModifications(this UdonSharpBehaviour behaviour, ProxySerializationPolicy serializationPolicy)
        {
            UdonSharpEditorUtility.CopyProxyToUdon(behaviour, serializationPolicy);
        }
    #endregion

    #region AddComponent
        [PublicAPI]
        public static UdonSharpBehaviour AddUdonSharpComponent(this GameObject gameObject, System.Type type)
        {
            if (type == typeof(UdonSharpBehaviour))
                throw new System.ArgumentException("Cannot add components of type 'UdonSharpBehaviour', you can only add subclasses of this type");

            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(type))
                throw new System.ArgumentException("Type for AddUdonSharpComponent must be a subclass of UdonSharpBehaviour");

            UdonBehaviour udonBehaviour = gameObject.AddComponent<UdonBehaviour>();

            UdonSharpProgramAsset programAsset = UdonSharpProgramAsset.GetProgramAssetForClass(type);

            udonBehaviour.programSource = programAsset;
#pragma warning disable CS0618 // Type or member is obsolete
            udonBehaviour.SynchronizePosition = false;
            udonBehaviour.AllowCollisionOwnershipTransfer = false;
#pragma warning restore CS0618 // Type or member is obsolete

            switch (programAsset.behaviourSyncMode)
            {
                case BehaviourSyncMode.Continuous:
                    udonBehaviour.SyncMethod = Networking.SyncType.Continuous;
                    break;
                case BehaviourSyncMode.Manual:
                    udonBehaviour.SyncMethod = Networking.SyncType.Manual;
                    break;
                case BehaviourSyncMode.None:
                    udonBehaviour.SyncMethod = Networking.SyncType.None;
                    break;
            }

            SerializedObject componentAsset = new SerializedObject(udonBehaviour);
            SerializedProperty serializedProgramAssetProperty = componentAsset.FindProperty("serializedProgramAsset");

            serializedProgramAssetProperty.objectReferenceValue = programAsset.SerializedProgramAsset;
            componentAsset.ApplyModifiedPropertiesWithoutUndo();

            UdonSharpBehaviour proxyComponent = UdonSharpEditorUtility.GetProxyBehaviour(udonBehaviour);

            if (EditorApplication.isPlaying)
                udonBehaviour.InitializeUdonContent();

            return proxyComponent;
        }

        [PublicAPI]
        public static T AddUdonSharpComponent<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            (T)AddUdonSharpComponent(gameObject, typeof(T));
    #endregion

    #region Obsolete GetComponent APIs

        private static UdonSharpBehaviour[] CastArray(this Component[] components) => components.OfType<UdonSharpBehaviour>().ToArray();

    #region GetComponent
        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponent<T>(this GameObject gameObject) where T : UdonSharpBehaviour => 
            gameObject.GetComponent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponent<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            gameObject.GetComponent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponent(this GameObject gameObject, Type type) => 
            gameObject.GetComponent(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponent(this GameObject gameObject, Type type, ProxySerializationPolicy proxySerializationPolicy) => 
            gameObject.GetComponent(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponent<T>(this Component component) where T : UdonSharpBehaviour => 
            component.GetComponent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponent<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            component.GetComponent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponent(this Component component, Type type) => 
            component.GetComponent(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponent(this Component component, Type type, ProxySerializationPolicy proxySerializationPolicy) => 
            component.GetComponent(type) as UdonSharpBehaviour;
    #endregion

    #region GetComponents
        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponents<T>(this GameObject gameObject) where T : UdonSharpBehaviour => 
            gameObject.GetComponents<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponents<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            gameObject.GetComponents<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this GameObject gameObject, Type type) => 
            gameObject.GetComponents(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this GameObject gameObject, Type type, ProxySerializationPolicy proxySerializationPolicy) => 
            gameObject.GetComponents(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponents<T>(this Component component) where T : UdonSharpBehaviour => 
            component.GetComponents<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponents<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            component.GetComponents<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this Component component, Type type) => 
            component.GetComponents(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this Component component, Type type, ProxySerializationPolicy proxySerializationPolicy) => 
            component.GetComponents(type).CastArray();
    #endregion

    #region GetComponentInChildren
        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject) where T : UdonSharpBehaviour => 
            gameObject.GetComponentInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            gameObject.GetComponentInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, Type type) => 
            gameObject.GetComponentInChildren(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, Type type, ProxySerializationPolicy proxySerializationPolicy) => 
            gameObject.GetComponentInChildren(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject, bool includeInactive) where T : UdonSharpBehaviour => 
            gameObject.GetComponentInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            gameObject.GetComponentInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, Type type, bool includeInactive) =>
            gameObject.GetComponentInChildren(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) => 
            gameObject.GetComponentInChildren(type, includeInactive) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this Component component) where T : UdonSharpBehaviour => 
            component.GetComponentInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            component.GetComponentInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, Type type) => 
            component.GetComponentInChildren(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentInChildren(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this Component component, bool includeInactive) where T : UdonSharpBehaviour =>
            component.GetComponentInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInChildren<T>(this Component component, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            component.GetComponentInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, Type type, bool includeInactive) =>
            component.GetComponentInChildren(type, includeInactive) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentInChildren(type, includeInactive) as UdonSharpBehaviour;
    #endregion

    #region GetComponentsInChildren
        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, Type type) =>
            gameObject.GetComponentsInChildren(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            gameObject.GetComponentsInChildren(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject, bool includeInactive) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, Type type, bool includeInactive) =>
            gameObject.GetComponentsInChildren(type, includeInactive).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            gameObject.GetComponentsInChildren(type, includeInactive).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component) where T : UdonSharpBehaviour =>
            component.GetComponentsInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            component.GetComponentsInChildren<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, Type type) =>
            component.GetComponentsInChildren(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentsInChildren(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component, bool includeInactive) where T : UdonSharpBehaviour =>
            component.GetComponentsInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            component.GetComponentsInChildren<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, Type type, bool includeInactive) =>
            component.GetComponentsInChildren(type, includeInactive).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentsInChildren(type, includeInactive).CastArray();
    #endregion

    #region GetComponentInParent
        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInParent<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            gameObject.GetComponentInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInParent<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            gameObject.GetComponentInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this GameObject gameObject, Type type) =>
            gameObject.GetComponentInParent(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this GameObject gameObject, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            gameObject.GetComponentInParent(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInParent<T>(this Component component) where T : UdonSharpBehaviour =>
            component.GetComponentInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T GetUdonSharpComponentInParent<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            component.GetComponentInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this Component component, Type type) =>
            component.GetComponentInParent(type) as UdonSharpBehaviour;

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this Component component, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentInParent(type) as UdonSharpBehaviour;
    #endregion

    #region GetComponentsInParent
        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, Type type) =>
            gameObject.GetComponentsInParent(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            gameObject.GetComponentsInParent(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject, bool includeInactive) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInParent<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            gameObject.GetComponentsInParent<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, Type type, bool includeInactive) =>
            gameObject.GetComponentsInParent(type, includeInactive).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            gameObject.GetComponentsInParent(type, includeInactive).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component) where T : UdonSharpBehaviour =>
            component.GetComponentsInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            component.GetComponentsInParent<T>();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, Type type) =>
            component.GetComponentsInParent(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentsInParent(type).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component, bool includeInactive) where T : UdonSharpBehaviour =>
            component.GetComponentsInParent<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            component.GetComponentsInParent<T>(includeInactive);

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, Type type, bool includeInactive) =>
            component.GetComponentsInParent(type, includeInactive).CastArray();

        [Obsolete("UdonSharp GetComponent Extensions are deprecated, use regular GetComponent(s) calls now.")]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            component.GetComponentsInParent(type, includeInactive).CastArray();
    #endregion

    #endregion
    }
}
