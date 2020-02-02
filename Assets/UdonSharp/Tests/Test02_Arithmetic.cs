using UnityEngine;

public class Test02_Arithmetic : MonoBehaviour
{
    public int exportedIntTest;

    void Start()
    {
        //int localInt = 4, localInt2;

        //localInt2 = localInt = localInt2 = exportedIntTest = 5;

        //int resultInt = localInt2 + 5 * 10;

        float testAssignment = 0;
        testAssignment *= 0.5f;

        testAssignment = testAssignment + 5;
        
        //Debug.Log(resultInt);
        Debug.Log(testAssignment);
    }
}
