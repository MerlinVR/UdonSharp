
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR // These using statements must be wrapped in this check to prevent issues on builds
using UnityEditor;
using UdonSharpEditor;
#endif

namespace UdonSharp.Examples.Inspectors
{
    /// <summary>
    /// Example behaviour that has a custom inspector
    /// </summary>
    public class CustomInspectorBehaviour : UdonSharpBehaviour 
    {
        public string stringVal;

        private void Update()
        {
            Debug.Log($"CustomInspectorBehaviour: {stringVal}");
        }
    }

    // Editor scripts must be wrapped in a UNITY_EDITOR check to prevent issues while uploading worlds. The !COMPILER_UDONSHARP check prevents UdonSharp from throwing errors about unsupported code here.
#if !COMPILER_UDONSHARP && UNITY_EDITOR 
    [CustomEditor(typeof(CustomInspectorBehaviour))]
    public class CustomInspectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            CustomInspectorBehaviour inspectorBehaviour = (CustomInspectorBehaviour)target;

            EditorGUI.BeginChangeCheck();

            // A simple string field modification with Undo handling
            string newStrVal = EditorGUILayout.TextField("String Val", inspectorBehaviour.stringVal);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Modify string val");

                inspectorBehaviour.stringVal = newStrVal;
            }
        }
    }
#endif
}
