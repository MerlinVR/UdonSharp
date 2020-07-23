
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Inspectors
{
    /// <summary>
    /// Example behaviour that has a custom inspector
    /// </summary>
    public class CustomInspectorBehaviour : UdonSharpBehaviour 
    {
        public GameObject[] gameObjects;

        private void Update()
        {
            string gameObjectNames = "Names: " + string.Join(", ", (object[])gameObjects);


            Debug.Log(gameObjectNames);
        }
    }
}
