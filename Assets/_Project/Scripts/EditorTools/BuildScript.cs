using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StrategyDemo.EditorTools
{
    /// <summary>
    /// Produces the Windows standalone player from the scenes enabled in Build Settings.
    /// Entry point for the editor menu and for batchmode (<c>-executeMethod StrategyDemo.EditorTools.BuildScript.BuildWindows</c>).
    /// </summary>
    public static class BuildScript
    {
        /// <summary>Folder, relative to the project root, that receives the Windows player.</summary>
        public const string OutputDirectory = "Builds/Windows";

        private const string MenuPath = "Tools/Build/Windows";
        private const string ExecutableExtension = ".exe";
        private const double BytesPerMegabyte = 1024d * 1024d;

        /// <summary>Builds the Windows 64-bit player into <see cref="OutputDirectory"/> and logs the result.</summary>
        [MenuItem(MenuPath)]
        public static void BuildWindows()
        {
            bool succeeded;
            try
            {
                BuildPlayerOptions options = CreateWindowsOptions(EditorBuildSettings.scenes, PlayerSettings.productName);
                // Unity overwrites but never removes files, so a stale DLL from an older build could ship.
                FileUtil.DeleteFileOrDirectory(OutputDirectory);
                succeeded = Build(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                succeeded = false;
            }

            // Batchmode callers check the process exit code, not the log.
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        /// <summary>
        /// Creates the build options for a Windows 64-bit player containing only the enabled scenes.
        /// Kept free of editor state so it can be tested without touching Build Settings.
        /// </summary>
        /// <param name="buildScenes">Scenes as listed in Build Settings; disabled entries are skipped.</param>
        /// <param name="productName">Player product name, used as the executable name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="buildScenes"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="productName"/> is empty, whitespace or not a valid file name.</exception>
        /// <exception cref="InvalidOperationException">No scene is enabled, so there is nothing to build.</exception>
        public static BuildPlayerOptions CreateWindowsOptions(EditorBuildSettingsScene[] buildScenes, string productName)
        {
            if (buildScenes == null)
            {
                throw new ArgumentNullException(nameof(buildScenes));
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                throw new ArgumentException("A product name is required to name the executable.", nameof(productName));
            }

            if (productName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"Product name '{productName}' contains characters not allowed in a file name.", nameof(productName));
            }

            string[] scenePaths = buildScenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenePaths.Length == 0)
            {
                throw new InvalidOperationException("No scene is enabled in Build Settings; nothing to build.");
            }

            return new BuildPlayerOptions
            {
                scenes = scenePaths,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                // Forward slash on purpose: Path.Combine would mix separators on Windows.
                locationPathName = $"{OutputDirectory}/{productName}{ExecutableExtension}",
                options = BuildOptions.None
            };
        }

        private static bool Build(BuildPlayerOptions options)
        {
            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[BuildScript] Windows build {summary.result}: {summary.totalErrors} error(s). See the build report above.");
                return false;
            }

            Debug.Log($"[BuildScript] Windows build succeeded: {summary.outputPath} " +
                      $"({summary.totalSize / BytesPerMegabyte:F1} MB, {summary.totalTime:mm\\:ss}).");
            return true;
        }
    }
}
