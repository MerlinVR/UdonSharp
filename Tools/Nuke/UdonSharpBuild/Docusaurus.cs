using System;
using System.IO;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Helpers;
using Markdig.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Nuke.Common.Utilities;
using YamlDotNet.Serialization;

namespace VRC.ClientSim.Build
{
    partial class Build
    {
        public class DocusaurusFrontMatter
        {
            [YamlMember(Alias = "id")]
            public string Id { get; set; }
    
            [YamlMember(Alias = "title")]
            public string Title { get; set; }
            [YamlMember(Alias = "hide_title")]
            public bool HideTitle { get; set; }
        }
        
        AbsolutePath DocusaurusPath = RootDirectory.Parent / "Docusaurus";
        AbsolutePath DocusaurusDocsPath => DocusaurusPath / "docs";
        AbsolutePath DocusaurusImagesPath => DocusaurusPath / "static" / "images";

        const string FrontMatterDivider = "---"; // Can we pull this from one of our libraries?
        Target CleanDocusaurusGenerated => _ => _
            .Executes(() =>
            {
                FileSystemTasks.EnsureCleanDirectory(DocusaurusDocsPath);
            });

        Target CopyImagesToDocusaurus => _ => _
            .Executes(() =>
            {
                FileSystemTasks.EnsureCleanDirectory(DocusaurusImagesPath);
                foreach (var file in DocsImages.GlobFiles("**/**.*"))
                {
                    FileSystemTasks.CopyFileToDirectory(file, DocusaurusImagesPath);
                }
            });
        
        Target TransformDocsForDocusaurus => _ => _
            .DependsOn(CleanDocusaurusGenerated)
            .Executes(() =>
            {
                foreach (var path in DocsSource.GlobFiles("**/**.md"))
                {
                    string fileAsText = File.ReadAllText(path);
                    
                    // Parse text as MarkdownDocument
                    var pipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();
                    var md = Markdown.Parse(fileAsText, pipeline);
                    bool hasFrontMatter = md.Descendants<YamlFrontMatterBlock>().Count() > 0;
                    if (hasFrontMatter)
                    {
                        throw new Exception($"File {path.Name} already has FrontMatter, please remove it.");
                    }

                    // Get first header or skip
                    var headings = md.Descendants<HeadingBlock>();
                    if (headings.Count() == 0)
                    {
                        Serilog.Log.Warning($"Skipping {path} which does not have a header");
                        continue;
                    }
                    var firstHeader = headings.First();

                    // Build Front Matter
                    var frontMatter = new DocusaurusFrontMatter();
                    frontMatter.Id = path.NameWithoutExtension.ToLower().Replace(" ", "-");
                    frontMatter.Title = firstHeader.Inline?.FirstChild?.ToString();
                    frontMatter.HideTitle = true;

                    // Skip if the title is empty
                    if (string.IsNullOrWhiteSpace(frontMatter.Title))
                    {
                        Serilog.Log.Warning($"Title could not be parsed for {path}, Skipping");
                        continue;
                    }
                    
                    // serialize front matter
                    var serialized = new Serializer().Serialize(frontMatter);
                    var newFrontMatterString = new StringBuilder();
                    newFrontMatterString
                        .AppendLine(FrontMatterDivider)
                        .Append(serialized)
                        .AppendLine(FrontMatterDivider);
                    
                    // Create new File Contents with front matter
                    string newFileContents = fileAsText.Insert(0, newFrontMatterString.ToString());
                    
                    // Save new file
                    string newPath = WriteTextToDocusaurusPath(path, newFileContents);
                    
                    Serilog.Log.Information($"Added id {frontMatter.Id} and title {frontMatter.Title} to new doc at {newPath}");
                }
            });

        Target ReorderIntroPage => _ => _
            .TriggeredBy(TransformDocsForDocusaurus)
            .Before(DocusaurusBuild)
            .Executes(() =>
            {
                var introPage = DocusaurusDocsPath / "index.md";
                string contents = File.ReadAllText(introPage);
                var insertIndex = contents.LastIndexOf(FrontMatterDivider, StringComparison.Ordinal);
                contents = contents.Insert(insertIndex, $"sidebar_position: 0{Environment.NewLine}");
                File.WriteAllText(introPage, contents);
            });

        private string WriteTextToDocusaurusPath(string path, string text)
        {
            string pathFromDocsSource = path.Replace($"{DocsSource}", "");
            pathFromDocsSource = pathFromDocsSource.TrimStart("/");
            var newPath = DocusaurusDocsPath / pathFromDocsSource;
            FileSystemTasks.EnsureExistingParentDirectory(newPath);
            File.WriteAllText(newPath, text);
            return newPath;
        }

        Target InstallDocusaurus => _ => _
            .Executes(() =>
            {
                if (IsServerBuild)
                {
                    NpmTasks.NpmCi(s => s.SetProcessWorkingDirectory(DocusaurusPath));
                }
            });

        Target DocusaurusBuild => _ => _
            .DependsOn(CleanDocusaurusGenerated)
            .DependsOn(TransformDocsForDocusaurus)
            .DependsOn(CopyImagesToDocusaurus)
            .DependsOn(InstallDocusaurus)
            .Executes(() =>
            {
                
                NpmTasks.NpmRun(s => s.SetProcessWorkingDirectory(DocusaurusPath).SetCommand("build"));
            });
    }
}