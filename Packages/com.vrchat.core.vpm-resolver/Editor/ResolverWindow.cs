using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Core.Types.Packages;

namespace VRC.PackageManagement.Resolver
{
    [InitializeOnLoad]
    public class ResolverWindow : EditorWindow
    {
        // VisualElements
        private VisualElement _rootView;
        private Button _refreshButton;
        private Button _createButton;
        private Button _resolveButton;
        private Box _manifestInfo;
        private TextElement _manifestReadout;
        private static string _projectDir;
        private const string _projectLoadedKey = "PROJECT_LOADED";

        static ResolverWindow()
        {
            try
            {
                // This library will be in something like  C:\ProjectName\Library\ScriptAssemblies\com.vrchat.tools.vpm-resolver.Editor.dll
                _projectDir = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent.Parent.Parent.FullName;

                if (VPMProjectManifest.ResolveIsNeeded(_projectDir))
                {
                    bool projectIsReady = SessionState.GetBool(_projectLoadedKey, false);
                    string cancelMessage = projectIsReady ? "Show Me the Details" : "Not yet";
                    string cancelInfo = projectIsReady
                        ? "" : "\n\nPress \"Not Yet\" to skip for now. You can open this window later from the menu via\n\nVRChat SDK > Utilities > VPM Resolver";
                    var result = EditorUtility.DisplayDialog("VRChat Package Management",
                        $"This project requires some VRChat Packages which are not in the project yet.\n\nPress OK to download and install them.{cancelInfo}",
                        "OK", cancelMessage);
                    if (result)
                    {
                        ResolveStatic(_projectDir);
                    }
                    else
                    {
                        // Don't try to open window while project is still opening
                        if (!SessionState.GetBool(_projectLoadedKey, false))
                        {
                            SessionState.SetBool(_projectLoadedKey, true);
                            return;
                        }
                        ShowWindow();
                    }
                }
            }
            catch (Exception)
            {
                // Unity says we can't open windows from this function so it throws an exception but also works fine.
            }
        }

        [MenuItem("VRChat SDK/Utilities/VPM Resolver")]
        public static void ShowWindow()
        {
            ResolverWindow wnd = GetWindow<ResolverWindow>();
            wnd.titleContent = new GUIContent("VPM Resolver");
        }

        private void Refresh()
        {
            if (_rootView == null) return;

            bool needsResolve = VPMProjectManifest.ResolveIsNeeded(_projectDir);
            string resolveStatus = needsResolve ? "Please press  \"Resolve\" to Download them." : "All of them are in the project.";
            
            // check for vpm dependencies
            if (!VPMManifestExists())
            {
                _manifestReadout.text = "No VPM Manifest";
            }
            else
            {
                var manifest = VPMProjectManifest.Load(_projectDir);
                var project = new UnityProject(_projectDir);
                StringBuilder readout = new StringBuilder();
                // Here is where we detect if all dependencies are installed
                var allDependencies = manifest.dependencies.Union(manifest.locked);
                foreach (var pair in allDependencies)
                {
                    var id = pair.Key;
                    var version = pair.Value.version;
                    if (project.VPMProvider.GetPackage(id, version) == null)
                    {
                        readout.AppendLine($"{id} {version}: MISSING");
                    }
                    else
                    {
                        readout.AppendLine($"{id} {version}: GOOD");
                    }
                }

                _manifestReadout.text = readout.ToString();

            }
            _resolveButton.SetEnabled(needsResolve);
        }

        private bool VPMManifestExists()
        {
            return VPMProjectManifest.Exists(_projectDir, out _);
        }

        private void CreateManifest()
        {
            VPMProjectManifest.Load(_projectDir);
            Refresh();
        }
        
        private void ResolveManifest()
        {
            ResolveStatic(_projectDir);
        }

        private static void ResolveStatic(string dir)
        {
            EditorUtility.DisplayProgressBar($"Getting all VRChat Packages", "Downloading and Installing...", 0.5f);
            VPMProjectManifest.Resolve(_projectDir);
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Unity calls the CreateGUI method automatically when the window needs to display
        /// </summary>
        private void CreateGUI()
        {
            _rootView = rootVisualElement;
            _rootView.name = "root-view";
            _rootView.styleSheets.Add((StyleSheet) Resources.Load("ResolverWindowStyle"));

            // Main Container
            var container = new Box()
            {
                name = "buttons"
            };
            _rootView.Add(container);

            // Create Button
            if (!VPMManifestExists())
            {
                _createButton = new Button(CreateManifest)
                {
                    text = "Create",
                    name = "create-button-base"
                };
                container.Add(_createButton);
            }
            else
            {
                _resolveButton = new Button(ResolveManifest)
                {
                    text = "Resolve",
                    name = "resolve-button-base"
                };
                container.Add(_resolveButton);
            }

            // Manifest Info
            _manifestInfo = new Box()
            {
                name = "manifest-info",
            };
            _manifestInfo.Add(new Label("Required Packages"){name = "manifest-header"});
            _manifestReadout = new TextElement();
            _manifestInfo.Add(_manifestReadout);
            
            _rootView.Add(_manifestInfo);
            
            // Refresh Button
            var refreshBox = new Box();
            _refreshButton = new Button(Refresh)
            {
                text = "Refresh",
                name = "refresh-button-base"
            };
            refreshBox.Add(_refreshButton);
            _rootView.Add(refreshBox);
            
            Refresh();
        }
    }

}