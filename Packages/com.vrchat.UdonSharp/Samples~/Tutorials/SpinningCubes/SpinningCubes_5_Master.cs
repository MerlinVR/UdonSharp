
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the fifth Udon spinning cube example (https://www.youtube.com/watch?v=tgbGetGdwiU)
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 5 Master")]
    public class SpinningCubes_5_Master : UdonSharpBehaviour 
    {
        public GameObject cubeToRotate;

        UdonBehaviour udonRotateTarget;
        bool isRotating = false;

        private void Start()
        {
            udonRotateTarget = (UdonBehaviour)cubeToRotate.GetComponent(typeof(UdonBehaviour));
        }

        private void OnMouseDown()
        {
            udonRotateTarget.SendCustomEvent("DoRotate");
        }

        public void DoRotate()
        {
            isRotating = !isRotating;
        }

        private void Update()
        {
            if (isRotating)
                transform.Rotate(Vector3.up, 60f * Time.deltaTime);
        }
    }
}
