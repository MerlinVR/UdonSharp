
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class V1TestScript : UdonSharpBehaviour
{
    //public float myField1;
    //public double myField2 = 5f;
    //public V1TestScript myField3, myField4 = null;
    public List<float> myList;
    public List<float> myList2;

    void Start()
    {
        Debug.Log("Test!");

        //Vector3 myVec = new Vector3(1, 2, 3);

        //myVec.Normalize();

        //Vector3.Cross(Vector3.up, myVec);
    }
}
