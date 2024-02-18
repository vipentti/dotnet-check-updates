// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Globalization;
using System.Text;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using static DotnetCheckUpdates.Tests.ProjectFileUtils;

namespace DotnetCheckUpdates.Tests.Core.ProjectModel;

public class ProjectFileParserTests
{
    [Fact]
    public void ParsesProjectFileWithPackageRefrences()
    {
        var projectFile = ProjectFileParser.ParseProjectFile(
            ProjectFileXml(new[] { ("Flurl", "3.0.1"), ("Flurl.Http", "3.0.1") }),
            "testfile"
        );

        projectFile
            .TargetFrameworks.Should()
            .ContainEquivalentOf(Frameworks.Default.ToNuGetFramework())
            .And.HaveCount(1);

        projectFile
            .PackageReferences.Should()
            .SatisfyRespectively(
                flurl =>
                {
                    flurl.Name.Should().Be("Flurl");
                    flurl.GetVersionString().Should().Be("3.0.1");
                },
                flurlHttp =>
                {
                    flurlHttp.Name.Should().Be("Flurl.Http");
                    flurlHttp.GetVersionString().Should().Be("3.0.1");
                }
            );

        projectFile.FilePath.Should().Be("testfile");
    }

    [Fact]
    public void Parses_ProjectFile_Correctly()
    {
        var projectFile = ProjectFileParser.ParseProjectFile(
            @"
<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""OneOf"" Version=""3.0.178"" />
    <PackageReference Include=""Pulumi"" Version=""3.0.0"" />
    <PackageReference Include=""YamlDotNet"" Version=""9.1.0"" />
  </ItemGroup>
</Project>
            ",
            "testfile"
        );

        projectFile
            .TargetFrameworks.Should()
            .ContainEquivalentOf(Frameworks.Net5_0.ToNuGetFramework())
            .And.HaveCount(1);

        projectFile
            .PackageReferences.Should()
            .SatisfyRespectively(
                it =>
                {
                    it.Name.Should().Be("OneOf");
                    it.GetVersionString().Should().Be("3.0.178");
                },
                it =>
                {
                    it.Name.Should().Be("Pulumi");
                    it.GetVersionString().Should().Be("3.0.0");
                },
                it =>
                {
                    it.Name.Should().Be("YamlDotNet");
                    it.GetVersionString().Should().Be("9.1.0");
                }
            );

        projectFile.FilePath.Should().Be("testfile");
    }

