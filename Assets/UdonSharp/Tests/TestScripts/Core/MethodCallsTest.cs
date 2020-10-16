
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

        string[] splitStrs = new string[] { "aaaa", "a b c d", "e f g h i" };

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

            tester.TestAssertion("Split test", "a b c d".Split(new [] { ' ' }, System.StringSplitOptions.None).Length == 4);

            enabled = false;
            tester.TestAssertion("UdonBehaviour enabled", enabled == false);
            enabled = true;

            UdonSharpBehaviour self = this;

            self.enabled = false;
            tester.TestAssertion("UdonSharpBehaviour ref enabled", self.enabled == false);
            self.enabled = true;

            UdonBehaviour selfUdon = (UdonBehaviour)(Component)this;

            selfUdon.enabled = false;
            tester.TestAssertion("UdonSharpBehaviour ref enabled", selfUdon.enabled == false);
            selfUdon.enabled = true;
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
