#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mate.Editor
{
    public static class MateWindowsBuild
    {
        private const string ScenePath = "Assets/_Project/Mate/Scenes/MatePhase1.unity";
        private const string BuildFolderName = "Builds/MateDesktop";
        private const string BuildExeName = "MateDesktop.exe";

        public static string BuildDirectory
        {
            get
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.GetFullPath(Path.Combine(projectRoot, "..", BuildFolderName));
            }
        }

        public static string BuildPath => Path.Combine(BuildDirectory, BuildExeName);

        [MenuItem("Mate/Phase 1/Build Windows Desktop Player")]
        public static void BuildDevelopment()
        {
            Directory.CreateDirectory(BuildDirectory);

            MatePhase1SceneBuilder.CreateOrOpenScene();
            EditorSceneManager.SaveOpenScenes();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.useFlipModelSwapchain = false;
            ConfigureWindowsGraphicsApi(BuildTarget.StandaloneWindows);
            ConfigureWindowsGraphicsApi(BuildTarget.StandaloneWindows64);
            AssetDatabase.SaveAssets();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Mate Windows desktop build succeeded: {BuildPath} ({summary.totalSize} bytes)");
            }
            else
            {
                Debug.LogError($"Mate Windows desktop build failed: {summary.result}");
            }
        }

        private static void ConfigureWindowsGraphicsApi(BuildTarget target)
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
            Debug.Log($"Mate Windows desktop graphics API for {target}: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(target))}");
        }
    }

    [InitializeOnLoad]
    internal static class MateEditorCommandRunner
    {
        private const string BuildRequestFileName = "MateWindowsDesktopBuild.request";
        private static bool _isRunningBuild;

        static MateEditorCommandRunner()
        {
            EditorApplication.update += TryRunPendingBuild;
        }

        private static void TryRunPendingBuild()
        {
            if (_isRunningBuild || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var requestPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp")), BuildRequestFileName);
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);
            _isRunningBuild = true;
            try
            {
                MateWindowsBuild.BuildDevelopment();
            }
            finally
            {
                _isRunningBuild = false;
            }
        }
    }
}
#endif
