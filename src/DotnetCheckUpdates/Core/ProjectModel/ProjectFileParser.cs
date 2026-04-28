// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using DotnetCheckUpdates.Core.Extensions;
using NuGet.Frameworks;
using Spectre.Console;

namespace DotnetCheckUpdates.Core.ProjectModel;

#pragma warning disable S125 // Sections of code should not be commented out
internal static class Errors
{
    public const string FailedToReadXml = "Unable to read XML {0}";

    // public static readonly CompositeFormat CompositeFailedToReadXml = CompositeFormat.Parse(FailedToReadXml);

    public const string ProjectNotFound = "Unsupported file: Project element not found in '{0}'";

    // public static readonly CompositeFormat CompositeProjectNotFound = CompositeFormat.Parse(ProjectNotFound);

    public const string SdkAttributeMissing =
        "Unsupported Project format: Sdk attribute is missing in '{0}'";

    // public static readonly CompositeFormat CompositeSdkAttributeMissing = CompositeFormat.Parse(SdkAttributeMissing);

    public const string UnsupportedSdk =
        "Unsupported Project format: Unsupported Sdk '{0}' supported sdks are: '{1}'";

    // public static readonly CompositeFormat CompositeUnsupportedSdk = CompositeFormat.Parse(UnsupportedSdk);
    public const string TargetFrameworkMissing =
        "Unsupported Project format: TargetFramework(s) are not specified";

    // public static readonly CompositeFormat CompositeTargetFrameworkMissing = CompositeFormat.Parse(TargetFrameworkMissing);
}
#pragma warning restore S125 // Sections of code should not be commented out

internal static partial class ProjectFileParser
{
    [GeneratedRegex(
        "'\\s*\\$\\(\\s*TargetFramework\\s*\\)\\s*'\\s*(==|!=)\\s*'\\s*([^']+?)\\s*'",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100
    )]
    private static partial Regex TargetFrameworkCondition();

    [GeneratedRegex(
        "\\bOr\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100
    )]
    private static partial Regex TargetFrameworkConditionOr();

    private static readonly Regex s_targetFrameworkConditionRegex = TargetFrameworkCondition();
    private static readonly Regex s_targetFrameworkConditionOrRegex = TargetFrameworkConditionOr();

    // https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview#available-sdks
    private static readonly HashSet<string> s_supportedSdks =
    [
        "Microsoft.NET.Sdk",
        "Microsoft.NET.Sdk.Web",
        "Microsoft.NET.Sdk.BlazorWebAssembly",
        "Microsoft.NET.Sdk.Razor",
        "Microsoft.NET.Sdk.Worker",
        "Microsoft.NET.Sdk.WindowsDesktop",
    ];

    public static IEnumerable<XElement> GetPackageReferenceElements(this XDocument xml)
    {
        foreach (var el in xml.Descendants("PackageReference") ?? [])
        {
            yield return el;
        }

        foreach (var el in xml.Descendants("PackageVersion") ?? [])
        {
            yield return el;
        }
    }

    public static IEnumerable<XElement> GetImportElements(this XDocument xml) =>
        xml.Descendants("Import") ?? [];

