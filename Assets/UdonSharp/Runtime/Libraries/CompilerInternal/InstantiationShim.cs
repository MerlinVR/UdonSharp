
using JetBrains.Annotations;
using UnityEngine;

namespace UdonSharp.Lib.Internal
{
    public static class InstantiationShim
    {
        // Gets aliased to VRCInstantiate by the compiler
        private static GameObject Instantiate_Extern(GameObject original) => null;

        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original)
        {
            return Instantiate_Extern(original);
        }

        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation)
        {
            GameObject instantiatedObject = Instantiate_Extern(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetPositionAndRotation(position, rotation);

            return instantiatedObject;
        }

        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Transform parent)
        {
            GameObject instantiatedObject = Instantiate_Extern(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetParent(parent, false);

            return instantiatedObject;
        }

        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Transform parent, bool worldPositionStays)
        {
            GameObject instantiatedObject = Instantiate_Extern(original);
            Transform objectTransform = instantiatedObject.transform;

            if (worldPositionStays)
            {
                Transform originalTransform = original.transform;
                Vector3 originalPosition = originalTransform.position;
                Quaternion originalRotation = originalTransform.rotation;
                objectTransform.SetPositionAndRotation(originalPosition, originalRotation);
                objectTransform.localScale = originalTransform.lossyScale;// May not behave the same as C#
            }

            objectTransform.SetParent(parent, worldPositionStays);

            return instantiatedObject;
        }

        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject instantiatedObject = Instantiate_Extern(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetParent(parent, false);
            objectTransform.SetPositionAndRotation(position, rotation);

            return instantiatedObject;
        }
    }
}
