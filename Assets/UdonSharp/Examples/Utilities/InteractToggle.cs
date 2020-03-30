
//using UdonSharp;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

//namespace UdonSharp.Examples.Utilities
//{
    /// <summary>
    /// A Basic example class that demonstrates how to toggle a list of object on and off when someone interacts with the UdonBehaviour
    /// This toggle only works locally
    /// </summary>
    public class InteractToggle : UdonSharpBehaviour 
    {
        [Tooltip("List of objects to toggle on and off")]
        public GameObject[] toggleObjects;

        public override void Interact() 
        {
            foreach (GameObject toggleObject in toggleObjects)
            {
                toggleObject.SetActive(!toggleObject.activeSelf);
            }
        }

        private void Start()
        {
            VRCStation station = (VRCStation)GetComponent(typeof(VRCStation));
            station.PlayerMobility = VRC_Station.Mobility.ImmobilizeForVehicle; 
        }
    }
//}
