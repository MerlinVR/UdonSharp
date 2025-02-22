
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

        private VRCStation station;
        bool isSitting;

        private void Start()
        {
            station = (VRCStation)GetComponentInChildren(typeof(VRCStation), true);
            if (Networking.IsMaster)
                station.gameObject.SetActive(true);
        }

        public override void Interact()
        {
            //Networking.LocalPlayer.UseAttachedStation();
            station.UseStation(Networking.LocalPlayer);
            //station.PlayerMobility = VRCStation.Mobility.Mobile;
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            Debug.Log(player.displayName + player.displayName.Length + " entered station");

            followerToAssign.followedPlayerApi = player;

            if (player.isLocal)
            {
                Networking.SetOwner(player, followerToAssign.gameObject);
                Networking.SetOwner(player, station.gameObject);
                isSitting = true;
                //station.gameObject.SetActive(false);
                //station.ExitStation(Networking.LocalPlayer);
            }
            else
            {
                //station.gameObject.SetActive(false);
                //station.PlayerMobility = VRCStation.Mobility.Immobilize;
            }
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            Debug.Log(player.displayName + player.displayName.Length + " exited station");

            //if (followerToAssign.followedPlayerApi == player)
            //    followerToAssign.followedPlayerApi = null;

            if (player.isLocal)
            {
                //station.gameObject.SetActive(true);
                isSitting = false;
            }
            else
            {
                //station.gameObject.SetActive(true);
                //station.PlayerMobility = VRCStation.Mobility.Mobile;
            }
        }

        private void Update()
        {
            if (isSitting && !Networking.IsOwner(station.gameObject))
            {
                station.ExitStation(Networking.LocalPlayer);
                isSitting = false;
            }

            //if (!Networking.IsMaster)
            //{
            //    station.gameObject.SetActive(false);
            //}
        }
    }
}
