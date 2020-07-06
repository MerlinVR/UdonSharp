
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Utilities
{
    /// <summary>
    /// This class allows anyone to toggle a gameobject for everyone in the world. 
    /// This script assumes that the object it is on will not have other things transferring ownership of it.
    /// </summary>
    public class GlobalToggleObject : UdonSharpBehaviour 
    {
        public GameObject toggleObject;

        [UdonSynced]
        bool isEnabledGlobal;
        bool isEnabledLocal;

        private void Start()
        {
            isEnabledGlobal = isEnabledLocal = toggleObject.activeSelf;
        }

        public override void OnDeserialization()
        {
            if (!Networking.IsOwner(gameObject))
            {
                toggleObject.SetActive(isEnabledGlobal);
                isEnabledLocal = isEnabledGlobal;
            }
        }

        public override void OnPreSerialization()
        {
            isEnabledGlobal = isEnabledLocal;
        }

        public override void Interact()
        {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            isEnabledLocal = isEnabledGlobal = !isEnabledGlobal;
            toggleObject.SetActive(isEnabledLocal);
        }
    }
}
