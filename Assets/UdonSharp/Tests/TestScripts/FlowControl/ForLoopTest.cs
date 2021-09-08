
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

            counter = 0;
            foreach (var child in transform)
            {
                ++counter;
            }

            foreach (Transform child in transform)
            {
                ++counter;
            }

            tester.TestAssertion("Foreach child Transform loop", counter == 6);

            string helloStr = "hello!";
            string builtStr = "";

            foreach (char strChar in helloStr)
            {
                builtStr += strChar;
            }

            tester.TestAssertion("Foreach string loop", builtStr == helloStr);

            GameObject[] gameObjects = new GameObject[2000];
            int gameObjectLen = gameObjects.Length;

            System.DateTime startTime = System.DateTime.UtcNow;
            for (int i = 0; i < gameObjectLen; ++i)
            {
                gameObjects[i] = gameObject;
            }

            tester.TestAssertion("Array indexer performance", (System.DateTime.UtcNow - startTime).TotalSeconds < 1f);
        }
    }
}
