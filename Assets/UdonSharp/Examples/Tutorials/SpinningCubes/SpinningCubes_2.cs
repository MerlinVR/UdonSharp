
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the second Udon spinning cube example (https://www.youtube.com/watch?v=_pbf9hakP9E)
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 2")]
    public class SpinningCubes_2 : UdonSharpBehaviour 
    {
        private void Update()
        {
            transform.Rotate(Vector3.up, 60f * Time.deltaTime);
        }
    }
}
