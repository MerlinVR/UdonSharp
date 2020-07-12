
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

    //public Vector3[] vecArr;

    public int[][] jagged = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };

    void Start()
    {
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
}
