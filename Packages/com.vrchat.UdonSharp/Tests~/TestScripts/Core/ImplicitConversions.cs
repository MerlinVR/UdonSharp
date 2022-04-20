
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/ImplicitConversions")]
    public class ImplicitConversions : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        [System.NonSerialized]
        public string testStr;

        public void ExecuteTests()
        {
            ImplicitConversions selfRef = this;
            testStr = null;

            // https://github.com/Merlin-san/UdonSharp/issues/32
            tester.TestAssertion("Null public string is null", selfRef.testStr == null);

            testStr = "hello!";
            tester.TestAssertion("Non-null public string is valid", selfRef.testStr == "hello!");
        }
    }
}
