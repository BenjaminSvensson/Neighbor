#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class ProjectStateParityValidator
{
    private const string ValidateMenuPath = "Tools/Neighbor/Validate Project State Parity";
    private const string ExportHashManifestMenuPath = "Tools/Neighbor/Export Project State Hash Manifest";
    private const string DefaultHashManifestPath = "Logs/NeighborProjectStateHashes.txt";

    private static readonly string[] BinaryProjectStateGitPatterns =
    {
        "Assets/New Terrain*.asset",
        "Assets/Main/Art/Terrain/Data/*.asset",
        "Assets/Main/Art/Terrain/Data/**/*.asset",
        "Assets/**/NavMesh*.asset"
    };

    private static readonly string[] HashManifestGitPatterns =
    {
        "Assets/**/*.unity",
        "Assets/New Terrain*.asset",
        "Assets/Main/Art/Terrain/Data/*.asset",
        "Assets/Main/Art/Terrain/Data/**/*.asset",
        "Assets/**/NavMesh*.asset",
        "Assets/**/*.terrainlayer",
        "Assets/Main/Art/Terrain/**/*.png",
        "Assets/Main/Art/Terrain/**/*.jpg",
        "Assets/Main/Art/Terrain/**/*.jpeg",
        "Assets/Main/Art/Terrain/**/*.tif",
        "Assets/Main/Art/Terrain/**/*.tiff",
        "Assets/**/*.mat"
    };

    private static readonly string[] TerrainImageExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff"
    };

    [MenuItem(ValidateMenuPath)]
    private static void ValidateFromMenu()
    {
        IReadOnlyList<string> issues = ValidateProjectState(true);
        IReadOnlyList<string> stashWarnings = CollectProjectStateStashWarnings();
        if (issues.Count == 0)
        {
            Debug.Log("Project state parity validation passed. Unity metas, binary project-state LFS coverage, and content hash inputs are ready.");
        }

        for (int i = 0; i < issues.Count; i++)
        {
            Debug.LogError(issues[i]);
        }

        LogStashWarnings(stashWarnings);
    }

    public static void ValidateFromCommandLine()
    {
        IReadOnlyList<string> issues = ValidateProjectState(true);
        IReadOnlyList<string> stashWarnings = CollectProjectStateStashWarnings();
        for (int i = 0; i < issues.Count; i++)
        {
            Debug.LogError(issues[i]);
        }

        if (issues.Count == 0)
        {
            Debug.Log("Project state parity validation passed.");
        }

        LogStashWarnings(stashWarnings);
        EditorApplication.Exit(issues.Count == 0 ? 0 : 1);
    }

    [MenuItem(ExportHashManifestMenuPath)]
    private static void ExportHashManifestFromMenu()
    {
        string manifestPath = ExportHashManifest(DefaultHashManifestPath);
        Debug.Log($"Wrote project state hash manifest to '{manifestPath}'.");
    }

    public static void ExportHashManifestFromCommandLine()
    {
        string manifestPath = ExportHashManifest(DefaultHashManifestPath);
        Debug.Log($"Wrote project state hash manifest to '{manifestPath}'.");
        EditorApplication.Exit(0);
    }

    internal static IReadOnlyList<string> ValidateProjectState(bool includeGitChecks)
    {
        List<string> issues = new();
        ValidateMetaPairs(issues);
        if (includeGitChecks)
        {
            ValidateBinaryProjectStateLfsCoverage(issues);
            ValidateHashManifestInputs(issues);
        }

        return issues;
    }

    internal static IReadOnlyList<string> CollectProjectStateStashWarningsForTests(
        string[] stashListLines,
        Func<string, string[]> getStashPaths)
    {
        return CollectProjectStateStashWarnings(stashListLines, getStashPaths);
    }

    internal static string ExportHashManifest(string outputPath)
    {
        string rootedOutputPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(rootedOutputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        List<string> paths = GetTrackedProjectStateHashPaths();
        using SHA256 sha256 = SHA256.Create();
        using StreamWriter writer = new(rootedOutputPath, false, new UTF8Encoding(false));
        writer.WriteLine("# Neighbor project-state hash manifest");
        writer.WriteLine("# Compare this file between PCs after git lfs pull + git lfs checkout.");
        writer.WriteLine("# GeneratedUtc: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteLine("# Format: SHA256  bytes  path");

        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            byte[] hash = ComputeHash(sha256, fullPath);
            long length = new FileInfo(fullPath).Length;
            writer.WriteLine($"{FormatHash(hash)}  {length}  {NormalizePath(path)}");
        }

        return rootedOutputPath;
    }

    private static void ValidateMetaPairs(List<string> issues)
    {
        foreach (string assetPath in EnumerateAssetDatabasePaths("Assets"))
        {
            string metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                issues.Add($"Missing Unity meta file for '{NormalizePath(assetPath)}'.");
            }
        }

        foreach (string metaPath in Directory.EnumerateFiles("Assets", "*.meta", SearchOption.AllDirectories))
        {
            string targetPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
            if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
            {
                issues.Add($"Orphan Unity meta file without matching asset or folder: '{NormalizePath(metaPath)}'.");
            }
        }
    }

    private static void ValidateBinaryProjectStateLfsCoverage(List<string> issues)
    {
        List<string> paths = GetTrackedPaths(BinaryProjectStateGitPatterns);
        if (paths.Count == 0)
        {
            issues.Add("No tracked binary terrain/NavMesh project-state assets were found for LFS validation.");
            return;
        }

        HashSet<string> lfsPaths = new(GetGitOutputLines("lfs", "ls-files", "--name-only").Select(NormalizePath), StringComparer.Ordinal);
        for (int i = 0; i < paths.Count; i++)
        {
            string path = NormalizePath(paths[i]);
            GitAttributes attributes = GetGitAttributes(path);
            if (!string.Equals(attributes.Filter, "lfs", StringComparison.Ordinal)
                || !string.Equals(attributes.Diff, "lfs", StringComparison.Ordinal)
                || !string.Equals(attributes.Merge, "lfs", StringComparison.Ordinal)
                || !string.Equals(attributes.Text, "unset", StringComparison.Ordinal))
            {
                issues.Add($"Binary project-state asset is not covered by the LFS binary rule: '{path}'.");
            }

            if (!lfsPaths.Contains(path))
            {
                issues.Add($"Binary project-state asset is not currently tracked by Git LFS: '{path}'. Run git add --renormalize for this asset after fixing .gitattributes.");
            }
        }
    }

    private static void ValidateHashManifestInputs(List<string> issues)
    {
        List<string> hashPaths = GetTrackedProjectStateHashPaths();
        if (hashPaths.Count == 0)
        {
            issues.Add("No tracked project-state files were found for hash manifest generation.");
            return;
        }

        for (int i = 0; i < hashPaths.Count; i++)
        {
            string path = hashPaths[i];
            if (!File.Exists(path))
            {
                issues.Add($"Tracked project-state file is missing from disk: '{NormalizePath(path)}'.");
            }
        }
    }

    private static IReadOnlyList<string> CollectProjectStateStashWarnings()
    {
        try
        {
            string[] stashListLines = GetGitOutputLines("stash", "list", "--format=%gd:%gs");
            return CollectProjectStateStashWarnings(
                stashListLines,
                stashReference => GetGitOutputLines("stash", "show", "--name-only", "--format=", stashReference));
        }
        catch (Exception exception)
        {
            return new[] { $"Could not inspect git stash project-state contents: {exception.Message}" };
        }
    }

    private static IReadOnlyList<string> CollectProjectStateStashWarnings(
        string[] stashListLines,
        Func<string, string[]> getStashPaths)
    {
        List<string> warnings = new();
        if (stashListLines == null || getStashPaths == null)
        {
            return warnings;
        }

        for (int i = 0; i < stashListLines.Length; i++)
        {
            string stashReference = ParseStashReference(stashListLines[i]);
            if (string.IsNullOrWhiteSpace(stashReference))
            {
                continue;
            }

            string[] paths = getStashPaths(stashReference) ?? Array.Empty<string>();
            List<string> projectStatePaths = paths
                .Select(NormalizePath)
                .Where(IsProjectStateStashPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (projectStatePaths.Count == 0)
            {
                continue;
            }

            warnings.Add(
                $"Git stash '{stashReference}' contains Unity project-state files that may not be published: {string.Join(", ", projectStatePaths)}.");
        }

        return warnings;
    }

    private static string ParseStashReference(string stashListLine)
    {
        if (string.IsNullOrWhiteSpace(stashListLine))
        {
            return string.Empty;
        }

        int separator = stashListLine.IndexOf(':');
        return separator > 0 ? stashListLine.Substring(0, separator).Trim() : stashListLine.Trim();
    }

    private static bool IsProjectStateStashPath(string path)
    {
        path = NormalizePath(path);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        string lowerPath = path.ToLowerInvariant();
        if (lowerPath.EndsWith(".unity", StringComparison.Ordinal)
            || lowerPath.EndsWith(".unity.meta", StringComparison.Ordinal)
            || lowerPath.EndsWith(".terrainlayer", StringComparison.Ordinal)
            || lowerPath.EndsWith(".terrainlayer.meta", StringComparison.Ordinal)
            || lowerPath.EndsWith(".mat", StringComparison.Ordinal)
            || lowerPath.EndsWith(".mat.meta", StringComparison.Ordinal))
        {
            return true;
        }

        if ((lowerPath.EndsWith(".asset", StringComparison.Ordinal) || lowerPath.EndsWith(".asset.meta", StringComparison.Ordinal))
            && (path.StartsWith("Assets/New Terrain", StringComparison.Ordinal)
                || path.StartsWith("Assets/Main/Art/Terrain/Data/", StringComparison.Ordinal)
                || path.IndexOf("/NavMesh", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return true;
        }

        if (path.StartsWith("Assets/Main/Art/Terrain/", StringComparison.Ordinal))
        {
            for (int i = 0; i < TerrainImageExtensions.Length; i++)
            {
                string extension = TerrainImageExtensions[i];
                if (lowerPath.EndsWith(extension, StringComparison.Ordinal)
                    || lowerPath.EndsWith(extension + ".meta", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return path.IndexOf("/Materials/", StringComparison.OrdinalIgnoreCase) >= 0
            && (lowerPath.EndsWith(".mat", StringComparison.Ordinal)
                || lowerPath.EndsWith(".mat.meta", StringComparison.Ordinal));
    }

    private static void LogStashWarnings(IReadOnlyList<string> warnings)
    {
        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning(warnings[i]);
        }
    }

    private static List<string> GetTrackedProjectStateHashPaths()
    {
        return GetTrackedPaths(HashManifestGitPatterns);
    }

    private static List<string> GetTrackedPaths(IEnumerable<string> patterns)
    {
        List<string> arguments = new() { "ls-files" };
        arguments.AddRange(patterns);
        return GetGitOutputLines(arguments.ToArray())
            .Select(NormalizePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static GitAttributes GetGitAttributes(string path)
    {
        string[] lines = GetGitOutputLines("check-attr", "filter", "diff", "merge", "text", "--", path);
        GitAttributes attributes = default;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int attributeSeparator = line.IndexOf(": ", StringComparison.Ordinal);
            if (attributeSeparator < 0)
            {
                continue;
            }

            int valueSeparator = line.IndexOf(": ", attributeSeparator + 2, StringComparison.Ordinal);
            if (valueSeparator < 0)
            {
                continue;
            }

            string attribute = line.Substring(attributeSeparator + 2, valueSeparator - attributeSeparator - 2);
            string value = line.Substring(valueSeparator + 2);
            switch (attribute)
            {
                case "filter":
                    attributes.Filter = value;
                    break;
                case "diff":
                    attributes.Diff = value;
                    break;
                case "merge":
                    attributes.Merge = value;
                    break;
                case "text":
                    attributes.Text = value;
                    break;
            }
        }

        return attributes;
    }

    private static string[] GetGitOutputLines(params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        for (int i = 0; i < arguments.Length; i++)
        {
            startInfo.ArgumentList.Add(arguments[i]);
        }

        using Process process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start git process.");
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed with exit {process.ExitCode}: {error}");
        }

        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<string> EnumerateAssetDatabasePaths(string root)
    {
        foreach (string filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return NormalizePath(filePath);
        }

        foreach (string directoryPath in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            yield return NormalizePath(directoryPath);
        }
    }

    private static byte[] ComputeHash(HashAlgorithm hashAlgorithm, string path)
    {
        using FileStream stream = File.OpenRead(path);
        return hashAlgorithm.ComputeHash(stream);
    }

    private static string FormatHash(byte[] hash)
    {
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private struct GitAttributes
    {
        public string Filter;
        public string Diff;
        public string Merge;
        public string Text;
    }
}
#endif
