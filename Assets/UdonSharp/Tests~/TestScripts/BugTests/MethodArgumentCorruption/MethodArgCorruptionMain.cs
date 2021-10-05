
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/MethodArgCorruptionMain")]
    public class MethodArgCorruptionMain : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            Method();
        }

#pragma warning disable CS0649
        [SerializeField]
        ArgCorruption2 udon;
#pragma warning restore CS0649

        // https://github.com/Merlin-san/UdonSharp/issues/41
        public void Method()
        {
            var obj1 = 123;
            // displayed 'Method1 type:System.Int32'
            udon.udon.Method1(obj1);
            // displayed 'Method1 type:System.Int32'
            obj1 = udon.udon.Method1(obj1);
            // displayed 'Method1 type:System.Int32'
            udon.udon.Method1(obj1);

            tester.TestAssertion("Int copy propagation test", obj1 == 123);

            var obj2 = (object)123;
            // displayed 'Method2 type:System.Int32'
            udon.udon.Method2(obj2);
            // Bug? displayed 'Method2 type:VRC.Udon.UdonBehaviour'
            obj2 = udon.udon.Method2(obj2);

            tester.TestAssertion("Object copy propagation test", obj2.GetType() == typeof(int) && (int)obj2 == 123);
        }
    }
}
