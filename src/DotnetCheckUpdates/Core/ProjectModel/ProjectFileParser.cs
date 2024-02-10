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
        new()
        {
            "Microsoft.NET.Sdk",
            "Microsoft.NET.Sdk.Web",
            "Microsoft.NET.Sdk.BlazorWebAssembly",
            "Microsoft.NET.Sdk.Razor",
            "Microsoft.NET.Sdk.Worker",
            "Microsoft.NET.Sdk.WindowsDesktop",
        };

    public static IEnumerable<XElement> GetPackageReferenceElements(this XDocument xml) =>
        xml.Descendants("PackageReference") ?? Enumerable.Empty<XElement>();

    public static ProjectFile ParseProjectFile(string xmlContent, string filePath)
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

        if (projectNode.Attribute("Sdk") is not XAttribute sdkAttribute)
        {
            // Normal throw here to workaround how the compiler works with pattern matched variables related to https://github.com/dotnet/csharpstandard/issues/290
            throw new FormatException(string.Format(Errors.SdkAttributeMissing, filePath));
        }

        if (!s_supportedSdks.Contains(sdkAttribute.Value))
        {
            ThrowHelper.ThrowFormatException(
                string.Format(
                    Errors.UnsupportedSdk,
                    sdkAttribute.Value,
                    string.Join(", ", s_supportedSdks)
                )
            );
        }

        var targetFramework = xml.Descendants("TargetFramework").FirstOrDefault()?.Value;
        var targetFrameworks = xml.Descendants("TargetFrameworks").FirstOrDefault()?.Value;

        targetFramework ??= targetFrameworks;

        if (targetFramework is null)
        {
            ThrowHelper.ThrowFormatException(Errors.TargetFrameworkMissing);
        }

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

        return new ProjectFile(filePath, frameworks, packageReferences) { Xml = xml, };
    }
}
