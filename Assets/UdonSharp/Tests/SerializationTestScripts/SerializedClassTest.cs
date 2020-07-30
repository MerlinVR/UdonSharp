
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SerializedClassTest : UdonSharpBehaviour
{
    public int[] ints;
    
    public Vector3 vec;

    public int defaultVal = 45;
    public int defaultVal2 = 600;

    public sbyte sbyteVal = 5;

    public char charField = 'a';

    public uint uintField = 42134;

    //public Vector3[] vecArr;

    public int[][] jagged = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };

    public Vector2 vec2;

    void Start()
    {
        //Debug.Log(sbyteVal);
        //jagged = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };
    }

    [ContextMenu("Print Array State")]
    public void PrintArrayState()
    {
        foreach (var ints in jagged)
        {
            if (ints != null)
            {
                foreach (int val in ints)
                {
                    Debug.Log(val);
                }
            }
        }
    }

    //public override void Interact()
    //{
    //}
}
