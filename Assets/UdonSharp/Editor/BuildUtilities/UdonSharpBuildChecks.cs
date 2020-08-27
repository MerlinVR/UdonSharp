
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace UdonSharp
{
    public class UdonSharpBuildChecks : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => -1;

        /// <summary>
        /// If you're considering commenting any section of this out, try enabling the force compile in the U# settings first.
        /// This is here to prevent you from corrupting your project files.
        /// If scripts are left uncompiled from Unity's side when uploading, there is a chance to corrupt your assemblies which can cause all of your UdonBehaviours to lose their variables if handled wrong.
        /// </summary>
        /// <param name="requestedBuildType"></param>
        /// <returns></returns>
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            UdonSharpSettings settings = UdonSharpSettings.GetSettings();
            bool shouldForceCompile = settings != null && settings.shouldForceCompile;

            // Unity doesn't like this and will throw errors if it ends up compiling scripts. But it seems to work. 
            // This is marked experimental for now since I don't know if it will break horribly in some case.
            if (shouldForceCompile)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            else
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                if (EditorApplication.isCompiling)
                {
                    Debug.LogError("[<color=#FF00FF>UdonSharp</color>] Scripts are in the process of compiling, please retry build after scripts have compiled.");
                    UdonSharpUtils.ShowEditorNotification("Scripts are in the process of compiling, please retry build after scripts have compiled.");
                    return false;
                }
            }
            
            return true;
        }
    }
}
