
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Incorrect COW handling across a ternary can cause intermediate values accessed after the completion of the
    /// ternary to use uninitialized values.
    /// </summary>
    public class TestTernary : UdonSharpBehaviour
    {
        private TestTernary x;

        [System.NonSerialized] public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            enabled = true ? true : val;
            enabled = false ? true : val;
            tester.TestAssertion("Ternary access COWing 'this' succeeded", true);
            
            x = this;
            x.enabled = true ? true : val;
            x.enabled = false ? true : x.val;
            tester.TestAssertion("Ternary access COWing a field succeeded", true);
        }

        private void Start()
        {
            enabled = true ? true : val;

            x = this;
            x.enabled = true ? true : x.val;
        }

        public bool val => true;
    }
}