using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DocFX;

namespace VRC.ClientSim.Build
{
    partial class Build
    {
        AbsolutePath DocFxPath = RootDirectory.Parent / "DocFx";
        AbsolutePath DocFxConfigPath => DocFxPath / "docfx.json";
        
        Target DocFxMetadata => _ => _
            .Executes(() =>
            {
                Assert.FileExists(DocFxConfigPath);
                DocFXTasks.DocFX($"metadata {DocFxConfigPath}", DocFxPath);
            });
    }
}