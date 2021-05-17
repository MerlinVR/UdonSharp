
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Test Lib/Integration Test Suite")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class IntegrationTestSuite : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public bool printPassedTests = false;

        public bool runSuiteTests = true;

#pragma warning disable CS0649
        [SerializeField] string testSuiteName;
        [SerializeField] bool forcePrintPassedTests = false;
        [SerializeField] UdonSharpBehaviour[] tests;
#pragma warning restore CS0649


        int testTotalCount;
        int testSuccessCount;
        readonly string whitespaceStr = new string(' ', 4);

        public int GetTotalTestCount() => testTotalCount;
        public int GetSucceededTestCount() => testSuccessCount;

        public void RunTests()
        {
            if (!runSuiteTests)
                return;

            testTotalCount = 0;
            testSuccessCount = 0;

            Debug.Log($"{whitespaceStr}[<color=#00AF54>UdonSharp Tests</color>] [{testSuiteName}] Start");

            foreach (UdonSharpBehaviour test in tests)
            {
                test.SetProgramVariable("tester", this);
                test.SendCustomEvent("ExecuteTests");
            }

            Debug.Log($"{whitespaceStr}[<color=#00AF54>UdonSharp Tests</color>] [{testSuiteName}] [{testSuccessCount}/{testTotalCount}] {(testSuccessCount == testTotalCount ? "Tests succeeded" : "")}");
        }

        public void TestAssertion(string testName, bool assertion)
        {
            string testPrefixText = assertion ? "[<color=#008000>Pass</color>]: " : "[<color=#FF0000>Fail</color>]: ";

            if (!assertion || printPassedTests || forcePrintPassedTests)
                Debug.Log(whitespaceStr + whitespaceStr + "[<color=#00AF54>Test</color>] " + testPrefixText + testName);

            ++testTotalCount;
            if (assertion) ++testSuccessCount;
        }
    }
}
