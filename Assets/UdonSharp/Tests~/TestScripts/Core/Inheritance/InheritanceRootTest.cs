
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
            tester.TestAssertion("Inherited methods 2", testClasses[0].GetClassID() == 1);
            tester.TestAssertion("Inherited type field 1", testClasses[0].BaseClassField == 4);
            tester.TestAssertion("Inherited type field 2", testClasses[1].BaseClassField == 20);
            tester.TestAssertion("Inherited type field 3", testClasses[2].NonSerializedField == 5);
        }
    }
}
