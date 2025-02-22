
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    /// <summary>
    /// Negative SByte values get read as 0 due to a bug in Odin that hasn't been fixed in VRC's branch
    /// 
    /// https://github.com/TeamSirenix/odin-serializer/issues/40
    /// https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/negative-sbytes-do-not-get-serialized-correctly
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tests/SByteSerialization")]
    public class SByteSerialization : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        sbyte negativeSbyte = -1;

        public void ExecuteTests()
        {
            tester.TestAssertion("Negative SByte serialization", negativeSbyte == -1);
        }
    }
}
