using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("")]
public class Test05_BehaviourInteractions : UdonSharpBehavior
{
    private float[] spectrumData;

    private AudioSource audioSource;
    private Text textComponent;

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

        textComponent.text = string.Format("{0:0.###}", totalSpectrumData * 10f);

        //transform.Rotate(Vector3.up, Time.deltaTime * degreesPerSecond);
    }
}
