
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/StationEventTest")]
    public class StationEventTest : UdonSharpBehaviour
    {
        public FollowPlayerStationTest followerToAssign;

        public override void Interact()
        {
            Networking.LocalPlayer.UseAttachedStation();
        }

        public override void OnStationEntered()
        {
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            Debug.Log(player.displayName + player.displayName.Length + " entered station");

            followerToAssign.followedPlayerApi = player;

            if (player.isLocal)
                Networking.SetOwner(player, followerToAssign.gameObject);
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            Debug.Log(player.displayName + player.displayName.Length + " exited station");

            //if (followerToAssign.followedPlayerApi == player)
            //    followerToAssign.followedPlayerApi = null;
        }
    }
}
