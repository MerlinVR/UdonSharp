
using UnityEngine;
using VRC.SDKBase;

namespace UdonSharp.Examples.Utilities
{
    /// <summary>
    /// Allows the master and only the master to toggle a game object globally
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class MasterToggleObject : UdonSharpBehaviour 
    {
        public GameObject toggleObject;

        [UdonSynced]
        bool isObjectEnabled;

        private void Start()
        {
            isObjectEnabled = toggleObject.activeSelf;
        }

        // Prevents people who are not the master from taking ownership
        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            return requestedOwner.isMaster;
        }

        public override void OnDeserialization()
        {
            toggleObject.SetActive(isObjectEnabled);
        }

        public override void Interact()
        {
            if (!Networking.IsMaster)
                return;
            else if (!Networking.IsOwner(gameObject)) // The object may have transfer ownership on collision checked which would allow people to take ownership by accident
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            isObjectEnabled = !isObjectEnabled;
            toggleObject.SetActive(isObjectEnabled);

            RequestSerialization();
        }
    }
}
