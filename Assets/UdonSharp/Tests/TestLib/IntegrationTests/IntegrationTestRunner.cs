
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Test Lib/Integration Test Runner")]
    public class IntegrationTestRunner : UdonSharpBehaviour
    {
        public bool runTestOnStart = true;
        public bool logPassedTests = true;

        public IntegrationTestSuite[] integrationTests;

        public LayerMask mask;

        void Start()
        {
            if (runTestOnStart)
                RunTests();

            Debug.Log((int)mask);
        }

        public override void Interact()
        {
            RunTests();
        }

        void RunTests()
        {
            Debug.Log("[<color=#00AF54>UdonSharp Tests</color>] Starting tests");

            int totalTests = 0;
            int passedTests = 0;

            foreach (IntegrationTestSuite suite in integrationTests)
            {
                suite.printPassedTests = logPassedTests;
                suite.RunTests();

                totalTests += suite.GetTotalTestCount();
                passedTests += suite.GetSucceededTestCount();
            }

            Debug.Log($"[<color=#00AF54>UdonSharp Tests</color>] Tests finished [{passedTests}/{totalTests}]");
        }
    }
}
