using UdonSharp;
using UnityEngine;

[AddComponentMenu("")]
public class Test01_DebugLog : UdonSharpBehaviour
{
    public string displayName;

    void Start()
    {
        //Debug.Log("Hello, World! - From, Udon#");
        Debug.Log($"Initialized instantiated object on frame {Time.frameCount}");
    }

    private void Update()
    {
        Debug.Log(displayName);
    }
}
