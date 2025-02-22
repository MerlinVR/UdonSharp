
using UdonSharp;
using UdonSharp.Tests;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    internal class NonBehaviourClassesReferencedClass
    {
        public int Field = 5;
        public string AutoProp { get; set; } = "Hello";
        public OtherClass<float> OtherClassInstance = new OtherClass<float>();
    
        public int Property
        {
            get => Field;
            set => Field = value;
        }
    
        public bool TestMethod(int a, int b)
        {
            return a == b;
        }
    
        public T TestMethod2<T>(T a, T b)
        {
            return a;
        }
    
        public void SetOtherClassData(float val)
        {
            OtherClassInstance.Field = val;
        }
    
        public float GetOtherClassData()
        {
            return OtherClassInstance.Field;
        }
    }
}
