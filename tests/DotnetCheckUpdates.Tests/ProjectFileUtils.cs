// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Globalization;
using System.Text;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;

namespace DotnetCheckUpdates.Tests;

internal static class ProjectFileUtils
{
    public static string ProjectFileXml(
        IEnumerable<(string id, string version)> packages,
        string framework = Frameworks.Default,
        ReferenceType referenceType = ReferenceType.PackageReference
    ) =>
        ProjectFileXml(
            packages.Select(it => new PackageReference(
                it.id,
                string.IsNullOrWhiteSpace(it.version) ? null! : it.version.ToVersionRange()
            )),
            framework,
            referenceType
        );

    public static string ProjectFileXml(
        IEnumerable<PackageReference> packages,
        string framework = Frameworks.Default,
        ReferenceType referenceType = ReferenceType.PackageReference
    ) =>
        $@"
<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>{framework}</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>{PackagesToString(packages, referenceType: referenceType)}
  </ItemGroup>

</Project>
".Trim();

    public static string SolutionFile(IEnumerable<(string name, string path)> projects) =>
        $@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
{string.Join(Environment.NewLine, projects.Select(FormatSolutionProject))}
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
".Trim();

    private static string FormatSolutionProject((string name, string path) tuple)
    {
        var (name, path) = tuple;
        return $$"""
            Project("{{{Guid.NewGuid()}}}") = "{{name}}", "{{path}}", "{{{Guid.NewGuid()}}}"
            EndProject
            """;
    }

    private static string PackagesToString(
        IEnumerable<PackageReference>? packages,
        int indent = 4,
        ReferenceType referenceType = ReferenceType.PackageReference
    )
    {
        if (packages?.Any() != true)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.AppendLine();

        var spaces = new string(' ', indent);

        foreach (var package in packages)
        {
            sb.Append(spaces);
#pragma warning disable RCS1197 // Optimize StringBuilder.Append/AppendLine call.
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"""<{referenceType} Include="{package.Name}" {(package.Version is null ? "" : $"Version=\"{package.GetVersionString()}\"")} />"""
            );
#pragma warning restore RCS1197 // Optimize StringBuilder.Append/AppendLine call.
        }

        sb.Length -= Environment.NewLine.Length;

        return sb.ToString();
    }
}
