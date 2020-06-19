
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

        public GameObject modObject;

        public void ExecuteTests()
        {
            tester.TestAssertion("GetComponent<Transform>()", GetComponent<Transform>() != null);

            BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>();

            tester.TestAssertion("GetComponentsInChildren<BoxCollider>()", colliders.Length == 2);

            tester.TestAssertion("GetComponentInChildren<PlayerModSetter>()", GetComponentInChildren<PlayerModSetter>() != null);

            PlayerModSetter[] modSetters = GetComponentsInChildren<PlayerModSetter>();

            tester.TestAssertion("GetComponentsInChildren<PlayerModSetter>()", modSetters.Length == 3);
            
            tester.TestAssertion("GetComponentsInChildren<MeshRenderer>()", GetComponentsInChildren<MeshRenderer>().Length == 2);

            UdonSharpBehaviour getBehaviour = (UdonSharpBehaviour)modObject.GetComponent(typeof(UdonBehaviour));

            tester.TestAssertion("Get UdonBehaviour typeof(UdonBehaviour)", getBehaviour != null);

            long typeID = GetUdonTypeID<PlayerModSetter>();

            tester.TestAssertion("Type ID matches", typeID == getBehaviour.GetUdonTypeID());

            tester.TestAssertion("Correct number of UdonBehaviours on gameobject", modObject.GetComponents(typeof(UdonBehaviour)).Length == 4);

            //Debug.Log(getBehaviour.GetUdonTypeID());
            //Debug.Log(getBehaviour.GetUdonTypeName());

            //foreach (Component behaviour in modObject.GetComponents(typeof(UdonBehaviour)))
            //{
            //    Debug.Log("Component name: " + ((UdonSharpBehaviour)behaviour).GetUdonTypeName());
            //}
        }
    }
}
