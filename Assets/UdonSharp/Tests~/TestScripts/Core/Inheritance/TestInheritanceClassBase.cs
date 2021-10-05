
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    public abstract class TestInheritanceClassBase : UdonSharpBehaviour
    {
        public virtual string GetClassName()
        {
            return "Base";
        }

        public abstract int GetClassID();
    }
}
