using UdonSharp;
using UnityEngine;

[AddComponentMenu("")]
public class Test04_Constructors : UdonSharpBehaviour
{
    float[] floatArr;

    void Start()
    {
        //Vector3 vector3 = new Vector3(4f, 5f, 4f);
        //vector3.y = 20f;

        //Debug.Log(string.Concat("gello", "hai", "ello", " uuuhhhh", " gsgsgfs", "dasfdawf", 5));

        floatArr = new float[400];

        for (int i = 0; i < floatArr.Length; ++i)
        {
            floatArr[i] = i;
        }

        float resultTotal = 0f;

        foreach (float val in floatArr)
        {
            Debug.Log(val);
            resultTotal += val;
            Debug.Log(resultTotal);
        }

        Debug.Log(resultTotal);

        //Debug.Log(vector3.y);

        //Debug.Log((vector3.x > 3));

        //Debug.LogFormat("{0}, 0x{1:X4}, {2}", 4, 50, 4.5f);
    }
}