    private static MemoryStream ToStream(this string input)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(input);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    public static readonly Encoding Utf8WithoutBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false
    );

    private static string DetectEncoding(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(
            stream,
            Utf8WithoutBom,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true
        );
        reader.Peek();

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        var encoding = reader.CurrentEncoding;

        return encoding.BodyName;
    }

    private static bool HasUtf8ByteOrderMarker(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        Span<byte> buffer = stackalloc byte[3];

        if (stream.Read(buffer) >= 3 && buffer.SequenceEqual(Encoding.UTF8.Preamble))
        {
            return true;
        }

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        return false;
    }

    public static ProjectFile ParseLessStrictProjectFile(string xmlContent, string filePath)
    {
        XDocument xml;
        string encodingName;
        bool hasByteOrderMarker;

        using (var ms = xmlContent.ToStream())
        {
            encodingName = DetectEncoding(ms);
            hasByteOrderMarker = HasUtf8ByteOrderMarker(ms);
            using (var reader = XmlReader.Create(ms))
            {
                xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
        }

        if (xml?.Root is null)
        {
            ThrowHelper.ThrowFormatException(
                string.Format(CultureInfo.InvariantCulture, Errors.FailedToReadXml, filePath)
            );
        }

        var projectNode =
            xml.Elements()
                .FirstOrDefault(it =>
                    string.Equals(it?.Name?.LocalName, "Project", StringComparison.Ordinal)
                )
            ?? ThrowHelper.ThrowFormatException<XElement>(
                string.Format(CultureInfo.InvariantCulture, Errors.ProjectNotFound, filePath)
            );

        var sdk = projectNode.Attribute("Sdk")?.Value;

        var targetFramework =
            xml.Descendants("TargetFramework").FirstOrDefault()?.Value
            ?? (xml.Descendants("TargetFrameworks").FirstOrDefault()?.Value)
            ?? "";

        var imports = xml.GetImportElements()
            .Select(item => (string?)item.Attribute("Project"))
            .Where(it => !string.IsNullOrEmpty(it))
            .Select(it => new Import(it!))
            .ToImmutableArray();

        var packageReferences = xml.GetPackageReferenceElements()
            .ToPackageReferences()
            .ToImmutableArray();

        var frameworks = targetFramework
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NuGetFramework.Parse)
            .Where(it => !it.IsUnsupported)
            .ToImmutableArray();

        return new ProjectFile(filePath, frameworks, packageReferences)
        {
            Xml = xml,
            Sdk = sdk,
            Imports = imports,
            EncodingName = encodingName,
            HasUtf8ByteOrderMarker = hasByteOrderMarker,
        };
    }

    private static IEnumerable<PackageReference> ToPackageReferences(
        this IEnumerable<XElement> elements
    ) =>
        elements
            .Select(
                (item, index) =>
                    (
                        index,
                        include: (string?)item.Attribute("Include"),
                        version: (string?)item.Attribute("Version"),
                        itemConditionGroups: ParseTargetFrameworkConditionGroups(
                            (string?)item.Attribute("Condition")
                        ),
                        itemGroupConditionGroups: ParseTargetFrameworkConditionGroups(
                            (string?)item.Parent?.Attribute("Condition")
                        )
                    )
            )
            .Where(item => !string.IsNullOrWhiteSpace(item.include))
            .Select(item => new PackageReference(item.include!, item.version.ToVersionRange())
            {
                ReferenceId = item.index,
                TargetFrameworkConditions = BuildConditions(
                    item.itemGroupConditionGroups,
                    item.itemConditionGroups
                ),
            });

    private static ImmutableArray<
        ImmutableArray<TargetFrameworkCondition>
    > ParseTargetFrameworkConditionGroups(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return [];
        }

        var segments = s_targetFrameworkConditionOrRegex.Split(condition);
        var groupBuilder = ImmutableArray.CreateBuilder<ImmutableArray<TargetFrameworkCondition>>();

        for (var groupIndex = 0; groupIndex < segments.Length; groupIndex++)
        {
            var matches = s_targetFrameworkConditionRegex.Matches(segments[groupIndex]);

            if (matches.Count == 0)
            {
                continue;
            }

            var conditionBuilder = ImmutableArray.CreateBuilder<TargetFrameworkCondition>(
                matches.Count
            );

            foreach (Match match in matches)
            {
                var op = match.Groups[1].Value;
                var framework = match.Groups[2].Value.Trim();

                conditionBuilder.Add(
                    new(
                        op == "=="
                            ? TargetFrameworkConditionOperator.Equals
                            : TargetFrameworkConditionOperator.NotEquals,
                        framework,
                        match.Value,
                        groupBuilder.Count
                    )
                );
            }

            groupBuilder.Add(conditionBuilder.ToImmutable());
        }

        return groupBuilder.ToImmutable();
    }

    private static ImmutableArray<TargetFrameworkCondition> BuildConditions(
        ImmutableArray<ImmutableArray<TargetFrameworkCondition>> itemGroupConditionGroups,
        ImmutableArray<ImmutableArray<TargetFrameworkCondition>> itemConditionGroups
    )
    {
        if (itemGroupConditionGroups.IsDefaultOrEmpty && itemConditionGroups.IsDefaultOrEmpty)
        {
            return [];
        }

        if (itemGroupConditionGroups.IsDefaultOrEmpty)
        {
            return FlattenConditionGroups(itemConditionGroups);
        }

        if (itemConditionGroups.IsDefaultOrEmpty)
        {
            return FlattenConditionGroups(itemGroupConditionGroups);
        }

        var totalConditions =
            itemGroupConditionGroups.Sum(it => it.Length) * itemConditionGroups.Length
            + itemConditionGroups.Sum(it => it.Length) * itemGroupConditionGroups.Length;
        var builder = ImmutableArray.CreateBuilder<TargetFrameworkCondition>(totalConditions);
        var groupId = 0;

        foreach (var itemGroupConditions in itemGroupConditionGroups)
        {
            foreach (var itemConditions in itemConditionGroups)
            {
                foreach (var condition in itemGroupConditions)
                {
                    builder.Add(condition with { GroupId = groupId });
                }

                foreach (var condition in itemConditions)
                {
                    builder.Add(condition with { GroupId = groupId });
                }

                groupId++;
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TargetFrameworkCondition> FlattenConditionGroups(
        ImmutableArray<ImmutableArray<TargetFrameworkCondition>> groups
    )
    {
        if (groups.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<TargetFrameworkCondition>(
            groups.Sum(it => it.Length)
        );

        for (var groupId = 0; groupId < groups.Length; groupId++)
        {
            foreach (var condition in groups[groupId])
            {
                builder.Add(condition with { GroupId = groupId });
            }
        }

        return builder.ToImmutable();
    }

    public static ProjectFile ParseProjectFile(string xmlContent, string filePath)
    {
        var file = ParseLessStrictProjectFile(xmlContent, filePath);

        if (file.Sdk is null)
        {
            throw new FormatException(
                string.Format(CultureInfo.InvariantCulture, Errors.SdkAttributeMissing, filePath)
            );
        }

        if (!s_supportedSdks.Contains(file.Sdk))
        {
            ThrowHelper.ThrowFormatException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    Errors.UnsupportedSdk,
                    file.Sdk,
                    string.Join(", ", s_supportedSdks)
                )
            );
        }

        if (file.TargetFrameworks.Length == 0)
        {
            ThrowHelper.ThrowFormatException(Errors.TargetFrameworkMissing);
        }

        return file;
    }
}
