
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/UserPropertyTest")]

    public class PropertyTestReferenceScript : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public int _value = 1;
        public int Value { get => _value; set => _value = value; }
        public int GetValue() => _value;
    }
}
