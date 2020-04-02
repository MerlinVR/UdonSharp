
using UnityEngine;
using UdonSharp;
using VRC.SDKBase;

namespace UdonSharp.Examples.Utilities
{
    [AddComponentMenu("Udon Sharp/Utilities/Player Mod Setter")]
    public class PlayerModSetter : UdonSharpBehaviour
    {
        public bool useLegacyLocomotion = false;

        public float jumpHeight = 3f;
        public float runSpeed = 4f;
        public float walkSpeed = 2f;

        void Start()
        {
            var playerApi = Networking.LocalPlayer;

            // Prevent error in editor from null player API
            if (playerApi != null)
            {
                playerApi.SetJumpImpulse(jumpHeight);
                playerApi.SetRunSpeed(runSpeed);
                playerApi.SetWalkSpeed(walkSpeed);

                if (useLegacyLocomotion)
                    playerApi.UseLegacyLocomotion();
            }

            Destroy(gameObject);
        }
    }
}
