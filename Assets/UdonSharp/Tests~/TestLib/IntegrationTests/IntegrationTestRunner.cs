
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
    [DefaultExecutionOrder(-10)]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class IntegrationTestRunner : UdonSharpBehaviour
    {
        [SerializeField] private bool runTestOnStart = true;
        [SerializeField] private bool logPassedTests = true;

        [SerializeField] IntegrationTestSuite[] integrationTests;

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
            testList = new ReorderableList(serializedObject, serializedObject.FindProperty("integrationTests"), true, true, true, true);
            testList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty testSuiteProperty = testList.serializedProperty.GetArrayElementAtIndex(index);
                IntegrationTestSuite testSuite = (IntegrationTestSuite)testSuiteProperty.objectReferenceValue;

                Rect testFieldRect = new Rect(rect.x, rect.y + 2, rect.width - 20, EditorGUIUtility.singleLineHeight);
                Rect checkBoxRect = new Rect(rect.x + rect.width - 15, rect.y + 2, 20, EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(testFieldRect, testSuiteProperty, label: new GUIContent());

                if (testSuite)
                {
                    EditorGUI.BeginChangeCheck();
                    bool newEnabledState = EditorGUI.Toggle(checkBoxRect, testSuite.runSuiteTests);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(testSuite, "Changed test suite enabled");

                        testSuite.runSuiteTests = newEnabledState;
                    }
                }

                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

                Rect progressBarRect = new Rect(testFieldRect.x, testFieldRect.y + testFieldRect.height + 2, testFieldRect.width + 20, 23f);

                if (EditorApplication.isPlaying)
                {
                    if (testSuite != null)
                    {
                        if (testSuite.runSuiteTests)
                        {
                            int totalTestCount = testSuite.GetTotalTestCount();
                            int passedTestCount = testSuite.GetSucceededTestCount();

                            float percentPassed = totalTestCount > 0 ? (passedTestCount / (float)totalTestCount) : 0f;

                            EditorGUI.ProgressBar(progressBarRect, percentPassed, $"{passedTestCount}/{totalTestCount}");
                        }
                        else
                            EditorGUI.ProgressBar(progressBarRect, 0f, "Tests disabled");
                    }
                    else
                        EditorGUI.ProgressBar(progressBarRect, 0f, "0/0");
                }
                else
                    EditorGUI.ProgressBar(progressBarRect, 0f, "0/0");

                EditorGUI.EndDisabledGroup();
            };
            testList.elementHeightCallback = (int index) =>
            {
                return 45f;
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
