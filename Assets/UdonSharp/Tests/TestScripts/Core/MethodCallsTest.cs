
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/MethodCallsTest")]
    public class MethodCallsTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            string testStr = string.Concat("a", "bc", "d", "e", "fg", "hij", "klmn", "opq", "rstuv", "wx", "yz");

            tester.TestAssertion("Params arrays", testStr == "abcdefghijklmnopqrstuvwxyz");
            
            string joinedStr = string.Join(", ", new[] { "Hello", "test", "join" });
            string joinedStr2 = string.Join(", ", "Hello", "test", "join" );

            tester.TestAssertion("Param parameter without expanding", joinedStr == "Hello, test, join");
            tester.TestAssertion("Param parameter with expanding", joinedStr2 == "Hello, test, join");

            string formatStr = string.Format("{0}, {1}", this, this);
            tester.TestAssertion("FormatStr 1", formatStr == "MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour)");

            string formatStr2 = string.Format("{0}", this);
            tester.TestAssertion("FormatStr 2", formatStr2 == "MethodCalls (VRC.Udon.UdonBehaviour)");

            var objArr = new object[] { this };
            string formatStr3 = string.Format("{0}", objArr);
            tester.TestAssertion("FormatStr 3", formatStr3 == "MethodCalls (VRC.Udon.UdonBehaviour)");

            tester.TestAssertion("String Join Objects params", string.Join(", ", this, this, this, this) == "MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour)"); 
            tester.TestAssertion("String Join Objects array", string.Join(", ", new object[] { this, this, this, this }) == "MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour), MethodCalls (VRC.Udon.UdonBehaviour)"); 
        }

        //public void test(int a, bool b, float c = 5f, params float[] d)
        //{
        //    test(a, b);
        //}

        //public void test2(params object[] strings)
        //{
        //    test2(new string[] { "aa", "bbb" });
        //}
    }
}