    [Fact]
    public void CorrectlyUpgradesPackagesInOrder()
    {
        var content = ProjectFileXml(
            new[]
            {
                ("NoUpgrade1", "1.0.0"),
                ("NoUpgrade2", "1.0.0"),
                ("NoUpgrade3", "1.0.0"),
                ("Upgrade1", "1.0.0"),
                ("NoUpgrade4", "1.0.0"),
                ("Upgrade2", "1.0.0"),
                ("NoUpgrade5", "1.0.0"),
                ("Upgrade3", "1.0.0"),
            }
        );

        var newProjectFile = ProjectFileParser
            .ParseProjectFile(content, "testfile")
            .UpdatePackageReferences(
                new()
                {
                    ["Upgrade1"] = "1.2.0".ToVersionRange(),
                    ["Upgrade2"] = "2.0.0".ToVersionRange(),
                    ["Upgrade3"] = "3.0.0".ToVersionRange(),
                }
            );

        newProjectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                ProjectFileXml(
                    new[]
                    {
                        ("NoUpgrade1", "1.0.0"),
                        ("NoUpgrade2", "1.0.0"),
                        ("NoUpgrade3", "1.0.0"),
                        ("Upgrade1", "1.2.0"),
                        ("NoUpgrade4", "1.0.0"),
                        ("Upgrade2", "2.0.0"),
                        ("NoUpgrade5", "1.0.0"),
                        ("Upgrade3", "3.0.0"),
                    }
                )
            );
    }

    [Fact]
    public void Can_Upgrade_Packages()
    {
        var projectFile = ProjectFileParser.ParseProjectFile(TestProjectFile(), "testfile");

        var newProjectFile = projectFile.UpdatePackageReferences(
            new()
            {
                ["Flurl"] = "3.2.0".ToVersionRange(),
                ["Flurl.Http"] = "3.2.1".ToVersionRange(),
            }
        );

        newProjectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.2.0"" />
    <PackageReference Include=""Flurl.Http"" Version=""3.2.1"" />
  </ItemGroup>
</Project>
".TrimStart()
            );
    }

    [Fact]
    public void ProjectFile_Round_Trip_Xml()
    {
        var content = TestProjectFile();
        var projectFile = ProjectFileParser.ParseProjectFile(content, "testfile");

        projectFile.ProjectFileToXml().Should().Be(content);
    }

    [Fact]
    public void Maintains_Whitespace_And_Formatting_When_Upgrading_Packages()
    {
        var content = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
        <OutputType>Exe</OutputType>

        <TargetFramework>net5.0</TargetFramework>

    <Nullable>enable</Nullable>

  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.2.0"" /> <PackageReference Include=""Flurl.Http"" Version=""3.2.1"" />
  </ItemGroup>
</Project>
".TrimStart();

        var projectFile = ProjectFileParser.ParseProjectFile(content, "testfile");

        var newProjectFile = projectFile.UpdatePackageReferences(
            new PackageUpgradeVersionDictionary()
            {
                ["Flurl"] = "9.2.0".ToVersionRange(),
                ["Flurl.Http"] = "9.2.1".ToVersionRange(),
            }
        );

        // Existing should not be modified
        projectFile.ProjectFileToXml().Should().Be(content);
        newProjectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
        <OutputType>Exe</OutputType>

        <TargetFramework>net5.0</TargetFramework>

    <Nullable>enable</Nullable>

  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""9.2.0"" /> <PackageReference Include=""Flurl.Http"" Version=""9.2.1"" />
  </ItemGroup>
</Project>
".TrimStart()
            );
    }

    [Fact]
    public void Maintains_Whitespace_And_Formatting()
    {
        var content = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
        <OutputType>Exe</OutputType>

        <TargetFramework>net5.0</TargetFramework>

    <Nullable>enable</Nullable>

  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.2.0"" /> <PackageReference Include=""Flurl.Http"" Version=""3.2.1"" />
  </ItemGroup>
</Project>
".TrimStart();

        var projectFile = ProjectFileParser.ParseProjectFile(content, "testfile");

        projectFile.ProjectFileToXml().Should().Be(content);
    }

    [Fact]
    public void Throws_with_unsupported_sdk()
    {
        var xml = @"
<Project Sdk=""Unsupported.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.0.1"" />
    <PackageReference Include=""Flurl.Http"" Version=""3.0.1"" />
  </ItemGroup>
</Project>
".TrimStart();

        var act = () => ProjectFileParser.ParseProjectFile(xml, "test");

        act.Should()
            .ThrowExactly<FormatException>()
            .WithMessage(
                "Unsupported Project format: Unsupported Sdk 'Unsupported.Sdk' supported sdks are*"
            );
    }

    [Fact]
    public void Throws_WhenProjectElementIsMissing()
    {
        var xml = @"
<NotProject Sdk=""Unsupported.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.0.1"" />
    <PackageReference Include=""Flurl.Http"" Version=""3.0.1"" />
  </ItemGroup>
</NotProject>
".TrimStart();

        var act = () => ProjectFileParser.ParseProjectFile(xml, "test");

        act.Should()
            .ThrowExactly<FormatException>()
            .WithMessage("Unsupported file: Project element not found in*");
    }

    [Fact]
    public void Throws_WhenSdkAttributeIsMissing()
    {
        var xml = @"
<Project NotSdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.0.1"" />
    <PackageReference Include=""Flurl.Http"" Version=""3.0.1"" />
  </ItemGroup>
</Project>
".TrimStart();

        var act = () => ProjectFileParser.ParseProjectFile(xml, "test");

        act.Should()
            .ThrowExactly<FormatException>()
            .WithMessage("Unsupported Project format: Sdk attribute is missing in 'test'");
    }

    [Fact]
    public void Throws_WhenTargetFrameworksAreMissing()
    {
        var xml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <NotTargetFramework>net5.0</NotTargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.0.1"" />
    <PackageReference Include=""Flurl.Http"" Version=""3.0.1"" />
  </ItemGroup>
</Project>
".TrimStart();

        var act = () => ProjectFileParser.ParseProjectFile(xml, "test");

        act.Should()
            .ThrowExactly<FormatException>()
            .WithMessage("Unsupported Project format: TargetFramework(s) are not specified");
    }

    private static string TestProjectFile(IEnumerable<PackageReference>? packages = default) =>
        $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Flurl"" Version=""3.0.1"" />
    <PackageReference Include=""Flurl.Http"" Version=""3.0.1"" />{PackagesToString(packages)}
  </ItemGroup>
</Project>
".TrimStart();

    private static string PackagesToString(IEnumerable<PackageReference>? packages, int indent = 4)
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
                $"<PackageReference Include=\"{package.Name}\" Version=\"{package.GetVersionString()}\" />"
            );
#pragma warning restore RCS1197 // Optimize StringBuilder.Append/AppendLine call.
        }

        sb.Length -= Environment.NewLine.Length;

        return sb.ToString();
    }
}
