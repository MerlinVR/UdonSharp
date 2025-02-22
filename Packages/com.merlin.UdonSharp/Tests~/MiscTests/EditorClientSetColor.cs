
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    public class EditorClientSetColor : UdonSharpBehaviour
    {
        void Start()
        {
            Material meshMaterial = GetComponent<MeshRenderer>().material;

#if UNITY_EDITOR
            meshMaterial.SetColor("_Color", Color.red);
#else
            meshMaterial.SetColor("_Color", Color.blue);
#endif
        }
    }
}
