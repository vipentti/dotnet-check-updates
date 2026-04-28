// Copyright 2023-2026 Ville Penttinen
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
        file.PackageReferences.Should().ContainSingle();
        file.PackageReferences[0].Name.Should().Be("Example");
        file.PackageReferences[0].GetVersionString().Should().Be("13.0.1");
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

    [Fact]
    public void CanParseItemAndItemGroupTargetFrameworkConditions()
    {
        const string xml = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>

              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>

              <ItemGroup>
                <PackageVersion Include="Example2" Version="2.0.0" Condition="'$(TargetFramework)' != 'net9.0'" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences.Should().HaveCount(2);

        file.PackageReferences[0].Name.Should().Be("Example");
        file.PackageReferences[0].TargetFrameworkConditions.Should().ContainSingle();
        file.PackageReferences[0].DisplayName.Should().Contain("net8.0");

        file.PackageReferences[1].Name.Should().Be("Example2");
        file.PackageReferences[1].TargetFrameworkConditions.Should().ContainSingle();
        file.PackageReferences[1].DisplayName.Should().Contain("!= net9.0");
    }

    [Fact]
    public void CanParseTargetFrameworkOrCondition()
    {
        const string xml = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" Condition="'$(TargetFramework)' == 'net8.0' Or '$(TargetFramework)' == 'net9.0'" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");
        var package = file.PackageReferences.Should().ContainSingle().Subject;

        package.TargetFrameworkConditions.Should().HaveCount(2);
        package.DisplayName.Should().Contain("net8.0 Or net9.0");
        package.TargetFrameworkConditions.Select(it => it.GroupId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void CanUpgradeDuplicatePackageNamesWithDifferentConditions()
    {
        const string xml = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" Condition="'$(TargetFramework)' == 'net8.0'" />
                <PackageVersion Include="Example" Version="14.0.1" Condition="'$(TargetFramework)' == 'net9.0'" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        var first = file.PackageReferences[0];
        var second = file.PackageReferences[1];

        first.UpgradeKey.Should().NotBe(second.UpgradeKey);

        var result = file.UpdatePackageReferences(
            new()
            {
                [first.UpgradeKey] = "13.1.0".ToVersionRange(),
                [second.UpgradeKey] = "14.1.0".ToVersionRange(),
            }
        );

        result
            .ProjectFileToXml()
            .Should()
            .Contain(
                "<PackageVersion Include=\"Example\" Version=\"13.1.0\" Condition=\"'$(TargetFramework)' == 'net8.0'\" />"
            )
            .And.Contain(
                "<PackageVersion Include=\"Example\" Version=\"14.1.0\" Condition=\"'$(TargetFramework)' == 'net9.0'\" />"
            );
    }

    public static TheoryData<string> ConditionsWithoutSupportedTargetFrameworkPredicate =>
        new() { "'$(TargetFramework)' &gt;= 'net8.0'", "'$(Configuration)' == 'Release'" };

    [Theory]
    [MemberData(nameof(ConditionsWithoutSupportedTargetFrameworkPredicate))]
    public void ConditionWithoutSupportedTargetFrameworkPredicateIsIgnoredWhenAppliedToPackage(
        string condition
    )
    {
        var xml = $$"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" Condition="{{condition}}" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences.Should().ContainSingle();
        file.PackageReferences[0].TargetFrameworkConditions.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ConditionsWithoutSupportedTargetFrameworkPredicate))]
    public void ConditionWithoutSupportedTargetFrameworkPredicateIsIgnoredWhenAppliedToItemGroupWithPackage(
        string condition
    )
    {
        var xml = $$"""
            <Project>
              <ItemGroup Condition="{{condition}}">
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences.Should().ContainSingle();
        file.PackageReferences[0].TargetFrameworkConditions.Should().BeEmpty();
    }

    public static TheoryData<string> MixedConditions =>
        new()
        {
            "'$(TargetFramework)' == 'net8.0' And '$(Configuration)' == 'Release'",
            "'$(TargetFramework)' != 'net9.0' And '$(UseReproducibleBuild)' == 'true'",
        };

    [Theory]
    [MemberData(nameof(MixedConditions))]
    public void MixedConditionExtractsSupportedTargetFrameworkPredicateFromPackageCondition(
        string condition
    )
    {
        var xml = $$"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" Condition="{{condition}}" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences.Should().ContainSingle();
        file.PackageReferences[0].TargetFrameworkConditions.Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(MixedConditions))]
    public void MixedConditionExtractsSupportedTargetFrameworkPredicateFromItemGroupCondition(
        string condition
    )
    {
        var xml = $$"""
            <Project>
              <ItemGroup Condition="{{condition}}">
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences.Should().ContainSingle();
        file.PackageReferences[0].TargetFrameworkConditions.Should().ContainSingle();
    }

    [Fact]
    public void MixedConditionExtractsMultipleSupportedTargetFrameworkPredicatesInOrder()
    {
        const string xml = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" Condition="'$(TargetFramework)' != 'net8.0' And '$(Configuration)' == 'Release' And '$(TargetFramework)' != 'net9.0'" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences.Should().ContainSingle();
        file.PackageReferences[0]
            .TargetFrameworkConditions.Select(it => it.ToDisplayString())
            .Should()
            .Equal("!= net8.0", "!= net9.0");
    }

    [Fact]
    public void OrConditionAppliesToMatchingFrameworks()
    {
        const string xml = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" Condition="'$(TargetFramework)' == 'net8.0' Or '$(TargetFramework)' == 'net9.0'" />
              </ItemGroup>
            </Project>
            """;

        var file = ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        file.PackageReferences[0]
            .GetApplicableFrameworks(file.TargetFrameworks)
            .Select(it => it.GetShortFolderName())
            .Should()
            .Equal("net8.0", "net9.0");
    }

    [Theory]
    [MemberData(nameof(MixedConditions))]
    public void UnsupportedConditionDoesNotThrowWhenAppliedToTarget(string condition)
    {
        var xml = $$"""
            <Project>
              <Target Condition="{{condition}}"></Target>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var act = () => ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(MixedConditions))]
    public void UnsupportedConditionDoesNotThrowWhenWhenAppliedToEmptyItemGroup(string condition)
    {
        var xml = $$"""
            <Project>
              <ItemGroup Condition="{{condition}}">
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var act = () => ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(MixedConditions))]
    public void UnsupportedConditionDoesNotThrowWhenWhenAppliedToItemGroupWithNonPackageElements(
        string condition
    )
    {
        var xml = $$"""
            <Project>
              <ItemGroup Condition="{{condition}}">
                <Target></Target>
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="Example" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;

        var act = () => ProjectFileParser.ParseLessStrictProjectFile(xml, "test");

        act.Should().NotThrow();
    }
}
