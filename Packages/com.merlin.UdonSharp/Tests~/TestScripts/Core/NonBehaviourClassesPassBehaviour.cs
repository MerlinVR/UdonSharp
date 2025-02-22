
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    internal class NonBehaviourClassesPassBehaviour : UdonSharpBehaviour
    {
        [NonSerialized]
        public NonBehaviourClassesReferencedClass referencedClass = new NonBehaviourClassesReferencedClass();

        public float GetOtherClassData()
        {
            return referencedClass.GetOtherClassData();
        }
        
        public void SetOtherClassData(float val)
        {
            referencedClass.SetOtherClassData(val);
        }
        
        public NonBehaviourClassesReferencedClass ReferencedClass => referencedClass;
    }
}
