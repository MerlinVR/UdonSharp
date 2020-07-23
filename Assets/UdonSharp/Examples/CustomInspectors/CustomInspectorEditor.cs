using System.Collections;
using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;


namespace UdonSharp.Examples.Inspectors
{
    [CustomEditor(typeof(CustomInspectorBehaviour))]
    public class CustomInspectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(target as UdonSharpBehaviour))
                return;

            base.OnInspectorGUI();
        }
    }
}

