
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Tests getting and setting variables on disabled UdonBehaviours that have never been enabled
    /// https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/heap-values-are-not-initialized-in-some-cases-so-getprogramvariable-returns-null
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tests/DisabledObjectHeapTest")]
    public class DisabledObjectHeapTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public InstantiatedObjectTesterScript referenceScript;

        public void ExecuteTests()
        {
            if (referenceScript == null)
                Debug.LogError("[DisabledObjectHeapTest] Reference script cannot be null");

            TestHeapVariableGet();
            TestHeapVariableSet();
            TestCustomEvent();
        }

        void TestHeapVariableGet()
        {
            tester.TestAssertion("Disabled Object Heap Get Value", referenceScript.testValueStr == "test");
        }

        void TestHeapVariableSet()
        {
            referenceScript.testValueStr = "test set value";
            tester.TestAssertion("Disabled Object Heap Set Value", referenceScript.testValueStr == "test set value");
        }

        int callCounter = 0;

        public void SpawnEvent()
        {
            callCounter++;
        }

        void TestCustomEvent()
        {
            callCounter = 0;

            referenceScript.spawnerBehaviour = this;
            referenceScript.CallEvent();

            tester.TestAssertion("Disabled Object Send Custom Event", callCounter == 1);
        }
    }
}
