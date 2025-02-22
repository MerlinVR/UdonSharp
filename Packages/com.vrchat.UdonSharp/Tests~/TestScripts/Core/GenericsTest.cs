
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/GenericsTest")]
    public class GenericsTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            float[] floatArray = { 1f, 2f };
            floatArray = floatArray.AddElement(3f);

            tester.TestAssertion("Generic variant 1", floatArray.Length == 3 && 
                                                      floatArray[0] == 1f && floatArray[1] == 2f && floatArray[2] == 3f);
            
            int[] intArray = { 1, 2 };
            intArray = intArray.AddElement(3);
            
            tester.TestAssertion("Generic variant 2", intArray.Length == 3 && 
                                                      intArray[0] == 1 && intArray[1] == 2 && intArray[2] == 3);

            UnityEngine.Object[] objects = { gameObject, transform };
            objects = objects.AddElement(this);
            
            tester.TestAssertion("Generic variant 3", objects.Length == 3 && 
                                                      objects[0] == gameObject && objects[1] == transform && objects[2] == this);
        }
    }

    internal static class GenericMethodClass
    {
        public static T[] AddElement<T>(this T[] array, T item)
        {
            T[] newArray = new T[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[array.Length] = item;
            array = newArray;
            return array;
        }
    }
}
