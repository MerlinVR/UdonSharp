using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor;
using VRC.Udon.Graph;
using UdonSharp;

public class APITestGUI : EditorWindow
{
    string nodeDefSearch = "";

    string functionSearch = "";

    [MenuItem("Window/Merlin/Udon API Tester")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(APITestGUI));
    }

    private void OnGUI()
    {
        nodeDefSearch = EditorGUILayout.TextField("Node Definition Query", nodeDefSearch);

        if (GUILayout.Button("Query Node Definitions") == true)
        {
            OnSearchNodeDefinitions();
        }

        Vector3 test = Vector2.zero;

        test.x = test.y;
        test.x = test.x + test.y;

        if (GUILayout.Button("Test Type members"))
        {
            foreach (MemberInfo info in typeof(Vector2).GetMembers())
            {
                Debug.Log(info.ToString());
            }
        }

        if (GUILayout.Button("Get Type Member CIL"))
        {
            ProbeCILOutput();
        }

        //functionSearch = EditorGUILayout.TextField("Search method name", functionSearch);

        if (GUILayout.Button("Lookup function"))
        {
            LookupFunction(); 
        }
    }

    private void OnSearchNodeDefinitions()
    {
        IEnumerable<UdonNodeDefinition> nodeDefinitions = VRC.Udon.Editor.UdonEditorManager.Instance.GetNodeDefinitions("").Where(e => e.fullName.Contains(nodeDefSearch)).OrderBy(e => e.fullName);
        //IEnumerable<UdonNodeDefinition> nodeDefinitions = VRC.Udon.Editor.UdonEditorManager.Instance.GetNodeDefinitions(nodeDefSearch).OrderBy(e => e.fullName);

        foreach (UdonNodeDefinition definition in nodeDefinitions)
        {
            if (definition != null) 
                Debug.Log(definition.fullName /*+ (definition.type == null ? " --- null type" : (" --- " + definition.type.FullName))*/);
        }
    }

    private void TestAB(float a, int b)
    {
        Debug.Log("int");
    }

    private void TestAB(double a, float b)
    {
        Debug.Log("float");
    }

    private void TestAB(double a, float b, string hahaDefaultArg = "")
    {
        Debug.Log("float");
    }

    private void TestAB(double a, double b)
    {
        Debug.Log("double");
    }

    private void TestAB(double a, double b, string hahaDefaultArg = "")
    {
        Debug.Log("double");
    }

    private void ProbeCILOutput()
    {
        Debug.Log(UdonSharpUtils.GetNumericConversionMethod(typeof(uint), typeof(Vector2)));
        

        //ResolverContext resolverContext = new ResolverContext();

        //var args = new List<System.Type>();
        //args.Add(typeof(string));
        //args.Add(typeof(ulong));
        //args.Add(typeof(ulong));
        //args.Add(typeof(ulong));

        //MethodInfo foundExactMatch = resolverContext.FindBestOverloadFunction(typeof(Debug)
        //    .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(e => e.Name == "LogFormat").ToArray(), args);

        //if (foundExactMatch != null)
        //{
        //    Debug.Log($"Exact match {foundExactMatch}");
        //}

        //foreach (MethodInfo info in typeof(Vector2).GetMembers())
        //{
        //    Debug.Log(info.GetMethodBody().GetILAsByteArray());
        //}

        //Debug.Log(typeof(float).IsAssignableFrom(typeof(int)));


        foreach (MemberInfo methodInfo in typeof(string).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)/*.Where(e => e.Name.Contains("op_"))*/)
        {
            Debug.Log(methodInfo);
        }

        //foreach (MemberInfo methodInfo in typeof(System.Convert).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        //{
        //    Debug.Log(methodInfo.Name);
        //}
    }

    private void LookupFunction()
    {
        ResolverContext resolverContext = new ResolverContext();
        resolverContext.AddNamespace("UnityEngine");

        //Debug.Log(resolverContext.ResolveStaticMethod("UnityEngine.Vector3.Dot", new string[] { "Vector3", "Vector3", "float" }));
        //Debug.Log(resolverContext.GetUdonMethodName(typeof(Vector3).GetProperty("up").GetMethod));

        VRC.SDKBase.VRCPlayerApi playerapi = new VRC.SDKBase.VRCPlayerApi();

        //playerapi.GetPlayersWithTag()

        //Debug.Log(resolverContext.GetUdonMethodName(typeof(VRC.SDKBase.VRCPlayerApi).GetMethod("GetPlayersWithTag")));

        //MethodInfo[] methodInfos = typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "GetComponentsInChildren").ToArray();
        //MethodInfo[] methodInfos = typeof(TrailRenderer).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "GetMaterials").ToArray();
        //FieldInfo[] methodInfos = typeof(Color).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(e => e.Name == "r").ToArray();
        MethodInfo[] methodInfos = typeof(Physics).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(e => e.Name == "CapsuleCast").ToArray();

        foreach (MethodInfo info in methodInfos)
        {
            Debug.Log(resolverContext.GetUdonMethodName(info));
        }
    }
}
