
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Tests issues where struct mutators don't apply their changes to the heap variable, these include Fields (fixed), Properties, and Methods
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tests/StructMutatorTest")]
    public class StructMutatorTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            TestFieldSet();
            TestRayPropertySet();
            TestColorBlockSet();
            TestMethodSet();
        }

        // https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/vector3set-xyz-not-functioning
        // This has been fixed for a while
        void TestFieldSet()
        {
            Vector3 vector = new Vector3(1, 2, 3);
            vector.y = 4f;

            tester.TestAssertion("Vector3 Field Set", vector == new Vector3(1, 4, 3));
        }

        // https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/raysetorigin-and-raysetdirection-not-working
        void TestRayPropertySet()
        {
            Ray ray = new Ray(Vector3.zero, Vector3.up);

            ray.direction = Vector3.right;

            tester.TestAssertion("Ray Property Set", ray.direction == Vector3.right);
        }

        // Another instance of property setters not working that was reported by someone.
        void TestColorBlockSet()
        {
            ColorBlock test = new ColorBlock();
            test.normalColor = Color.green;

            tester.TestAssertion("Color Block Property Set", test.normalColor == Color.green);
        }

        // Related to https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/raysetorigin-and-raysetdirection-not-working, does not have a dedicated canny. Q ran into the issue so it's hopefully internally tracked.
        void TestMethodSet()
        {
            Vector3 vector = new Vector3(1, 2, 3);
            vector.Normalize();

            tester.TestAssertion("Vector Method Set", Mathf.Approximately(vector.magnitude, 1f));
        }
    }
}
