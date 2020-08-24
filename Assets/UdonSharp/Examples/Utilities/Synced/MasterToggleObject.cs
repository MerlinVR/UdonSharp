
using UnityEngine;
using VRC.SDKBase;

namespace UdonSharp.Examples.Utilities
{
    /// <summary>
    /// Allows the master and only the master to toggle a game object globally
    /// </summary>
#if UDON_BETA_SDK
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
#endif
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
            else if (!Networking.IsOwner(gameObject)) // The object may have transfer ownership on collision checked which would allow people to take ownership by accident
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            isObjectEnabled = !isObjectEnabled;
            toggleObject.SetActive(isObjectEnabled);
        }
    }
}
