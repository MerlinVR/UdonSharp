
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace UdonSharp
{
    internal class UdonSharpBuildCompile : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 100;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar)
                return true;

            if (UdonSharpSettings.GetSettings()?.disableUploadCompile ?? false)
                return true;

            UdonSharpProgramAsset.CompileAllCsPrograms(true, false);
            UdonSharpEditorCache.SaveAllCache();

            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
            {
                Debug.LogError("[<color=#FF00FF>UdonSharp</color>] Failed to compile UdonSharp scripts for build, check error log for details.");
                UdonSharpUtils.ShowEditorNotification("Failed to compile UdonSharp scripts for build, check error log for details.");
                return false;
            }

            return true;
        }
    }
}
