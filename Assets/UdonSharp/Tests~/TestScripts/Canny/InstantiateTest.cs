
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/InstantiateTest")]
    public class InstantiateTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public GameObject objectToSpawn;

        public void ExecuteTests()
        {
            GameObject newObject = Instantiate(objectToSpawn);

            tester.TestAssertion("Instantiation", newObject != null);
        }
    }
}
