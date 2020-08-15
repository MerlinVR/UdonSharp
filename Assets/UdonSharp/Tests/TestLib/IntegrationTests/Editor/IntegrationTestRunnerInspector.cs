using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UdonSharp.Tests
{
    [CustomEditor(typeof(IntegrationTestRunner))]
    public class IntegrationTestRunnerInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
}
