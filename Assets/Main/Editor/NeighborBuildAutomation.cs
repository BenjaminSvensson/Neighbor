#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class NeighborBuildAutomation
{
    internal const string PrimaryScenePath = "Assets/Main/Scenes/Main/TrueTreeHouse/TrueTreeHouse.unity";
    private const string DefaultValidationBuildPath = "Builds/Validation/NeighborValidation.exe";
    private const string BuildPathArgument = "-neighborBuildPath";

    [MenuItem("Tools/Neighbor/Validate Build Readiness")]
    private static void ValidateBuildReadinessFromMenu()
    {
        IReadOnlyList<string> issues = NeighborBuildReadinessValidator.CollectIssues();
        if (issues.Count == 0)
        {
            Debug.Log("Neighbor build readiness passed: product metadata, primary scene, display defaults, and player configuration are valid.");
            return;
        }

        for (int i = 0; i < issues.Count; i++)
        {
            Debug.LogError(issues[i]);
        }
    }

    [MenuItem("Tools/Neighbor/Build Windows Validation Player")]
    private static void BuildWindows64DevelopmentFromMenu()
    {
        BuildWindows64Development(DefaultValidationBuildPath);
    }

    public static void BuildWindows64DevelopmentFromCommandLine()
    {
        try
        {
            string outputPath = GetCommandLineValue(BuildPathArgument) ?? DefaultValidationBuildPath;
            BuildWindows64Development(outputPath);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    internal static BuildReport BuildWindows64Development(string outputPath)
    {
        IReadOnlyList<string> issues = NeighborBuildReadinessValidator.CollectIssues();
        if (issues.Count > 0)
        {
            throw new BuildFailedException("Neighbor build readiness failed:\n- " + string.Join("\n- ", issues));
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        BuildPlayerOptions options = new()
        {
            scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray(),
            locationPathName = fullOutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.StrictMode
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Windows validation build ended with {report.summary.result}: "
                + $"{report.summary.totalErrors} error(s), {report.summary.totalWarnings} warning(s).");
        }

        Debug.Log(
            $"Windows validation build succeeded at '{fullOutputPath}' "
            + $"({report.summary.totalSize / (1024f * 1024f):0.0} MiB, {report.summary.totalTime}).");
        return report;
    }

    private static string GetCommandLineValue(string argumentName)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[i + 1];
            }
        }

        return null;
    }
}

internal static class NeighborBuildReadinessValidator
{
    internal static IReadOnlyList<string> CollectIssues()
    {
        List<string> issues = new();
        ValidateScenes(issues);
        ValidateProductMetadata(issues);
        ValidateDisplayDefaults(issues);
        return issues;
    }

    private static void ValidateScenes(List<string> issues)
    {
        EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .ToArray();
        if (enabledScenes.Length == 0)
        {
            issues.Add("Build Settings has no enabled scenes.");
            return;
        }

        if (!string.Equals(enabledScenes[0].path, NeighborBuildAutomation.PrimaryScenePath, StringComparison.Ordinal))
        {
            issues.Add(
                $"The first enabled build scene must be '{NeighborBuildAutomation.PrimaryScenePath}', "
                + $"but is '{enabledScenes[0].path}'.");
        }

        for (int i = 0; i < enabledScenes.Length; i++)
        {
            string path = enabledScenes[i].path;
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                issues.Add($"Enabled build scene {i} is missing or invalid: '{path}'.");
            }
        }
    }

    private static void ValidateProductMetadata(List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName)
            || string.Equals(PlayerSettings.companyName, "DefaultCompany", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("Player Settings still uses the Unity template company name.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.productName))
        {
            issues.Add("Player Settings has no product name.");
        }

        string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
        if (string.IsNullOrWhiteSpace(identifier)
            || identifier.Contains("UnityTechnologies", StringComparison.OrdinalIgnoreCase)
            || identifier.Contains("template", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Standalone application identifier is missing or still template-derived: '{identifier}'.");
        }
    }

    private static void ValidateDisplayDefaults(List<string> issues)
    {
        if (PlayerSettings.defaultScreenWidth < 1280 || PlayerSettings.defaultScreenHeight < 720)
        {
            issues.Add(
                $"Default resolution {PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight} "
                + "is below the supported 1280x720 baseline.");
        }

        if (!PlayerSettings.resizableWindow)
        {
            issues.Add("Standalone window resizing is disabled.");
        }

        if (PlayerSettings.colorSpace != ColorSpace.Linear)
        {
            issues.Add("Player color space must be Linear for the configured URP lighting pipeline.");
        }
    }
}

internal sealed class NeighborBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        IReadOnlyList<string> issues = NeighborBuildReadinessValidator.CollectIssues();
        if (issues.Count > 0)
        {
            throw new BuildFailedException("Neighbor build readiness failed:\n- " + string.Join("\n- ", issues));
        }
    }
}
#endif
