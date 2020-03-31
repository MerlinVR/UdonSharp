
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the first Udon spinning cube example (https://www.youtube.com/watch?v=SP4K89z_Qck) 
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 1")]
    public class SpinningCubes_1 : UdonSharpBehaviour 
    {
        private void Update()
        {
            transform.Rotate(Vector3.up, 1f);
        }
    }
}
