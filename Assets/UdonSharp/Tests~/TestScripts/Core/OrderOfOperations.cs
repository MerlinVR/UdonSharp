
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/OrderOfOperations")]
    public class OrderOfOperations : UdonSharpBehaviour
    {
        [HideInInspector]
        public IntegrationTestSuite tester;

        OrderOfOperations self;

        int[] intArray;
#pragma warning disable 0414
        Vector3[] structArray;
#pragma warning restore 0414

        public int overwriteArrayFields(int index)
        {
            intArray = null;
            structArray = null;
            return index;
        }

        private uint ROTRIGHT(uint a, int b)
        {
            return (a >> b) | (a << 32 - b);
        }

        public void ExecuteTests()
        {
            int a = 1;
            int b;

            self = this;

            b = a + (a += 1) + a;
            tester.TestAssertion("Order of operations: Binary operations with postfix increment", b == 5);

            b = a + (++a * 10) + (a + (a++ * 10)) * 100;
            tester.TestAssertion("Order of operations: Prefix and postfix", b == 3332);

            b = 3;
            int[] localArray = new int[] { b, b += 1, b };
            tester.TestAssertion("Array initializers with side effects", (localArray[0] == 3 && localArray[1] == 4 && localArray[2] == 4));

            a = 1;
            localArray[a] = 1;
            tester.TestAssertion("Array index writes", (localArray[0] == 3 && localArray[1] == 1 && localArray[2] == 4));
            localArray[a] = (a += 1);
            tester.TestAssertion("Array index writes with side effects", (localArray[0] == 3 && localArray[1] == 2 && localArray[2] == 4));

            a = 1;
            tester.TestAssertion("String interpolation with side effects", $"{a} {a += 1}" == "1 2");

            string result = string.Format("{0} {1} {2} {3}", a, a++, a += 2, ++a);
            tester.TestAssertion("Variadic extern function invocation with side effects", result == "2 2 5 6");

            Vector3[] localVecArray = new Vector3[5];
            a = 0;
            localVecArray[a].x = (a += 1);

            tester.TestAssertion("Structure array write with side effects", localVecArray[0].x == 1);

            localVecArray[a].y += (a += 1);
            
            tester.TestAssertion("Structure array compound assignment with side effects", localVecArray[1].y == 2f);

            localVecArray[a++].y += 3f;
            
            tester.TestAssertion("Structure array compound assignment with index postfix increment", localVecArray[2].y == 3f);
            
            localVecArray[++a].y += 6f;
            tester.TestAssertion("Structure array compound assignment with index prefix increment", localVecArray[4].y == 6f);

            int testVal = 4;

            testVal += (testVal += 5);
            
            tester.TestAssertion("Nested compound assignment with side effects", testVal == 13);
            
            intArray = new int[4];

            // Should not exception
            intArray[1] = ((intArray = null) == null ? 1 : 0);
            tester.TestAssertion("Clearing array field while write is pending", true);
            localVecArray[0].x = ((localVecArray = null) == null ? 1 : 0);
            tester.TestAssertion("Clearing structure array field while write is pending", true);

            intArray = new int[3];
            // // should not exception
            intArray[0] = overwriteArrayFields(0);
            tester.TestAssertion("Clearing array field by local function while write is pending", true);
            
            self = this;
            intArray = new int[3];
            
            // should not exception
            intArray[0] = self.overwriteArrayFields(0);
            tester.TestAssertion("Clearing array field by non-local function while write is pending", true);
            
            uint x = 3;
            tester.TestAssertion("Local function return values", (ROTRIGHT(x, 3) ^ ROTRIGHT(x, 18)) == 1610661888);
        }
    }
}
