
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/SerializationTest")]
    public class SerializationTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;
        
        public VRC.SDK3.Video.Components.VRCUnityVideoPlayer videoPlayer;
        public VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer baseVideoPlayer;

        public VRC.SDK3.Video.Components.VRCUnityVideoPlayer nullVideoPlayer;

        public void ExecuteTests()
        {
            tester.TestAssertion("Video player valid", videoPlayer != null);
            tester.TestAssertion("Base Video player valid", baseVideoPlayer != null);
            tester.TestAssertion("Video player null", nullVideoPlayer == null);
        }
    }
}
