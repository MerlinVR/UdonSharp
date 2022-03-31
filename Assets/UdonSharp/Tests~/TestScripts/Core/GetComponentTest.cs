
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;
using VRCStation = VRC.SDKBase.VRCStation;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/GetComponentTest")]
    public class GetComponentTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;
        
        public GameObject modObject;
        public GameObject inheritContainer;

        Transform[] transforms;

        Transform transformIn = null;

        VRCUnityVideoPlayer videoPlayer;
        VRCAVProVideoPlayer avProVideoPlayer;
        BaseVRCVideoPlayer baseVideoPlayer;

        public void ExecuteTests()
        {
            SetProgramVariable("transformIn", transform);

            tester.TestAssertion("GetComponent on object strongbox", transformIn.GetComponent<Transform>().position != Vector3.one * 1000f); // Shouldn't throw

            tester.TestAssertion("GetComponent UdonSharp Type", transformIn.GetComponent<GetComponentTest>() != null);

            tester.TestAssertion("GetComponent on GameObject", transformIn.gameObject.GetComponent<Transform>() != null);

            tester.TestAssertion("GetComponent user type on GameObject", transformIn.gameObject.GetComponent<GetComponentTest>() != null);
            
            tester.TestAssertion("GetComponent on GameObject 2", GameObject.Find("GetComponents").GetComponent<GetComponentTest>() != null);

            transforms = new[] { transform, transform };

            // Udon StrongBox assignment test
            transforms[1].GetComponentInChildren<BoxCollider>().enabled = false;
            transforms[1].GetComponentInChildren<BoxCollider>().enabled = true;
            
            tester.TestAssertion("GetComponent<Transform>()", GetComponent<Transform>() != null);

            tester.TestAssertion("GetComponent(typeof(Transform))", GetComponent(typeof(Transform)) != null);

            BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>();
            
            tester.TestAssertion("GetComponentsInChildren<BoxCollider>()", colliders.Length == 3);

            tester.TestAssertion("GetComponentInChildren<NameOf>()", GetComponentInChildren<NameOf>() != null);

            NameOf[] nameOfs = GetComponentsInChildren<NameOf>();

            tester.TestAssertion("GetComponentsInChildren<NameOf>()", nameOfs.Length == 3);
            
            tester.TestAssertion("GetComponentsInChildren<MeshRenderer>()", GetComponentsInChildren<MeshRenderer>().Length == 2);

#if COMPILER_UDONSHARP
            UdonSharpBehaviour getBehaviour = (UdonSharpBehaviour)modObject.GetComponent(typeof(UdonBehaviour));

            tester.TestAssertion("Get UdonBehaviour typeof(UdonBehaviour)", getBehaviour != null);

            long typeID = GetUdonTypeID<NameOf>();

            tester.TestAssertion("Type ID matches", typeID == getBehaviour.GetUdonTypeID());
#endif

            tester.TestAssertion("Correct number of UdonBehaviours on gameobject", modObject.GetComponents(typeof(UdonBehaviour)).Length == 4);

            videoPlayer = (VRCUnityVideoPlayer)GetComponent(typeof(VRCUnityVideoPlayer));
            avProVideoPlayer = (VRCAVProVideoPlayer)GetComponent(typeof(VRCAVProVideoPlayer));
            baseVideoPlayer = (BaseVRCVideoPlayer)GetComponent(typeof(BaseVRCVideoPlayer));

            VRCStation station;
            station = (VRCStation)GetComponent(typeof(VRCStation));

            // Should compile
            GetComponents<Collider>()[0].enabled = false;
            GetComponents<Collider>()[0].enabled = true;
            
            // U# inheritance
            tester.TestAssertion("Base type GetComponent", inheritContainer.GetComponents<TestInheritanceClassBase>().Length == 3);
            tester.TestAssertion("Inherited GetComponent 1", inheritContainer.GetComponents<ClassA>().Length == 1);
            tester.TestAssertion("Inherited GetComponent 2", inheritContainer.GetComponents<ClassB>().Length == 2);
            tester.TestAssertion("Inherited GetComponent 3", inheritContainer.GetComponents<ClassC>().Length == 1);
            
            tester.TestAssertion("Get UdonBehaviour", GetComponent<UdonBehaviour>());
            tester.TestAssertion("Get UdonBehaviours", GetComponents<UdonBehaviour>().Length == 2);
            tester.TestAssertion("Get UdonBehaviours 2", inheritContainer.GetComponents<UdonBehaviour>().Length == 5);
            tester.TestAssertion("Get VRC component 1", GetComponent<VRCObjectSync>());
            tester.TestAssertion("Get VRC component 2", GetComponent<VRCAvatarPedestal>());
            tester.TestAssertion("Get VRC component 3", GetComponent<VRC_AvatarPedestal>());
            tester.TestAssertion("Get VRC components 1", GetComponents<VRC_AvatarPedestal>().Length == 1 && GetComponents<VRC_AvatarPedestal>()[0] != null);
        }
    }
}
