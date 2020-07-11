#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UdonSharp.Serialization;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class ClassSerializer : MonoBehaviour
{
    SerializedClassTest cComponent;
    public UdonBehaviour uComponent;

    bool ranInit = false;

    IValueStorage componentStorage;
    Serializer<SerializedClassTest> classSerializer;

    void Init()
    {
        if (ranInit)
            return;

        cComponent = GetComponent<SerializedClassTest>();
        componentStorage = new SimpleValueStorage<UdonBehaviour>(uComponent);
        classSerializer = Serializer.CreatePooled<SerializedClassTest>();

        ranInit = true;
    }

    [ContextMenu("Udon->C#")]
    private void CopyToCSharp()
    {
        Init();

        classSerializer.Read(ref cComponent, componentStorage);
    }

    [ContextMenu("C#->Udon")]
    private void CopyToUdon()
    {
        Init();

        classSerializer.Write(componentStorage, in cComponent);
    }

    private void Update()
    {
        //CopyToUdon(ref cComponent.ints);
        //CopyToCSharp();
        CopyToUdon();
    }
}

#endif
