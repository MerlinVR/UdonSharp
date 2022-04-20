
using System;
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
                throw new ArgumentException("Cannot add components of type 'UdonSharpBehaviour', you can only add subclasses of this type");

            if (!typeof(UdonSharpBehaviour).IsAssignableFrom(type))
                throw new ArgumentException("Type for AddUdonSharpComponent must be a subclass of UdonSharpBehaviour");

            UdonSharpBehaviour proxyBehaviour = (UdonSharpBehaviour)Undo.AddComponent(gameObject, type);
            UdonSharpEditorUtility.RunBehaviourSetupWithUndo(proxyBehaviour);

            if (EditorApplication.isPlaying)
                UdonSharpEditorUtility.GetBackingUdonBehaviour(proxyBehaviour).InitializeUdonContent();

            return proxyBehaviour;
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
