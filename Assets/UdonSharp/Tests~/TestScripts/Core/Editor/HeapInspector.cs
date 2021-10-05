using System.Collections;
using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace UdonSharp.Tests
{
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
}
