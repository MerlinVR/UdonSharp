using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("")]
public class Test05_BehaviourInteractions : UdonSharpBehaviour
{
    private float[] spectrumData;

    private AudioSource audioSource;
    private Text textComponent;

    public Transform referenceTransform;

    Transform GetTestObject()
    {
        Debug.Log("On noooo");
        return null;
    }

    void Start()
    {
        //Renderer rendererVar = null;
        //if (rendererVar == null)
        //    Debug.Log("The renderer var is null!");
        //else
        //    Debug.Log("The renderer var is not null!");

        //var renderer = (Renderer)GetComponentInChildren(typeof(MeshRenderer), false);

        //Debug.Log(renderer.HasPropertyBlock());

        //SetProgramVariable("degreesPerSecond", 4f);

        spectrumData = new float[128];

        audioSource = (AudioSource)GetComponentInChildren(typeof(AudioSource));
        textComponent = (Text)GetComponentInChildren(typeof(Text));
        

    }

    private void Update()
    {
        float totalSpectrumData = 0f;

        audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

        foreach (float val in spectrumData)
        {
            totalSpectrumData += val;
        }  

        //textComponent.text = string.Format("{0:0.###}", totalSpectrumData * 10f);
        textComponent.text = $"Test: {totalSpectrumData * 10f:0.###}";

        Debug.Log(referenceTransform ?? GetTestObject());

        //transform.Rotate(Vector3.up, Time.deltaTime * degreesPerSecond);
    }
}
