using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UdonSharpEditor;
#endif
using UnityEditor;
using UnityEngine;

namespace UdonSharp.Tests
{
#if UNITY_EDITOR
    [CustomEditor(typeof(DefaultHeapValueTest))]
    public class DefaultHeapValueTestInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            DefaultHeapValueTest heapValueTest = (DefaultHeapValueTest)target;

            EditorGUI.BeginChangeCheck();
            int newVal = EditorGUILayout.IntField("AAA field", (int)(heapValueTest.objectIntVal ?? 0));

            if (EditorGUI.EndChangeCheck())
                Undo.RecordObject(target, "Change aaa field");

            heapValueTest.objectIntVal = newVal;
            heapValueTest.syncedObjectIntVal = (int)heapValueTest.objectIntVal;
        }
    }
#endif
}
