
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/NameOf")]
    public class NameOf : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            tester.TestAssertion("Single name nameof", nameof(ExecuteTests) == "ExecuteTests");
            tester.TestAssertion("Single type nameof", nameof(UdonSharpBehaviour) == "UdonSharpBehaviour");

            string functionName = nameof(NameOf.ExecuteTests);

            tester.TestAssertion("Function nameof", functionName == "ExecuteTests");

            tester.TestAssertion("Type nameof", nameof(System.String) == "String");

            int[] nums = new[] { 1, 2 };
            tester.TestAssertion("Property nameof", nameof(nums.Length) == "Length");

            tester.TestAssertion(">2 accesses nameof", nameof(UnityEngine.UI.Text) == "Text");

            int @int = 0;
            tester.TestAssertion("Verbatim identifier", nameof(@int) == "int");
        }
    }
}
