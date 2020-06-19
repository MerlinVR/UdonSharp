
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/ObjectDestroyNullCheck")]
    public class ObjectDestroyNullCheck : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public GameObject refObject;

        public void ExecuteTests()
        {
            DestroyImmediate(refObject);

            tester.TestAssertion("Object Destroy Null Check", refObject == null);
        }
    }
}
