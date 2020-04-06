
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Tutorials
{
    /// <summary>
    /// U# implementation of the fourth Udon spinning cube example (https://www.youtube.com/watch?v=HSpvrfWICeI)
    /// </summary>
    [AddComponentMenu("Udon Sharp/Tutorials/Spinning Cubes 4 Signal")]
    public class SpinningCubes_4_Signal : UdonSharpBehaviour 
    {
        public float speed = 60f;
    }
}
