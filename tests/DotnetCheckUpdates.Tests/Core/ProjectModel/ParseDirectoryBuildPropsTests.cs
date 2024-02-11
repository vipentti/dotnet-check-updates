// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.ProjectModel;

namespace DotnetCheckUpdates.Tests.Core.ProjectModel;

public class ParseDirectoryBuildPropsTests
{
    [Fact]
    public void ParsesFileWithPackageReferencesWithoutTargetFramework()
    {
        const string xml = """
<Project>
    <!-- General -->
    <PropertyGroup>
        <LangVersion>12.0</LangVersion>
        <Nullable>enable</Nullable>
        <Features>strict</Features>
    </PropertyGroup>

    <!-- Build -->
    <PropertyGroup>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
        <WarnOnPackingNonPackableProject>true</WarnOnPackingNonPackableProject>
    </PropertyGroup>

    <!-- Packaging -->
    <PropertyGroup Label="Packaging">
        <!-- Enable packaging on a per-project basis. -->
        <IsPackable>false</IsPackable>
        <IsPublishable>false</IsPublishable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Example" Version="3.0.178" />
    </ItemGroup>
</Project>
""";

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        using var scope = new AssertionScope();
        file.TargetFrameworks.Should().BeEmpty();

        file.PackageReferences.Should()
            .SatisfyRespectively(it =>
            {
                it.Name.Should().Be("Example");
                it.GetVersionString().Should().Be("3.0.178");
            });
    }
}
