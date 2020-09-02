
using JetBrains.Annotations;
using System.Collections.Generic;
using UdonSharp;
using UnityEditor;
using UnityEngine;
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

        #region Utility functions
        private static UdonSharpBehaviour ConvertToUdonSharpComponent(UdonBehaviour behaviour, System.Type type, ProxySerializationPolicy proxySerializationPolicy)
        {
            if (behaviour == null)
                return null;

            UdonSharpBehaviour udonSharpBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(behaviour, ProxySerializationPolicy.NoSerialization);
            System.Type uSharpBehaviourType = udonSharpBehaviour.GetType();

            if (udonSharpBehaviour && (uSharpBehaviourType == type || uSharpBehaviourType.IsSubclassOf(type)))
            {
                UdonSharpEditorUtility.CopyUdonToProxy(udonSharpBehaviour, proxySerializationPolicy);
                return udonSharpBehaviour;
            }

            return null;
        }

        private static T ConvertToUdonSharpComponent<T>(UdonBehaviour behaviour, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour
        {
            return (T)ConvertToUdonSharpComponent(behaviour, typeof(T), proxySerializationPolicy);
        }

        private static UdonSharpBehaviour[] ConvertToUdonSharpComponents(UdonBehaviour[] behaviours, System.Type type, ProxySerializationPolicy proxySerializationPolicy)
        {
            if (behaviours.Length == 0)
                return new UdonSharpBehaviour[0];

            List<UdonSharpBehaviour> udonSharpBehaviours = new List<UdonSharpBehaviour>();

            foreach (UdonBehaviour behaviour in behaviours)
            {
                UdonSharpBehaviour udonSharpBehaviour = ConvertToUdonSharpComponent(behaviour, type, proxySerializationPolicy);

                if (udonSharpBehaviour)
                    udonSharpBehaviours.Add(udonSharpBehaviour);
            }

            return udonSharpBehaviours.ToArray();
        }

        private static T[] ConvertToUdonSharpComponents<T>(UdonBehaviour[] behaviours, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour
        {
            if (behaviours.Length == 0)
                return new T[0];

            List<T> udonSharpBehaviours = new List<T>();

            foreach (UdonBehaviour behaviour in behaviours)
            {
                UdonSharpBehaviour udonSharpBehaviour = ConvertToUdonSharpComponent<T>(behaviour, proxySerializationPolicy);

                if (udonSharpBehaviour)
                    udonSharpBehaviours.Add((T)udonSharpBehaviour);
            }

            return udonSharpBehaviours.ToArray();
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

        #region GetComponent
        [PublicAPI]
        public static T GetUdonSharpComponent<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponent<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponent<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponent<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponent(this GameObject gameObject, System.Type type) =>
            ConvertToUdonSharpComponent(gameObject.GetComponent<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponent(this GameObject gameObject, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(gameObject.GetComponent<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T GetUdonSharpComponent<T>(this Component component) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponent<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponent<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour => 
            ConvertToUdonSharpComponent<T>(component.GetComponent<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponent(this Component component, System.Type type) =>
            ConvertToUdonSharpComponent(component.GetComponent<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponent(this Component component, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(component.GetComponent<UdonBehaviour>(), type, proxySerializationPolicy);
        #endregion

        #region GetComponents
        [PublicAPI]
        public static T[] GetUdonSharpComponents<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponents<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponents<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponents<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this GameObject gameObject, System.Type type) =>
            ConvertToUdonSharpComponents(gameObject.GetComponents<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this GameObject gameObject, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(gameObject.GetComponents<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponents<T>(this Component component) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponents<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponents<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponents<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this Component component, System.Type type) =>
            ConvertToUdonSharpComponents(component.GetComponents<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponents(this Component component, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(component.GetComponents<UdonBehaviour>(), type, proxySerializationPolicy);
        #endregion

        #region GetComponentInChildren
        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponentInChildren<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponentInChildren<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, System.Type type) =>
            ConvertToUdonSharpComponent(gameObject.GetComponentInChildren<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(gameObject.GetComponentInChildren<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject, bool includeInactive) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponentInChildren<UdonBehaviour>(includeInactive), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this GameObject gameObject, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponentInChildren<UdonBehaviour>(includeInactive), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, System.Type type, bool includeInactive) =>
            ConvertToUdonSharpComponent(gameObject.GetComponentInChildren<UdonBehaviour>(includeInactive), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this GameObject gameObject, System.Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(gameObject.GetComponentInChildren<UdonBehaviour>(includeInactive), type, proxySerializationPolicy);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this Component component) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponentInChildren<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponentInChildren<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, System.Type type) =>
            ConvertToUdonSharpComponent(component.GetComponentInChildren<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(component.GetComponentInChildren<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this Component component, bool includeInactive) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponentInChildren<UdonBehaviour>(includeInactive), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponentInChildren<T>(this Component component, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponentInChildren<UdonBehaviour>(includeInactive), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, System.Type type, bool includeInactive) =>
            ConvertToUdonSharpComponent(component.GetComponentInChildren<UdonBehaviour>(includeInactive), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInChildren(this Component component, System.Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(component.GetComponentInChildren<UdonBehaviour>(includeInactive), type, proxySerializationPolicy);
        #endregion

        #region GetComponentsInChildren
        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInChildren<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInChildren<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, System.Type type) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInChildren<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInChildren<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject, bool includeInactive) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInChildren<UdonBehaviour>(includeInactive), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this GameObject gameObject, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInChildren<UdonBehaviour>(includeInactive), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, System.Type type, bool includeInactive) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInChildren<UdonBehaviour>(includeInactive), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this GameObject gameObject, System.Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInChildren<UdonBehaviour>(includeInactive), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInChildren<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInChildren<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, System.Type type) =>
            ConvertToUdonSharpComponents(component.GetComponentsInChildren<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(component.GetComponentsInChildren<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component, bool includeInactive) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInChildren<UdonBehaviour>(includeInactive), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInChildren<T>(this Component component, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInChildren<UdonBehaviour>(includeInactive), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, System.Type type, bool includeInactive) =>
            ConvertToUdonSharpComponents(component.GetComponentsInChildren<UdonBehaviour>(includeInactive), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInChildren(this Component component, System.Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(component.GetComponentsInChildren<UdonBehaviour>(includeInactive), type, proxySerializationPolicy);
        #endregion

        #region GetComponentInParent
        [PublicAPI]
        public static T GetUdonSharpComponentInParent<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponentInParent<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponentInParent<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(gameObject.GetComponentInParent<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this GameObject gameObject, System.Type type) =>
            ConvertToUdonSharpComponent(gameObject.GetComponentInParent<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this GameObject gameObject, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(gameObject.GetComponentInParent<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T GetUdonSharpComponentInParent<T>(this Component component) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponentInParent<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T GetUdonSharpComponentInParent<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponent<T>(component.GetComponentInParent<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this Component component, System.Type type) =>
            ConvertToUdonSharpComponent(component.GetComponentInParent<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour GetUdonSharpComponentInParent(this Component component, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponent(component.GetComponentInParent<UdonBehaviour>(), type, proxySerializationPolicy);
        #endregion

        #region GetComponentsInParent
        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInParent<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInParent<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, System.Type type) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInParent<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInParent<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject, bool includeInactive) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInParent<UdonBehaviour>(includeInactive), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this GameObject gameObject, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(gameObject.GetComponentsInParent<UdonBehaviour>(includeInactive), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, System.Type type, bool includeInactive) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInParent<UdonBehaviour>(includeInactive), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this GameObject gameObject, System.Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(gameObject.GetComponentsInParent<UdonBehaviour>(includeInactive), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInParent<UdonBehaviour>(), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInParent<UdonBehaviour>(), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, System.Type type) =>
            ConvertToUdonSharpComponents(component.GetComponentsInParent<UdonBehaviour>(), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, System.Type type, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(component.GetComponentsInParent<UdonBehaviour>(), type, proxySerializationPolicy);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component, bool includeInactive) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInParent<UdonBehaviour>(includeInactive), ProxySerializationPolicy.Default);

        [PublicAPI]
        public static T[] GetUdonSharpComponentsInParent<T>(this Component component, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) where T : UdonSharpBehaviour =>
            ConvertToUdonSharpComponents<T>(component.GetComponentsInParent<UdonBehaviour>(includeInactive), proxySerializationPolicy);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, System.Type type, bool includeInactive) =>
            ConvertToUdonSharpComponents(component.GetComponentsInParent<UdonBehaviour>(includeInactive), type, ProxySerializationPolicy.Default);

        [PublicAPI]
        public static UdonSharpBehaviour[] GetUdonSharpComponentsInParent(this Component component, System.Type type, bool includeInactive, ProxySerializationPolicy proxySerializationPolicy) =>
            ConvertToUdonSharpComponents(component.GetComponentsInParent<UdonBehaviour>(includeInactive), type, proxySerializationPolicy);
        #endregion
    }
}
