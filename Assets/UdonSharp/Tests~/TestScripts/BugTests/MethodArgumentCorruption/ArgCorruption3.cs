
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/ArgCorruption3")]
    public class ArgCorruption3 : UdonSharpBehaviour
    {
        public int Method1(int val)
        {
            //Debug.Log($"Method1 type:{val.GetType()}");
            return val;
        }

        public object Method2(object val)
        {
            //Debug.Log($"Method2 type:{val.GetType()}");
            return val;
        }
    }
}
