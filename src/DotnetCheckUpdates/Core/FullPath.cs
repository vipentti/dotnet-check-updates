// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core;

/// <summary>
/// Utility struct to provide helpers to work with full (absolute) paths
/// </summary>
internal readonly record struct FullPath
{
    private readonly string _path;

    private FullPath(string path) => _path = path;

    public static FullPath Create(string path) => new(Nuke.Common.IO.AbsolutePath.Create(path));

    /// <summary>
    /// Returns this path as a string
    /// </summary>
    public override string ToString() => _path;

    public FullPath Combine(FullPath b) => Combine(this, b);

    public FullPath Combine(string b) => Combine(this, b);

    public FullPath PathCombine(FullPath b) => Combine(this, b);

    public FullPath PathCombine(string b) => Combine(this, b);

    public static FullPath operator /(FullPath a, FullPath b) => Combine(a, b);

    public static FullPath operator /(FullPath a, string b) => Combine(a, b);

    public static FullPath Combine(FullPath a, FullPath b) => Create(InternalCombine(a, b));

    public static FullPath Combine(FullPath a, string b) => Create(InternalCombine(a, b));

    public static FullPath Combine(string a, string b) => Create(InternalCombine(a, b));

    public static FullPath Combine(string a, FullPath b) => Create(InternalCombine(a, b));

    public static implicit operator string(FullPath it) => it._path;

    private static string InternalCombine(string left, string right) =>
        Nuke.Common.IO.PathConstruction.NormalizePath(
            Nuke.Common.IO.PathConstruction.Combine(left, right)
        );
}
