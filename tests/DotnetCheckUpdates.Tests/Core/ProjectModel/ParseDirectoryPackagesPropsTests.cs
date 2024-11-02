// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;

namespace DotnetCheckUpdates.Tests.Core.ProjectModel;

public class ParseDirectoryPackagesPropsTests
{
    [Fact]
    public void CanParseDirectoryPackagePropsFile()
    {
        const string xml = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        using var scope = new AssertionScope();
        file.TargetFrameworks.Should().BeEmpty();
        file.PackageReferences.Should()
            .BeEquivalentTo([new PackageReference("Example", "13.0.1".ToVersionRange())]);
    }

    [Fact]
    public void SupportRoundTrip()
    {
        const string xml = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.ProjectFileToXml().Should().Be(xml);
    }

    [Fact]
    public void CanUpgradePackages()
    {
        const string xml = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>

              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>

            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        var result = file.UpdatePackageReferences(
            new() { ["Example"] = "14.0.0".ToVersionRange() }
        );

        result
            .ProjectFileToXml()
            .Should()
            .Be(
                """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageVersion Include="Example" Version="14.0.0" />
                  </ItemGroup>

                </Project>
                """
            );
    }
}
