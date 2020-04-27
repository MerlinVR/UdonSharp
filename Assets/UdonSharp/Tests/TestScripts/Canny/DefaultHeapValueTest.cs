
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/DefaultHeapValueTest")]
    public class DefaultHeapValueTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            TestPublicArray();
            TestPublicString();
            TestSyncedString();
            TestPrivateArr();
            TestPrivateStr();
        }

        [HideInInspector]
        public string[] defaultPublicArr;

        void TestPublicArray()
        {
            tester.TestAssertion("Default Public Array Initialized", defaultPublicArr != null && defaultPublicArr.Length == 0);
        }

        [HideInInspector]
        public string defaultString;

        void TestPublicString()
        {
            tester.TestAssertion("Default String Value", defaultString == "");
        }

        [UdonSynced]
        string networkSyncedString;

        void TestSyncedString()
        {
            tester.TestAssertion("Default Synced String Value", networkSyncedString == "");
        }

        string[] privateStrArr;

        void TestPrivateArr()
        {
            tester.TestAssertion("Private Array Default Null", privateStrArr == null);
        }

        string privateStr;

        void TestPrivateStr()
        {
            tester.TestAssertion("Private String Default Null", privateStr == null);
        }
    }
}
