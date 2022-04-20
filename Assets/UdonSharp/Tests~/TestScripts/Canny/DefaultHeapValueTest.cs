
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Tests/DefaultHeapValueTest")]
    public class DefaultHeapValueTest : UdonSharpBehaviour
    {
        [System.NonSerialized]
        public IntegrationTestSuite tester;

        public void ExecuteTests()
        {
            TestPublicArray();
            TestPublicString();
            TestSyncedString();
            TestPrivateArr();
            TestPrivateStr();
            TestDefaultByte();
            TestJaggedArrays();
            TestObjectInt();
            TestDefaultReferences();
        }

        [HideInInspector]
        public string[] defaultPublicArr;
        
        [HideInInspector]
        public UdonSharpBehaviour[] emptyUdonBehaviourArray;

        [SerializeField]
        public readonly string readonlyStr = "aaa";

        [SerializeField]
        public const string constStr = "bbb";

        void TestPublicArray()
        {
            tester.TestAssertion("Default Public Array Initialized", defaultPublicArr != null && defaultPublicArr.Length == 0);
            tester.TestAssertion("Default UdonSharpBehaviour Public Array Initialized", emptyUdonBehaviourArray != null && emptyUdonBehaviourArray.Length == 0);
        }

        [HideInInspector]
        public string defaultString;

        void TestPublicString()
        {
            tester.TestAssertion("Default String Value", defaultString == "");
        }

#pragma warning disable 0649
        [UdonSynced]
        string networkSyncedString;
#pragma warning restore 0649

        [UdonSynced]
        string networkSyncedStringDefaultEmpty = "";

        [UdonSynced]
        string networkSyncedStringDefaultValue = "hello";

        [UdonSynced, HideInInspector]
        public string publicNetworkSyncedString;

        [UdonSynced, HideInInspector]
        public string publicNetworkSyncedStringDefaultEmpty = "";

        [UdonSynced, HideInInspector]
        public string publicNetworkSyncedStringDefaultValue = "hello";

        void TestSyncedString()
        {
            tester.TestAssertion("Uninitialized Synced String", networkSyncedString == "");
            tester.TestAssertion("Empty Synced String Value", networkSyncedStringDefaultEmpty == "");
            tester.TestAssertion("Default Synced String Value", networkSyncedStringDefaultValue == "hello");
            
            tester.TestAssertion("Uninitialized Public Synced String", publicNetworkSyncedString == "");
            tester.TestAssertion("Empty Public Synced String Value", publicNetworkSyncedStringDefaultEmpty == "");
            tester.TestAssertion("Default Public Synced String Value", publicNetworkSyncedStringDefaultValue == "hello");
        }

#pragma warning disable 0649
        string[] privateStrArr;
        string privateStr;
#pragma warning restore 0649
        string privateNullInitializedString = null;
        string privateInitializedString = "Test";

        void TestPrivateArr()
        {
            tester.TestAssertion("Private Array Default Null", privateStrArr == null);
        }

        void TestPrivateStr()
        {
            tester.TestAssertion("Private String Default Null", privateStr == null);
            tester.TestAssertion("Private null initialized String", privateNullInitializedString == null);
            tester.TestAssertion("Private initialized String", privateInitializedString == "Test");
        }
        
        byte byteVal = 0b1000_0000;

        void TestDefaultByte()
        {
            tester.TestAssertion("Byte type match", byteVal.GetType() == typeof(byte));
        }

        int[][] jaggedArrayInitialVal = new[] { new[] { 1, 2 }, new[] { 3, 4 }, null };

        void TestJaggedArrays()
        {
            tester.TestAssertion("Jagged array initialized", jaggedArrayInitialVal != null);
            tester.TestAssertion("Jagged array type", jaggedArrayInitialVal.GetType() == typeof(object[]));
            tester.TestAssertion("Jagged array element type", jaggedArrayInitialVal[0].GetType() == typeof(int[]));
            tester.TestAssertion("Jagged array values", jaggedArrayInitialVal[0][0] == 1 &&
                                                        jaggedArrayInitialVal[0][1] == 2 &&
                                                        jaggedArrayInitialVal[1][0] == 3 &&
                                                        jaggedArrayInitialVal[1][1] == 4 &&
                                                        jaggedArrayInitialVal[2] == null);
        }
        
        public object objectIntVal = 5;
        // Should by synced by the custom inspector to the same value as objectIntVal
        [HideInInspector]
        public object syncedObjectIntVal = 5;

        void TestObjectInt()
        {
            tester.TestAssertion("Object int is valid", (int)objectIntVal == (int)syncedObjectIntVal);
        }     
 
        //[HideInInspector]
        public Transform nullTransform;
        //[HideInInspector]
        public GameObject nullGameObject;
        //[HideInInspector]
        public UdonBehaviour nullUdonBehaviour;

        void TestDefaultReferences()
        {
            tester.TestAssertion("Default Transform null", nullTransform == null);
            tester.TestAssertion("Default GameObject null", nullGameObject == null);
            tester.TestAssertion("Default UdonBehaviour null", nullUdonBehaviour == null);
        }
    }
}
