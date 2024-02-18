// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal sealed record MockUpgrade(string Name)
{
    public List<string> Versions { get; init; } = [];

    public HashSet<string> SupportedFrameworks { get; init; } = [];

    public static readonly HashSet<string> DefaultSupportedFrameworks =
    [
        Frameworks.Net6_0,
        Frameworks.Net7_0,
        Frameworks.Net8_0,
        Frameworks.NetStandard2_0,
        Frameworks.NetStandard2_1
    ];

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
