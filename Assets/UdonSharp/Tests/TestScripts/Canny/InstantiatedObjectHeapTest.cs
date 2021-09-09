
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Tests the issue where the heap will not be immediately initialized after instantiation
    /// https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/heap-values-are-not-initialized-in-some-cases-so-getprogramvariable-returns-null
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tests/InstantiatedObjectHeapTest")]
    public class InstantiatedObjectHeapTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public GameObject referenceSpawnObject;

        public void ExecuteTests()
        {
            TestHeapVariableGetComponent();
            TestHeapVariableGet();
            TestHeapVariableSet();
            TestCustomEvent();
        }

        void TestHeapVariableGetComponent()
        {
            GameObject newObject = Instantiate(referenceSpawnObject);

            InstantiatedObjectTesterScript testScript = newObject.GetComponent<InstantiatedObjectTesterScript>();

            // GetComponent<User T>() will fail immediately after instantiation because the script ID heap variable returns null, so U# has no way to determine the script type
            tester.TestAssertion("Instantiated Object GetComponent<T>()", testScript != null);

            Destroy(newObject);
        }

        void TestHeapVariableGet()
        {
            GameObject newObject = Instantiate(referenceSpawnObject);

            // Have a separate case that will force find the script because of the above issue. We know in this specific test case that the only UdonBehaviour on the object will be of the type `InstantiatedObjectTesterScript`
            InstantiatedObjectTesterScript testScript = (InstantiatedObjectTesterScript)newObject.GetComponent(typeof(UdonBehaviour));

            tester.TestAssertion("Instantiated Object Get Field", testScript.testValueStr == "test");

            Destroy(newObject);
        }

        void TestHeapVariableSet()
        {
            GameObject newObject = Instantiate(referenceSpawnObject);
            
            InstantiatedObjectTesterScript testScript = (InstantiatedObjectTesterScript)newObject.GetComponent(typeof(UdonBehaviour));

            testScript.testValueStr = "test set";

            tester.TestAssertion("Instantiated Object Set Field", testScript.testValueStr == "test set");

            Destroy(newObject);
        }

        int spawnCounter = 0;

        public void SpawnEvent()
        {
            spawnCounter++;
        }

        void TestCustomEvent()
        {
            spawnCounter = 0;

            GameObject newObject = Instantiate(referenceSpawnObject);

            InstantiatedObjectTesterScript testScript = (InstantiatedObjectTesterScript)newObject.GetComponent(typeof(UdonBehaviour));

            testScript.spawnerBehaviour = this;
            testScript.CallEvent();

            tester.TestAssertion("Instantiated Object Send Event", spawnCounter == 1);

            Destroy(newObject);
        }
    }
}
