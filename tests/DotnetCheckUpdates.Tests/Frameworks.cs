// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal static class Frameworks
{
    public const string Unspecified = "";

    public const string Net5_0 = "net5.0";
    public const string Net6_0 = "net6.0";
    public const string Net7_0 = "net7.0";
    public const string Net8_0 = "net8.0";
    public const string Net9_0 = "net9.0";
    public const string NetStandard2_0 = "netstandard2.0";
    public const string NetStandard2_1 = "netstandard2.1";

    public const string Default = Net8_0;
}
