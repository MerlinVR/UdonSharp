
using UnityEngine;

public class Test08_Instantiation_mono : MonoBehaviour
{
    public GameObject sourcePrefab;

    public int objectCount;

    public float rotationOffset;

    public float rotationSpeed;

    private GameObject[] spawnedObjects;

    private void Start()
    {
        spawnedObjects = new GameObject[objectCount];

        for (int i = 0; i < objectCount; ++i)
        {
            GameObject instantiatedObject = Instantiate(sourcePrefab);

            instantiatedObject.SetActive(true);
            instantiatedObject.transform.parent = transform;
            //instantiatedObject.transform.position = Random.insideUnitSphere * 2f;
            //instantiatedObject.transform.rotation = Random.rotation;
            //instantiatedObject.transform.localScale *= Random.Range(0.2f, 1.5f);

            spawnedObjects[i] = instantiatedObject;
        }
    }

    private void Update()
    {
        float time = Time.time;
        float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < objectCount; ++i)
        {
            GameObject spawnedObject = spawnedObjects[i];

            float progress = ((i / (float)objectCount) + rotationSpeed * time) * twoPi;
            //progress = progress + rotationSpeed * Time.time;
            //progress *= Mathf.PI * 2f;

            Vector3 newPosition = new Vector3(Mathf.Sin(progress), 0f, Mathf.Cos(progress)) * rotationOffset;
            newPosition += new Vector3(0f, Mathf.Cos(progress * 5f) * 0.2f, 0f);

            spawnedObject.transform.localPosition = newPosition;
            spawnedObject.transform.LookAt(transform.position, Vector3.up);
        }
    }
}
