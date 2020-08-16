#if UNITY_EDITOR

using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace UdonSharp.Examples.Inspectors
{
    [CustomEditor(typeof(CustomInspectorBehaviour))]
    public class CustomInspectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(target) ||
                UdonSharpGUI.DrawProgramSource(target, false))
                return;

            base.OnInspectorGUI();
        }
    }
}

#endif
