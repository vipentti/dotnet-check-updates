// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Reflection;

namespace DotnetCheckUpdates.Core.Utils;

internal static class AssemblyUtils
{
    public static string GetEntryAssemblyVersion() =>
        Assembly.GetEntryAssembly().GetAssemblyVersion();

    public static string GetAssemblyVersion(this Assembly? assembly)
    {
        return assembly?.GetInformationalVersion()
            ?? assembly?.GetVersion()
            ?? assembly?.GetFileVersion()
            ?? "0.0.1-unknown";
    }

    public static string? GetInformationalVersion(this Assembly? assembly)
    {
        var attribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion;
    }

    public static string? GetVersion(this Assembly? assembly)
    {
        var attribute = assembly?.GetCustomAttribute<AssemblyVersionAttribute>();
        return attribute?.Version;
    }

    public static string? GetFileVersion(this Assembly? assembly)
    {
        var attribute = assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>();
        return attribute?.Version;
    }
}
