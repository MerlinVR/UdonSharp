
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Inspectors
{
    /// <summary>
    /// Example behaviour that has a custom inspector
    /// </summary>
    /// 
    public class CustomInspectorBehaviour : UdonSharpBehaviour 
    {
        public GameObject[] gameObjects;

        public GameObject refGameObject;

        public Vector3 behaviourPos;
         
        //public float randomFloat;

        private void Update() 
        {
            string gameObjectNames2 = "Names: " + string.Join(", ", (object[])gameObjects);
            //string gameObjectNames = "Names: ";
            //foreach (GameObject obj in gameObjects)
            //{
            //    if (obj)
            //        gameObjectNames += obj.ToString() + ", ";
            //    else
            //        gameObjectNames += "null, ";
            //}

            //Debug.Log(gameObjectNames);
            //Debug.Log(gameObjectNames2);
            //Debug.Log(gameObjects);
            //Debug.Log(refGameObject);

            transform.position = behaviourPos; 

            //randomFloat = Random.Range(0f, 100f);
        }
    }
}
