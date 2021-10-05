#if UNITY_EDITOR

using UdonSharp.Serialization;
using UnityEngine;
using VRC.Udon;

public class ClassSerializer : MonoBehaviour
{
    SerializedClassTest cComponent;
    public UdonBehaviour uComponent;

    bool ranInit = false;

    IValueStorage componentStorage;

    void Init()
    {
        if (Application.isPlaying && ranInit)
            return;

        cComponent = GetComponent<SerializedClassTest>();
        componentStorage = new SimpleValueStorage<UdonBehaviour>(uComponent);

        ranInit = true;
    }

    [ContextMenu("Udon->C#")]
    private void CopyToCSharp()
    {
        Init();

        Serializer.CreatePooled<SerializedClassTest>().Read(ref cComponent, componentStorage);
    }

    [ContextMenu("C#->Udon")]
    private void CopyToUdon(ClassSerializer serializedClass)
    {
        Init();

        Serializer.CreatePooled<SerializedClassTest>().Write(componentStorage, serializedClass.cComponent);
    }

    private void Update()
    {
        //CopyToCSharp();
        //ClassSerializer self = this;
        //CopyToUdon(self);
    }
}

#endif
