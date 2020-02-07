using UnityEngine;

[AddComponentMenu("")]
public class Test02_Arithmetic : MonoBehaviour
{
    public int exportedIntTest;

    void Start()
    {
        int localInt = 4, localInt2;

        localInt2 = localInt = localInt2 = 5;

        int resultInt = localInt2 + 5 * 10;

        float testAssignment = 10;
        testAssignment *= 0.5f;

        testAssignment = (testAssignment + 5f) * 20;

        float floatVar = 2;
        Debug.Log(floatVar.GetType());

        int bitshift = (int)4.0;
        
        bitshift <<= 2; // 16
        bitshift >>= 1; // 8
        ++bitshift;
        Debug.Log(bitshift);
        Debug.Log(--bitshift);

        Debug.Log(bitshift++);
        Debug.Log(bitshift);

        Debug.Log(resultInt);
        Debug.Log(testAssignment + " hello " + typeof(float));

        Debug.Log(bitshift >= exportedIntTest);
    }
}
