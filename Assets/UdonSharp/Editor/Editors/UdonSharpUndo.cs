
using JetBrains.Annotations;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace UdonSharpEditor
{
    public static class UdonSharpUndo
    {
        [PublicAPI]
        public static void DestroyImmediate(UdonSharpBehaviour behaviour)
        {
            UdonBehaviour udonBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);

            if (udonBehaviour)
                Undo.DestroyObjectImmediate(udonBehaviour);

            UdonSharpEditorUtility.SetIgnoreEvents(true);

            try
            {
                Undo.DestroyObjectImmediate(behaviour);
            }
            finally
            {
                UdonSharpEditorUtility.SetIgnoreEvents(false);
            }
        }

        /// <summary>
        /// Adds an UdonSharpBehaviour to the target GameObject and registers an Undo operation for the add
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [PublicAPI]
        public static UdonSharpBehaviour AddComponent(GameObject gameObject, System.Type type)
        {
            if (type == typeof(UdonSharpBehaviour))
                throw new System.ArgumentException("Cannot add components of type 'UdonSharpBehaviour', you can only add subclasses of this type");

            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(type))
                throw new System.ArgumentException("Type for AddUdonSharpComponent must be a subclass of UdonSharpBehaviour");

            UdonBehaviour udonBehaviour = Undo.AddComponent<UdonBehaviour>(gameObject);

            UdonSharpProgramAsset programAsset = UdonSharpProgramAsset.GetProgramAssetForClass(type);

            udonBehaviour.programSource = programAsset;
#pragma warning disable CS0618 // Type or member is obsolete
            udonBehaviour.AllowCollisionOwnershipTransfer = false;
#pragma warning restore CS0618 // Type or member is obsolete

            SerializedObject componentAsset = new SerializedObject(udonBehaviour);
            SerializedProperty serializedProgramAssetProperty = componentAsset.FindProperty("serializedProgramAsset");

            serializedProgramAssetProperty.objectReferenceValue = programAsset.SerializedProgramAsset;
            componentAsset.ApplyModifiedProperties();

            System.Type scriptType = programAsset.GetClass();

            UdonSharpBehaviour proxyComponent = (UdonSharpBehaviour)Undo.AddComponent(udonBehaviour.gameObject, scriptType);
            proxyComponent.hideFlags = HideFlags.DontSaveInBuild |
#if !UDONSHARP_DEBUG
                                       HideFlags.HideInInspector |
#endif
                                       HideFlags.DontSaveInEditor;
            proxyComponent.enabled = false;

            UdonSharpEditorUtility.SetBackingUdonBehaviour(proxyComponent, udonBehaviour);
            UdonSharpEditorUtility.CopyUdonToProxy(proxyComponent, ProxySerializationPolicy.AllWithCreateUndo);

            if (EditorApplication.isPlaying)
                udonBehaviour.InitializeUdonContent();

            return proxyComponent;
        }

        /// <summary>
        /// Adds an UdonSharpBehaviour to the target GameObject and registers an Undo operation for the add
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        [PublicAPI]
        public static T AddComponent<T>(GameObject gameObject) where T : UdonSharpBehaviour
        {
            return (T)AddComponent(gameObject, typeof(T));
        }
    }
}
