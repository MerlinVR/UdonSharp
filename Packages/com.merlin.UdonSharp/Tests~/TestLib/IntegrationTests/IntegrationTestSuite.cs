
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
        [SerializeField] private string testSuiteName;
        [SerializeField] private bool forcePrintPassedTests;
        [SerializeField] private UdonSharpBehaviour[] tests;
#pragma warning restore CS0649

        private int _testTotalCount;
        private int _testSuccessCount;
        private string _whitespaceStr = new string(' ', 4);

        public int GetTotalTestCount() => _testTotalCount;
        public int GetSucceededTestCount() => _testSuccessCount;

        public void RunTests()
        {
            if (!runSuiteTests)
                return;

            _whitespaceStr = new string(' ', 4);
            
            _testTotalCount = 0;
            _testSuccessCount = 0;

            Debug.Log($"{_whitespaceStr}[<color=#00AF54>UdonSharp Tests</color>] [{testSuiteName}] Start");

            foreach (UdonSharpBehaviour test in tests)
            {
                test.SetProgramVariable("tester", this);
                test.SendCustomEvent("ExecuteTests");
            }

            Debug.Log($"{_whitespaceStr}[<color=#00AF54>UdonSharp Tests</color>] [{testSuiteName}] [{_testSuccessCount}/{_testTotalCount}] {(_testSuccessCount == _testTotalCount ? "Tests succeeded" : "")}");
        }

        public void TestAssertion(string testName, bool assertion)
        {
            string testPrefixText = assertion ? "[<color=#008000>Pass</color>]: " : "[<color=#FF0000>Fail</color>]: ";

            if (!assertion || printPassedTests || forcePrintPassedTests)
                Debug.Log(_whitespaceStr + _whitespaceStr + "[<color=#00AF54>Test</color>] " + testPrefixText + testName);

            ++_testTotalCount;
            if (assertion) ++_testSuccessCount;
        }
    }
}
