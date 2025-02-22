
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the fourth Udon spinning cube example (https://www.youtube.com/watch?v=HSpvrfWICeI)
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 4 Spinner")]
    public class SpinningCubes_4_Spinner : UdonSharpBehaviour 
    {
        public SpinningCubes_4_Signal signal;

        private void Update()
        {
            transform.Rotate(Vector3.up, signal.speed * Time.deltaTime);
        }
    }
}
