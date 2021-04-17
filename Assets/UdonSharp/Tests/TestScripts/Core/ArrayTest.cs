
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

        public VRCStation[] stations;
        public VRC.SDK3.Components.VRCStation[] stationsSDK3;

        public VRC.SDK3.Video.Components.VRCUnityVideoPlayer[] unityVideoPlayerArray;
        public VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer[] baseVideoPlayerArray;

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

            float result = vecArray[0].x += 3f;

            tester.TestAssertion("Struct array increment assignment in place result", result == 12f);

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

            float[][] floatJaggedArray = new float[][] { new[] { 1f, 2f }, new[] { 3f, 4, 5 } };

            tester.TestAssertion("Jagged array access", floatJaggedArray[0][0] == 1f && floatJaggedArray[0][1] == 2f && floatJaggedArray[1][0] == 3f && floatJaggedArray[1][1] == 4f && floatJaggedArray[1][2] == 5f);

            tester.TestAssertion("Jagged array in-place increment", (floatJaggedArray[0][1] += 2f) == 4f);
            tester.TestAssertion("Jagged array in-place increment after assignment", floatJaggedArray[0][1] == 4f);

            ArrayTest[][] userTypeJaggedArray = new ArrayTest[][] { new ArrayTest[] { this, this }, new [] { this } };
            userTypeJaggedArray[1][0] = null;

            tester.TestAssertion("User type jagged array", userTypeJaggedArray[0][0] == this && userTypeJaggedArray[0][1] == this && userTypeJaggedArray[1][0] == null);

            tester.TestAssertion("Base VRCStation array", stations[0] != null && stations[1] != null);
            tester.TestAssertion("VRCSDK3 VRCStation array", stationsSDK3[0] != null && stationsSDK3[1] != null && stationsSDK3[2] != null);

            tester.TestAssertion("Unity VideoPlayer array", unityVideoPlayerArray[0] != null && unityVideoPlayerArray[1] != null);
            tester.TestAssertion("Base VRCStation array", baseVideoPlayerArray[0] != null && baseVideoPlayerArray[1] != null);

            // Test implicit array rank conversions
            float[] myFloatArr = new float[20U];
            tester.TestAssertion("Array size implicit conversion 1", myFloatArr.Length == 20);

            uint arrSize = 4;
            myFloatArr = new float[arrSize];
            tester.TestAssertion("Array size implicit conversion 2", myFloatArr.Length == 4);

            byte arrSize2 = 30;
            myFloatArr = new float[arrSize2];

            tester.TestAssertion("Array size implicit conversion 3", myFloatArr.Length == 30);
        }
    }
}
