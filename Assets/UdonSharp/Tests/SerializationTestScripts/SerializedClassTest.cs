
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SerializedClassTest : UdonSharpBehaviour
{
    public int[] ints;
    
    public Vector3 vec;

    //public Vector3[] vecArr;

    //public int[][] jagged;

    void Start()
    {
        //jagged = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };
    }
}
