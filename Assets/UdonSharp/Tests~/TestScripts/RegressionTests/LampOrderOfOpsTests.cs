
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/LampOrderOfOpsTests")]
    public class LampOrderOfOpsTests : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

#pragma warning disable 0649
        private UdonBehaviour behaviour;
#pragma warning restore 0649

        [System.NonSerialized]
        public float floatField = 4.4f;

        public void ExecuteTests()
        {
            if (behaviour != null)
            {
                // This just needs to compile
                byte[] data = (byte[])((UdonBehaviour)behaviour.GetProgramVariable("ABC")).GetProgramVariable("DEFG");
            }
            
            int castFloat = (int)5.4f;
            tester.TestAssertion("Int Cast", castFloat == 5);


            int testCastFloat = (int)(int)(float)(int)floatField;

            tester.TestAssertion("Int Cast 2", testCastFloat == 4);
        }
    }
}
