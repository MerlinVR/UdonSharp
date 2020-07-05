
using UnityEngine;
using VRC.SDKBase;

namespace UdonSharp.Examples.Utilities
{
    /// <summary>
    /// Allows the master and only the master to toggle a game object globally
    /// </summary>
    public class MasterToggleObject : UdonSharpBehaviour 
    {
        public GameObject toggleObject;

        [UdonSynced]
        bool isObjectEnabled;

        private void Start()
        {
            isObjectEnabled = toggleObject.activeSelf;
        }

        public override void OnDeserialization()
        {
            toggleObject.SetActive(isObjectEnabled);
        }

        public override void Interact()
        {
            if (!Networking.IsMaster)
                return;

            isObjectEnabled = !isObjectEnabled;
            toggleObject.SetActive(isObjectEnabled);
        }
    }
}
