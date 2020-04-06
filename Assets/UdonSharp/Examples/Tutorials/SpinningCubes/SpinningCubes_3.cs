
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the third Udon spinning cube example (https://www.youtube.com/watch?v=JlzT0LccdmU)
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 3")]
    public class SpinningCubes_3 : UdonSharpBehaviour 
    {
        public Transform cubeToRotate;

        private void Start()
        {
            // Public variables in U# do not automatically initialize to the object unlike normal Udon, so we need to initialize the cubeToRotate if it's not set.
            if (cubeToRotate == null)
                cubeToRotate = transform;
        }

        private void Update()
        {
            cubeToRotate.Rotate(Vector3.up, 60f * Time.deltaTime);
        }
    }
}
