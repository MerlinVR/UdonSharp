
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/InheritanceRootTest")]
    public class InheritanceRootTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public TestInheritanceClassBase[] testClasses;

        public void ExecuteTests()
        {
            string resultStr = "";
            
            foreach (var testInheritanceClassBase in testClasses)
            {
                resultStr += testInheritanceClassBase.GetClassName();
            }
            
            tester.TestAssertion("Inherited methods", resultStr == "ABBC");
        }
    }
}
