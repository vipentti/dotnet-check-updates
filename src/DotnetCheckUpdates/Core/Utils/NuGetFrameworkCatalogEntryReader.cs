// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CommunityToolkit.HighPerformance.Buffers;
using NuGet.Frameworks;
using SpanUtils.Extensions;

namespace DotnetCheckUpdates.Core.Utils;

internal static class NuGetFrameworkCatalogEntryReader
{
    public static ImmutableHashSet<NuGetFramework> ReadFrameworksFromCatalogJson(string catalogJson)
    {
        using var doc = JsonDocument.Parse(
            catalogJson,
            new JsonDocumentOptions()
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }
        );
        return doc.ReadFrameworksFromCatalog();
    }

    public static ImmutableHashSet<NuGetFramework> ReadFrameworksFromCatalog(
        this JsonDocument? catalogDocument
    )
    {
        if (catalogDocument is null)
        {
            return ImmutableHashSet<NuGetFramework>.Empty;
        }

        var frameworks = ImmutableHashSet.CreateBuilder<NuGetFramework>();

        if (
            catalogDocument.RootElement.TryGetProperty("dependencyGroups", out var depElement)
            && depElement.ValueKind == JsonValueKind.Array
            && depElement.EnumerateArray() is JsonElement.ArrayEnumerator depEntries
        )
        {
            foreach (var dep in depEntries)
            {
                if (dep.TryGetNonNullStringProperty("targetFramework", out var frameworkName))
                {
                    var fw = NuGetFramework.Parse(frameworkName);

                    if (!fw.IsUnsupported)
                    {
                        frameworks.Add(fw);
                    }
                }
            }
        }

        if (
            catalogDocument.RootElement.TryGetProperty("packageEntries", out var entriesElement)
            && entriesElement.ValueKind == JsonValueKind.Array
            && entriesElement.EnumerateArray() is JsonElement.ArrayEnumerator entries
        )
        {
            foreach (var entry in entries)
            {
                if (entry.TryGetNonNullStringProperty("fullName", out var fullName))
                {
                    _ = entry.TryGetNonNullStringProperty("name", out var maybeName);

                    var nameSpan = (maybeName ?? "").AsSpan();

                    foreach (
                        var path in fullName.EnumerateSplitSubstrings(
                            new[] { '/', '\\' },
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                        )
                    )
                    {
                        if (path.Equals(nameSpan, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var pathString = StringPool.Shared.GetOrAdd(path);

                        if (s_knownFrameworks.TryGetValue(pathString, out var knownFramework))
                        {
                            frameworks.Add(knownFramework);
                        }
                        else if (!s_invalidPathParts.ContainsKey(pathString))
                        {
                            var fw = NuGetFramework.Parse(pathString);

                            if (!fw.IsUnsupported)
                            {
                                frameworks.Add(fw);
                                _ = s_knownFrameworks.TryAdd(pathString, fw);
                            }
                            else
                            {
                                _ = s_invalidPathParts.TryAdd(pathString, true);
                            }
                        }
                    }
                }
            }
        }

        return frameworks.ToImmutable();
    }

    private static readonly ConcurrentDictionary<string, bool> s_invalidPathParts = new();
    private static readonly ConcurrentDictionary<string, NuGetFramework> s_knownFrameworks = new();

    public static bool TryGetNonNullStringProperty(
        this JsonElement element,
        string propertyName,
        [NotNullWhen(true)] out string? value
    )
    {
        if (
            element.TryGetProperty(propertyName, out var valueElement)
            && valueElement.ValueKind == JsonValueKind.String
        )
        {
            value = valueElement.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }
}
