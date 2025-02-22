
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/JaggedArrayCOWTest")]
    public class JaggedArrayCOWTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public bool[] JaggedArrayCallTest(bool[][][] arr)
        {
            //bool[][][] arr3 = arr;
            bool[] arr2 = arr[1][0];
            return arr2;
        }

        void TestJaggedArray()
        {
            bool[][][] array = new bool[2][][];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new bool[2][];
                for (int i2 = 0; i2 < array.Length; i2++)
                {
                    array[i][i2] = new bool[] { true, false };
                }
            }

            bool[] arr2 = JaggedArrayCallTest(array);

            tester.TestAssertion("Jagged Array COW Leak Test", arr2[0] == true && arr2[1] == false);
        }

        public void ExecuteTests()
        {
            TestJaggedArray();
        }
    }
}
