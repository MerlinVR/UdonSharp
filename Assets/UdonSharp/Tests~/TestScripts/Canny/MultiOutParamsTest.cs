
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Tests for bug in the VM where methods with multiple `out` or `ref` parameters do not update the out parameters correctly
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tests/MultiOutParamsTest")]
    public class MultiOutParamsTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            TestAngleAxis();
        }

        // https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/quaterniontoangleaxis-broken
        void TestAngleAxis()
        {
            Quaternion rotation = Quaternion.AngleAxis(20f, new Vector3(1, 2, 3).normalized);

            float angle = 0f;
            Vector3 axis = Vector3.zero;
            rotation.ToAngleAxis(out angle, out axis);

            tester.TestAssertion("Angle Axis double out params", angle != 0f || axis != Vector3.zero);
        }
    }
}
