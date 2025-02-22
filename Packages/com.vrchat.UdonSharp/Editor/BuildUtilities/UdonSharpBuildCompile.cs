
using UdonSharp;
using UdonSharp.Compiler;
using VRC.SDKBase.Editor.BuildPipeline;

namespace UdonSharpEditor
{
    internal class UdonSharpBuildCompile : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 100;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar)
                return true;

            if (UdonSharpSettings.GetSettings().disableUploadCompile)
                return true;

            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions() { IsEditorBuild = false });
            UdonSharpEditorCache.SaveAllCache();

            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
            {
                UdonSharpUtils.LogError("Failed to compile UdonSharp scripts for build, check error log for details.");
                UdonSharpUtils.ShowEditorNotification("Failed to compile UdonSharp scripts for build, check error log for details.");
                return false;
            }

            if (UdonSharpEditorManager.RunAllUpgrades())
            {
                UdonSharpUtils.LogWarning(UdonSharpEditorManager.UPGRADE_MESSAGE);
                return false;
            }

            return true;
        }
    }
}
