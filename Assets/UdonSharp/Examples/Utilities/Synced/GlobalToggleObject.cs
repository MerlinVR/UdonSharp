
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
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class GlobalToggleObject : UdonSharpBehaviour 
    {
        public GameObject toggleObject;

        [UdonSynced]
        bool isEnabled;

        private void Start()
        {
            isEnabled = toggleObject.activeSelf;
        }

        public override void OnDeserialization()
        {
            if (!Networking.IsOwner(gameObject))
                toggleObject.SetActive(isEnabled);
        }

        public override void Interact()
        {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            isEnabled = !isEnabled;
            toggleObject.SetActive(isEnabled);

            RequestSerialization();
        }
    }
}
