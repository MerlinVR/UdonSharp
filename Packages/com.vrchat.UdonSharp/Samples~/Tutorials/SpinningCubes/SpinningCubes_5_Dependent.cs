
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the fifth Udon spinning cube example (https://www.youtube.com/watch?v=tgbGetGdwiU)
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 5 Dependent")]
    public class SpinningCubes_5_Dependent : UdonSharpBehaviour 
    {
        bool isRotating = false;

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
