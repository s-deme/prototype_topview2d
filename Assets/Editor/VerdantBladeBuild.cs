#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VerdantBlade.EditorTools
{
    /// <summary>Repeatable local and CI build entry point for the Windows release.</summary>
    public static class VerdantBladeBuild
    {
        private const string OutputDirectory = "Builds/Windows";
        private const string ExecutableName = "VerdantBlade.exe";
        private const string IconPath = "Assets/Branding/VerdantBladeIcon.png";

        [MenuItem("Verdant Blade/Build Windows Release")]
        public static void BuildWindows64()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                throw new InvalidOperationException("No scenes are enabled in Build Settings.");
            }

            var enabledScenes = Array.FindAll(scenes, scene => scene.enabled);
            if (enabledScenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are available for the build.");
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                throw new InvalidOperationException("Missing release icon: " + IconPath);
            }

            PlayerSettings.bundleVersion = GameManager.ProductVersion;
            var applicationIconSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            var applicationIcons = new Texture2D[applicationIconSizes.Length];
            for (var index = 0; index < applicationIcons.Length; index++)
            {
                applicationIcons[index] = icon;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, applicationIcons, IconKind.Application);
            Directory.CreateDirectory(OutputDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(enabledScenes, scene => scene.path),
                locationPathName = Path.Combine(OutputDirectory, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.StrictMode
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            }

            Debug.Log("[Verdant Blade] Windows build complete: " + report.summary.outputPath);
        }
    }
}
#endif
