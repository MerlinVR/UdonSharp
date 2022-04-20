using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace VRC.Udon.Editor {
    [InitializeOnLoad]
    public class UPMImporter
    {
        // Add packages here to auto-import
        public static string[] requiredPackages =
        {
            #if UNITY_2019_3_OR_NEWER
            "com.unity.cinemachine@2.8.0",
            "com.unity.postprocessing@3.1.1",
            "com.unity.textmeshpro@2.1.6",
            "com.unity.modules.androidjni@1.0.0",
            "com.unity.timeline@1.2.18",
            "com.unity.ugui@1.0.0",
            "com.unity.test-framework@1.1.27",
            "com.unity.package-manager-ui@2.2.0",
            #else
            "com.unity.cinemachine@2.6.1",
            "com.unity.postprocessing@3.0.3",
            "com.unity.textmeshpro@1.5.1",
            #endif
        };
        
        private static ListRequest list;
        
        static UPMImporter()
        {
            list = Client.List();
            EditorApplication.update += Update;
        }

        public static void Update()
        {
            // Exit early if we're still gathering the list
            if (!list.IsCompleted) return;
            
            // Unsubscribe from Update once the list is ready
            EditorApplication.update -= Update;

            var localPackages = list.Result;
            bool importedNewPackage = false;
            foreach (string packageName in requiredPackages)
            {
                if(localPackages.All(p => $"{p.name}@{p.version}" != packageName))
                {
                    Install(packageName);
                    importedNewPackage = true;
                }
            }
            
            // if Unity tried to import SDK3 before required packages, it will have old errors showing.
            //if(importedNewPackage) ClearLog();
        }

        public static bool Install(string id)
        {
            var request = Client.Add(id);
            while (!request.IsCompleted) {};
            if(request.Error != null)Debug.LogError(request.Error.message);
            return request.Error == null;
        }
        
        public static void ClearLog()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));

            System.Type type = assembly.GetType("UnityEditor.LogEntries");
            System.Reflection.MethodInfo method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }

    }

}
