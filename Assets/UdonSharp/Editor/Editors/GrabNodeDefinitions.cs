#if UNITY_EDITOR && UDONSHARP_DEBUG

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor;

public class GrabNodeDefinitions : EditorWindow
{
    [MenuItem("Window/Udon Sharp/Node Definition Grabber")]
    static void Init()
    {
        GrabNodeDefinitions window = GetWindow<GrabNodeDefinitions>(false, "Node Definition Grabber");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Get Node Names"))
        {
            IEnumerable<string> nodeNames = UdonEditorManager.Instance.GetNodeDefinitions().Select(e => e.fullName).OrderBy(e => e);
            EditorGUIUtility.systemCopyBuffer = string.Join("\n", nodeNames);
        }
    }
}

#endif
