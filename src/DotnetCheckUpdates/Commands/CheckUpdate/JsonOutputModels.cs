// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Text.Json.Serialization;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal sealed class JsonOutputDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("upgradeRequested")]
    public bool UpgradeRequested { get; init; }

    [JsonPropertyName("upgradesApplied")]
    public bool UpgradesApplied { get; init; }

    [JsonPropertyName("solutions")]
    public List<JsonSolution> Solutions { get; init; } = [];

    [JsonPropertyName("checkedFiles")]
    public List<JsonCheckedFile> CheckedFiles { get; init; } = [];
}

internal sealed class JsonSolution
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("projects")]
    public List<string> Projects { get; init; } = [];
}

internal sealed class JsonCheckedFile
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    [JsonPropertyName("packageCount")]
    public int PackageCount { get; init; }

    [JsonPropertyName("targetFrameworks")]
    public List<string> TargetFrameworks { get; init; } = [];

    [JsonPropertyName("packages")]
    public List<JsonPackage> Packages { get; init; } = [];
}

internal sealed class JsonPackage
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("currentVersion")]
    public string? CurrentVersion { get; init; }

    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; init; }

    [JsonPropertyName("upgradeType")]
    public string? UpgradeType { get; init; }

    [JsonPropertyName("applicableFrameworks")]
    public List<string> ApplicableFrameworks { get; init; } = [];
}

internal static class JsonOutputKind
{
    public const string Project = "project";
    public const string DirectoryBuildProps = "directoryBuildProps";
    public const string DirectoryPackagesProps = "directoryPackagesProps";
}

internal static class JsonUpgradeTypeTokens
{
    public const string None = "none";
    public const string Major = "major";
    public const string Minor = "minor";
    public const string Patch = "patch";
    public const string Release = "release";

    public static string FromUpgradeType(Core.UpgradeType type) =>
        type switch
        {
            Core.UpgradeType.None => None,
            Core.UpgradeType.Major => Major,
            Core.UpgradeType.Minor => Minor,
            Core.UpgradeType.Patch => Patch,
            Core.UpgradeType.Release => Release,
            _ => None,
        };
}
