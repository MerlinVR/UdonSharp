using Nuke.Common;
using Nuke.Common.IO;

namespace VRC.ClientSim.Build
{
    partial class Build : NukeBuild
    {
        AbsolutePath DocsRoot = RootDirectory.Parent.Parent / "Docs";
        AbsolutePath DocsSource => DocsRoot / "Source";
        AbsolutePath DocsPublished => DocsRoot / "Published";
        AbsolutePath DocsImages => DocsSource / "images";

        public static int Main () => Execute<Build>(x => x.DocusaurusBuild);

        [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
        readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    }
}