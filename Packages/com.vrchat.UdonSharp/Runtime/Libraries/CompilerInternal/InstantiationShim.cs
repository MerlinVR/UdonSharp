
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
            GameObject instantiatedObject = Instantiate(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetPositionAndRotation(position, rotation);

            return instantiatedObject;
        }
        
        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Transform parent)
        {
            GameObject instantiatedObject = Instantiate(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetParent(parent, false);

            return instantiatedObject;
        }

        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Transform parent, bool worldPositionStays)
        {
            GameObject instantiatedObject = Instantiate(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetParent(parent, worldPositionStays);

            return instantiatedObject;
        }
        
        [UsedImplicitly]
        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject instantiatedObject = Instantiate(original);
            Transform objectTransform = instantiatedObject.transform;
            objectTransform.SetPositionAndRotation(position, rotation);
            objectTransform.SetParent(parent, true);

            return instantiatedObject;
        }
    }
}
