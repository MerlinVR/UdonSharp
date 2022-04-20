
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Utilities
{
    /// <summary>
    /// Follows a chosen bone on humanoid avatars using the playerapi
    /// </summary>
    [AddComponentMenu("Udon Sharp/Utilities/Bone Follower")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class BoneFollower : UdonSharpBehaviour 
    {
        public HumanBodyBones trackedBone;

        VRCPlayerApi playerApi;
        bool isInEditor;

        void Start()
        {
            playerApi = Networking.LocalPlayer;
            isInEditor = playerApi == null;
        }

        void Update()
        {
            if (isInEditor)
                return;

            transform.SetPositionAndRotation(playerApi.GetBonePosition(trackedBone), playerApi.GetBoneRotation(trackedBone));
        }
    }
}
