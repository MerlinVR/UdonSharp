
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Certain Udon externs are exposed not via the C# type or interface they're implemented on, but rather
    /// on some subclass directly. This test verifies that we can search for such an alternate subclass and
    /// use it for the extern invocation.
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tests/AccessViaAlternateInvocee")]
    public class AccessViaAlternateInvocee : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            System.Type tyString = typeof(string);
            tester.TestAssertion("Access System.Type.Name", tyString.Name.Equals("String"));
        }
    }
}
