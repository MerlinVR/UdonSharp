using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

//namespace UdonSharpTests
//{
    [AddComponentMenu("")] 
    public class Test08_Instantiation : UdonSharpBehaviour
    {
        public GameObject sourcePrefab;
        
        public int objectCount;
        
        public float rotationOffset;

        public float rotationSpeed;

        private GameObject[] spawnedObjects;
        
        [HideInInspector]
        public UdonBehaviour otherBehaviour;

        string[] listInitTest = new string[]
         {
                "Hello",
                "test",
                "string",
                "aaaa",
         };

        private void testFunc(short input)
        {

        }

        private void Start()
        {
            spawnedObjects = new GameObject[objectCount];

            for (int i = 0; i < objectCount; ++i)
            {
                GameObject instantiatedObject = Instantiate(sourcePrefab);
                UdonBehaviour behaviour = (UdonBehaviour)instantiatedObject.GetComponent(typeof(UdonBehaviour));

                //Debug.Log(behaviour);

                behaviour.SetProgramVariable("displayName", "hello");
                //Debug.Log(behaviour.GetProgramVariable("displayName"));

                instantiatedObject.SetActive(true);
                instantiatedObject.transform.parent = transform;
                //instantiatedObject.transform.position = Random.insideUnitSphere * 2f;
                //instantiatedObject.transform.rotation = Random.rotation;
                //instantiatedObject.transform.localScale *= Random.Range(0.2f, 1.5f);

                spawnedObjects[i] = instantiatedObject;
            }
        }

        private void OnEnable()
        {
            //testFunc(4);

            //Debug.Log("hello! 15");

            //foreach (var test in "hello")
            //{
            //    Debug.Log(test); 
            //}

            //ushort testVal = 4f;

            //otherBehaviour.SendCustomEvent("PrintTest"); 
             

            foreach (GameObject gameObj in spawnedObjects)
            {

                UdonBehaviour behaviour = (UdonBehaviour)gameObj.GetComponent(typeof(UdonBehaviour));
                behaviour.SetProgramVariable("displayName", "hello");
            }

            Debug.Log($"initialized on frame {Time.frameCount}");
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            Debug.LogFormat("Player {0} joined!", player.displayName);
        }

        private void FixedUpdate()
        {
            float time = Time.time;
            float twoPi = Mathf.PI * 2f;

        //Vector3 assignmentTarget = Vector3.zero;
        //Vector3 assignmentSource = Vector3.zero;

        Debug.Log($"Update manager frame {Time.frameCount}");

            for (int i = 0; i < objectCount; ++i)
            {
                //assignmentTarget = assignmentSource;

                GameObject spawnedObject = spawnedObjects[i];
                //UdonBehaviour behaviour = (UdonBehaviour)spawnedObject.GetComponent(typeof(UdonBehaviour));
                //behaviour.SetProgramVariable("displayName", "hello there");

                Vector3 testVec = new Vector3(4, 5, 6);

                float progress = ((i / (float)objectCount) + rotationSpeed * time) * twoPi;

                Vector3 newPosition = new Vector3(Mathf.Sin(progress), 0f, Mathf.Cos(progress)) * rotationOffset + new Vector3(0f, Mathf.Cos(progress * 5f) * 0.2f, 0f);

                spawnedObject.transform.localPosition = newPosition;
                spawnedObject.transform.LookAt(transform.position, Vector3.up);
            }
        }
    }

//}