
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/ArrayTest")]
    public class ArrayTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        [HideInInspector]
        public int[] intDefaultValueArr = { 1, 2, 3 };

        readonly int[] intDefaultReadonlyArray = { x, y };

        const int x = 4;
        const int y = 2;

        [System.NonSerialized]
        public int testVal = 0;

        public void SetTestVal(int newTestVal)
        {
            testVal = newTestVal;
        }

        public void ExecuteTests()
        {
            tester.TestAssertion("Default value array", intDefaultValueArr[0] == 1 && intDefaultValueArr[1] == 2 && intDefaultValueArr[2] == 3);
            tester.TestAssertion("Const initialized array", intDefaultReadonlyArray[0] == 4 && intDefaultReadonlyArray[1] == 2);

            Vector3[] vecArray = new Vector3[] { new Vector2(4, 5f) };
            vecArray[0].x += 4;
            tester.TestAssertion("Struct array increment assignment", vecArray[0].x == 8f);
            
            tester.TestAssertion("Struct array increment assignment in place", (vecArray[0].x += 1f) == 9f);
            tester.TestAssertion("Struct array increment assignment in place after assignment", vecArray[0].x == 9f);

            int[] intArray = new int[2];

            intArray[1] += 4;
            tester.TestAssertion("Int array increment assignment", intArray[1] == 4);
            tester.TestAssertion("Int array increment assignment in place", (intArray[1] += 4) == 8);

            ArrayTest[] selfArrayTest = new[] { this };

            testVal = 0;
            selfArrayTest[0].testVal = 5;
            tester.TestAssertion("User Field array set", testVal == 5);

            testVal = 0;
            selfArrayTest[0].SetTestVal(4);
            tester.TestAssertion("User Method array call", testVal == 4);

            long typeID = GetUdonTypeID<ArrayTest>();
            tester.TestAssertion("Udon Type ID array access", typeID == selfArrayTest[0].GetUdonTypeID());

        }
    }
}
