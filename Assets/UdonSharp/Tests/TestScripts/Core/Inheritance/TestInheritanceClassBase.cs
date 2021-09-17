
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    public class TestInheritanceClassBase : UdonSharpBehaviour
    {
        public virtual string GetClassName()
        {
            return "Base";
        }
    }
}
