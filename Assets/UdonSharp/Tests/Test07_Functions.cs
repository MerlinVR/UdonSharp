using UdonSharp;
using UnityEngine;

[AddComponentMenu("")]
public class Test07_Functions : UdonSharpBehaviour
{
    public void PrintTest()
    {
        Debug.Log("hello");
    }

    private void LogTestVar(string inputVar)
    {
        Debug.LogFormat("[Test] {0}", inputVar);
    }

    private string GetName()
    {
        return name;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.name);
         
    }

    private int GetInt()
    {
        return 45;
    }

    private bool CheckIfTrue()
    {
        Debug.Log("CheckIfTrue");
        return false;
    }

    private void Start()
    {
        Debug.Log(true && CheckIfTrue() && CheckIfTrue());

        //PrintTest();
        //LogTestVar("Hello 2");
        

        //Debug.Log(GetComponent(typeof(Transform)));
        //Debug.Log(GetName());

        //Debug.Log(6 * Vector3.up);
        //Debug.Log(GetInt());
    }
}
