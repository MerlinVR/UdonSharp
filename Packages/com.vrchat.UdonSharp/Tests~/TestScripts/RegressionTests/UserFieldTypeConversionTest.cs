
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/UserFieldTypeConversionTest")]
    public class UserFieldTypeConversionTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        [System.NonSerialized]
        public sbyte baz = 3;

        public void ExecuteTests()
        {
            UserFieldTypeConversionTest self = this;

            int resultVal = Test(self.baz);
            tester.TestAssertion("User Field Type Conversion", resultVal == 5);
        }

        int Test(int val)
        {
            val += 2;
            return val;
        }
    }
}
