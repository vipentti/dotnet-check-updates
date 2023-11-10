// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal record MockUpgrade(string Name)
{
    public List<string> Versions { get; init; } = new();

    public HashSet<string> SupportedFrameworks { get; init; } = new();

    public static readonly HashSet<string> DefaultSupportedFrameworks =
        new() { "net6.0", "net7.0", "netstandard2.0", "netstandard2.1" };

    public void Deconstruct(
        out string name,
        out List<string> versions,
        out HashSet<string> frameworks
    )
    {
        name = Name;
        versions = Versions;
        frameworks = SupportedFrameworks;
    }
}
