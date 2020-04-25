
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

        public void ExecuteTests()
        {
        }

        
    }
}
