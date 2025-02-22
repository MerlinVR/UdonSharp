using UdonSharp;
using UnityEngine;

[AddComponentMenu("")]
public class Test06_Raycast : UdonSharpBehaviour
{
    private void Start()
    {
        if (true /*&& false && testObject.activeSelf*/)
        {
            Debug.Log("Hello");
        }
    }

    private void Update()
    {
        RaycastHit raycastHit;

        if (Physics.Raycast(new Ray(transform.position, transform.up), out raycastHit))
        {
            Debug.DrawLine(transform.position, raycastHit.point, Color.red);
            Debug.DrawLine(raycastHit.point, raycastHit.point + raycastHit.normal * 0.5f, Color.blue);
        }
    }
}
