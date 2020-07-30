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
            if (UdonSharpGUI.DrawConvertToUdonBehaviourButton(target as UdonSharpBehaviour))
                return;

            UdonBehaviour backingBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(target as UdonSharpBehaviour);

            if (backingBehaviour)
                UdonSharpGUI.DrawProgramSource(backingBehaviour, false);

            base.OnInspectorGUI();
        }
    }
}

#endif
