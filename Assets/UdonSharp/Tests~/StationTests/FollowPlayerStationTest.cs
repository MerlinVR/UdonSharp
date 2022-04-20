
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/FollowPlayerStationTest")]
    public class FollowPlayerStationTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public VRCPlayerApi followedPlayerApi;

        private void LateUpdate()
        {
            if (followedPlayerApi != null)
            {
                transform.position = followedPlayerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position + Vector3.up * 2f;
            }
        }
    }
}
