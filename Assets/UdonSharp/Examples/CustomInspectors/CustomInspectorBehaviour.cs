
using UnityEngine;
using VRC.SDKBase;

#if !COMPILER_UDONSHARP && UNITY_EDITOR // These using statements must be wrapped in this check to prevent issues on builds
using UnityEditor;
using UdonSharpEditor;
using System.Linq;
#endif

namespace UdonSharp.Examples.Inspectors
{
    /// <summary>
    /// Example behaviour that has a custom inspector
    /// </summary>
    [AddComponentMenu("Udon Sharp/Inspectors/Custom Inspector Behaviour")]
    public class CustomInspectorBehaviour : UdonSharpBehaviour 
    {
        public string stringVal;

        public Vector3 orbitOrigin = new Vector3(0, 3f, 0);

        public CustomInspectorChildBehaviour[] childBehaviours;

        public VRCStation[] stations;
    }

    // Editor scripts must be wrapped in a UNITY_EDITOR check to prevent issues while uploading worlds. The !COMPILER_UDONSHARP check prevents UdonSharp from throwing errors about unsupported code here.
#if !COMPILER_UDONSHARP && UNITY_EDITOR 
    [CustomEditor(typeof(CustomInspectorBehaviour))]
    public class CustomInspectorEditor : Editor
    {
        SerializedProperty stationProperty;

        private void OnEnable()
        {
            stationProperty = serializedObject.FindProperty("stations");
        }

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            CustomInspectorBehaviour inspectorBehaviour = (CustomInspectorBehaviour)target;

            //EditorGUILayout.PropertyField(stationProperty, true);
            //serializedObject.ApplyModifiedProperties();

            EditorGUI.BeginChangeCheck();

            // A simple string field modification with Undo handling
            string newStrVal = EditorGUILayout.TextField("String Val", inspectorBehaviour.stringVal);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Modify string val");

                inspectorBehaviour.stringVal = newStrVal;
            }

            EditorGUI.BeginDisabledGroup(true);

            EditorGUILayout.Vector3Field("Orbit Origin", inspectorBehaviour.orbitOrigin); // Set via handles in OnSceneGUI

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            DrawChildBehaviourControls(inspectorBehaviour);
        }

        void DrawChildBehaviourControls(CustomInspectorBehaviour inspectorBehaviour)
        {
            EditorGUILayout.LabelField($"Child count: {inspectorBehaviour.childBehaviours.Length}");

            if (GUILayout.Button("Add child behaviour"))
            {
                GameObject newGameObject = new GameObject("Custom Inspector Child");
                Undo.RegisterCreatedObjectUndo(newGameObject, "Create child behaviour");
                newGameObject.transform.SetParent(inspectorBehaviour.transform);

                CustomInspectorChildBehaviour newChild = newGameObject.AddUdonSharpComponent<CustomInspectorChildBehaviour>();

                newChild.parentBehaviour = inspectorBehaviour;
                newChild.myIndex = inspectorBehaviour.childBehaviours.Length;

                Undo.RecordObject(inspectorBehaviour, "Add child behaviour");
                inspectorBehaviour.childBehaviours = inspectorBehaviour.childBehaviours.Append(newChild).ToArray();

                // Will recursively apply the child behaviour as well since it is referenced in the childBehaviours array
                inspectorBehaviour.ApplyProxyModifications(ProxySerializationPolicy.All);
            }

            if (GUILayout.Button("Remove child behaviour"))
            {
                if (inspectorBehaviour.childBehaviours.Length > 0)
                {
                    CustomInspectorChildBehaviour child = inspectorBehaviour.childBehaviours.Last();

                    Undo.RecordObject(inspectorBehaviour, "Delete child object");
                    inspectorBehaviour.childBehaviours = inspectorBehaviour.childBehaviours.Take(inspectorBehaviour.childBehaviours.Length - 1).ToArray();

                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private void OnSceneGUI()
        {
            CustomInspectorBehaviour inspectorBehaviour = (CustomInspectorBehaviour)target;

            EditorGUI.BeginChangeCheck();
            Vector3 newTarget = Handles.PositionHandle(inspectorBehaviour.orbitOrigin, Quaternion.identity);
            Handles.Label(newTarget, "Orbit Origin");

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Move orbit origin");
                inspectorBehaviour.orbitOrigin = newTarget;
            }

            Color oldColor = Handles.color;
            Handles.color = Color.red;

            foreach (CustomInspectorChildBehaviour child in inspectorBehaviour.childBehaviours)
            {
                Handles.DrawWireDisc(child.transform.position, Vector3.up, 0.2f);
            }

            Handles.color = oldColor;
        }
    }
#endif
}
