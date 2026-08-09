using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CIBuilder
{
    public static void BuildWindows()
    {
        Build(
            BuildTarget.StandaloneWindows64,
            "Build/Windows/ModdingEditor.exe"
        );
    }

    public static void BuildMacOS()
    {
        Build(
            BuildTarget.StandaloneOSX,
            "Build/macOS/ModdingEditor.app"
        );
    }

    private static void Build(BuildTarget target, string outputPath)
    {
        Debug.Log("==========================================");
        Debug.Log($"CI BUILD START: {target}");
        Debug.Log("==========================================");

        // Use all enabled scenes from Build Settings.
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new Exception(
                "No enabled scenes were found in Build Settings."
            );
        }

        // GitHub tag -> Unity bundle version
        // Example: v2.2.1 -> 2.2.1
        string releaseVersion =
            Environment.GetEnvironmentVariable("RELEASE_VERSION");

        if (!string.IsNullOrWhiteSpace(releaseVersion))
        {
            releaseVersion =
                releaseVersion.TrimStart('v', 'V');

            PlayerSettings.bundleVersion = releaseVersion;

            Debug.Log($"Release version: {releaseVersion}");
        }

        string directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        };

        Debug.Log($"Build Target: {target}");
        Debug.Log($"Output: {outputPath}");

        BuildReport report = BuildPipeline.BuildPlayer(options);

        BuildSummary summary = report.summary;

        Debug.Log($"Result: {summary.result}");
        Debug.Log($"Size: {summary.totalSize}");
        Debug.Log($"Time: {summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception(
                $"Unity build failed: {summary.result}"
            );
        }

        Debug.Log("==========================================");
        Debug.Log("CI BUILD SUCCESS");
        Debug.Log("==========================================");
    }
}