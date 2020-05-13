
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/ForLoopTest")]
    public class ForLoopTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            int counter = 0;

            for (int i = 0; i < 10; ++i)
                counter++;

            tester.TestAssertion("Basic for loop", counter == 10);

            int[] ints = new[] { 1, 2, 3, 4, 5 };

            counter = 0;

            foreach (var val in ints)
            {
                counter += val;
            }

            tester.TestAssertion("Foreach loop", counter == 15);

            string helloStr = "hello!";
            string builtStr = "";

            foreach (char strChar in helloStr)
            {
                builtStr += strChar;
            }

            tester.TestAssertion("Foreach string loop", builtStr == helloStr);
        }
    }
}
