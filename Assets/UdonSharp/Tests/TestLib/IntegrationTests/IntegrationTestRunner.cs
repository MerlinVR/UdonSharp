
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UdonSharpEditor;
#endif

namespace UdonSharp.Tests
{
    [AddComponentMenu("Udon Sharp/Test Lib/Integration Test Runner")]
    public class IntegrationTestRunner : UdonSharpBehaviour
    {
        public bool runTestOnStart = true;
        public bool logPassedTests = true;

        public IntegrationTestSuite[] integrationTests;

        void Start()
        {
            if (runTestOnStart)
                RunTests();
        }

        public override void Interact()
        {
            RunTests();
        }

        void RunTests()
        {
            Debug.Log("[<color=#00AF54>UdonSharp Tests</color>] Starting tests");

            int totalTests = 0;
            int passedTests = 0;

            foreach (IntegrationTestSuite suite in integrationTests)
            {
                suite.printPassedTests = logPassedTests;
                suite.RunTests();

                totalTests += suite.GetTotalTestCount();
                passedTests += suite.GetSucceededTestCount();
            }

            Debug.Log($"[<color=#00AF54>UdonSharp Tests</color>] Tests finished [{passedTests}/{totalTests}]");
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(IntegrationTestRunner))]
    public class IntegrationTestRunnerInspector : Editor
    {
        ReorderableList testList;

        private void OnEnable()
        {
            //if (target == null) // Hack I need to figure out why this is breaking further down
            //    return;

            testList = new ReorderableList(serializedObject, serializedObject.FindProperty("integrationTests"), true, true, true, true);
            testList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                EditorGUI.PropertyField(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight), testList.serializedProperty.GetArrayElementAtIndex(index), label: new GUIContent());
            };
            testList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, "Integration Tests"); };
        }

        private void OnDisable()
        {
            testList = null;
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("runTestOnStart"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("logPassedTests"));

            EditorGUILayout.Space();

            testList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
