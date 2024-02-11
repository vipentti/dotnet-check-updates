// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using DotnetCheckUpdates.Core.Extensions;
using NuGet.Frameworks;
using Spectre.Console;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal static class Errors
{
    public const string FailedToReadXml = "Unable to read XML {0}";
    public const string ProjectNotFound = "Unsupported file: Project element not found in '{0}'";
    public const string SdkAttributeMissing =
        "Unsupported Project format: Sdk attribute is missing in '{0}'";
    public const string UnsupportedSdk =
        "Unsupported Project format: Unsupported Sdk '{0}' supported sdks are: '{1}'";
    public const string TargetFrameworkMissing =
        "Unsupported Project format: TargetFramework(s) are not specified";
}

internal static class ProjectFileParser
{
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

    public static IEnumerable<XElement> GetPackageReferenceElements(this XDocument xml) =>
        xml.Descendants("PackageReference") ?? Enumerable.Empty<XElement>();

    public static IEnumerable<XElement> GetImportElements(this XDocument xml) =>
        xml.Descendants("Import") ?? Enumerable.Empty<XElement>();

    public static ProjectFile ParseLessStrictProjectFile(string xmlContent, string filePath)
    {
        var xml = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);

        if (xml?.Root is null)
        {
            ThrowHelper.ThrowFormatException(string.Format(Errors.FailedToReadXml, filePath));
        }

        var projectNode =
            xml.Elements().FirstOrDefault(it => string.Equals(it?.Name?.LocalName, "Project"))
            ?? ThrowHelper.ThrowFormatException<XElement>(
                string.Format(Errors.ProjectNotFound, filePath)
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
            .Select(item =>
                (
                    include: (string?)item.Attribute("Include"),
                    version: (string?)item.Attribute("Version")
                )
            )
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.include) && !string.IsNullOrWhiteSpace(item.version)
            )
            .Select(item => new PackageReference(item.include!, item.version!.ToVersionRange()))
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
            Imports = imports
        };
    }

    public static ProjectFile ParseProjectFile(string xmlContent, string filePath)
    {
        var file = ParseLessStrictProjectFile(xmlContent, filePath);

        if (file.Sdk is null)
        {
            throw new FormatException(string.Format(Errors.SdkAttributeMissing, filePath));
        }

        if (!s_supportedSdks.Contains(file.Sdk))
        {
            ThrowHelper.ThrowFormatException(
                string.Format(Errors.UnsupportedSdk, file.Sdk, string.Join(", ", s_supportedSdks))
            );
        }

        if (file.TargetFrameworks.Length == 0)
        {
            ThrowHelper.ThrowFormatException(Errors.TargetFrameworkMissing);
        }

        return file;
    }
}
