
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

        Transform[] transforms;

        //Transform transformIn;

        //private void Start()
        //{
        //    SetProgramVariable("transformIn", transform);

        //    Debug.Log(transformIn.GetComponent<Transform>().position);
        //}

        public void ExecuteTests()
        {
            transforms = new[] { transform, transform };

            // Udon StrongBox assignment test
            transforms[1].GetComponentInChildren<BoxCollider>().enabled = false;
            transforms[1].GetComponentInChildren<BoxCollider>().enabled = true;
            
            tester.TestAssertion("GetComponent<Transform>()", GetComponent<Transform>() != null);

            tester.TestAssertion("GetComponent(typeof(Transform))", GetComponent(typeof(Transform)) != null);

            BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>();

            tester.TestAssertion("GetComponentsInChildren<BoxCollider>()", colliders.Length == 2);

            tester.TestAssertion("GetComponentInChildren<NameOf>()", GetComponentInChildren<NameOf>() != null);

            NameOf[] nameOfs = GetComponentsInChildren<NameOf>();

            tester.TestAssertion("GetComponentsInChildren<NameOf>()", nameOfs.Length == 3);
            
            tester.TestAssertion("GetComponentsInChildren<MeshRenderer>()", GetComponentsInChildren<MeshRenderer>().Length == 2);

#if COMPILER_UDONSHARP
            UdonSharpBehaviour getBehaviour = (UdonSharpBehaviour)modObject.GetComponent(typeof(UdonBehaviour));

            tester.TestAssertion("Get UdonBehaviour typeof(UdonBehaviour)", getBehaviour != null);

            long typeID = GetUdonTypeID<NameOf>();

            tester.TestAssertion("Type ID matches", typeID == getBehaviour.GetUdonTypeID());
#endif

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
