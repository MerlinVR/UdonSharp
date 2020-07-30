#if UNITY_EDITOR

using UdonSharpEditor;
using UnityEditor;

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

#endif
