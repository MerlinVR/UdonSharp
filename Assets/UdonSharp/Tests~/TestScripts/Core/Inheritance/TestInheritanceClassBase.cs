
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    public abstract class TestInheritanceClassBase : UdonSharpBehaviour
    {
        [SerializeField]
        private int baseClassField = 4;

        public int BaseClassField => baseClassField;

        private int _nonSerializedField = 5;

        public int NonSerializedField => _nonSerializedField;
        
        public virtual string GetClassName()
        {
            return "Base";
        }

        public abstract int GetClassID();
    }
}
