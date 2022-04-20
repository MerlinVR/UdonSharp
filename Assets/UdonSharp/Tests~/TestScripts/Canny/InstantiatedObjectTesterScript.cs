
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/InstantiatedObjectTesterScript")]
    public class InstantiatedObjectTesterScript : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public string testValueStr = "test";

        [System.NonSerialized]
        public UdonSharpBehaviour spawnerBehaviour;

        public void CallEvent()
        {
            spawnerBehaviour.SendCustomEvent("SpawnEvent");
        }
    }
}
