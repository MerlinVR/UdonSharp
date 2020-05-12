
using UdonSharp;
using UdonSharp.Examples.Utilities;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/GetComponentTest")]
    public class GetComponentTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            tester.TestAssertion("GetComponent<Transform>()", GetComponent<Transform>() != null);

            BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>();

            tester.TestAssertion("GetComponentsInChildren<BoxCollider>()", colliders.Length == 2);

            tester.TestAssertion("GetComponentInChildren<PlayerModSetter>()", GetComponentInChildren<PlayerModSetter>() != null);

            PlayerModSetter[] modSetters = GetComponentsInChildren<PlayerModSetter>();

            tester.TestAssertion("GetComponentsInChildren<PlayerModSetter>()", modSetters.Length == 3);
            
            tester.TestAssertion("GetComponentsInChildren<MeshRenderer>()", GetComponentsInChildren<MeshRenderer>().Length == 2); 
        }
    }
}
