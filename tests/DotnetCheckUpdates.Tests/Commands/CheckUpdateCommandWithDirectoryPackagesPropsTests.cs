// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using Spectre.Console.Testing;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandWithDirectoryPackagesPropsTests
{
    [Fact]
    public async Task CanUpgradePackages()
    {
        var (cwd, _, fs, command) = SetupCommand(
            new Dictionary<string, string>()
            {
                ["test0.csproj"] = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Example1" />
    <PackageReference Include="Example2" />
  </ItemGroup>

</Project>
""",
                [CliConstants.DirectoryPackagesPropsFileName] = """
<Project>

  <ItemGroup>
    <PackageReference Include="Example1" Version="4.0.0" />
    <PackageReference Include="Example2" Version="5.0.0" />
  </ItemGroup>

</Project>
""",
            },
            new PackageDictionary()
            {
                ["Example1"] = new[] { "9.0.0".ToNuGetVersion() },
                ["Example2"] = new[] { "10.0.0".ToNuGetVersion() },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            DefaultCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest,
            }
        );

        // Assert
        using var _as = new AssertionScope();

        result.Should().Be(0);

        var newPackageProps = await cwd.ReadProjectFile(
            fs,
            CliConstants.DirectoryPackagesPropsFileName
        );

        newPackageProps
            .ProjectFileToXml()
            .Should()
            .Be(
                """
                <Project>

                  <ItemGroup>
                    <PackageReference Include="Example1" Version="9.0.0" />
                    <PackageReference Include="Example2" Version="10.0.0" />
                  </ItemGroup>

                </Project>
                """
            );

        var newProjectFile = await cwd.ReadProjectFile(fs, "test0.csproj");

        newProjectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Example1" />
                    <PackageReference Include="Example2" />
                  </ItemGroup>

                </Project>
                """
            );
    }

    [Fact]
    public async Task Output_contains_Directory_Packages_props_when_found_in_solution_with_no_upgrades()
    {
        var console = new TestConsole();

        var (cwd, _fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new(
                        $"{CliConstants.DirectoryPackagesPropsFileName}",
                        Framework: Frameworks.Net8_0
                    )
                    {
                        ReferenceType = ReferenceType.PackageVersion,
                        Packages = { ("Test", "1.0") },
                    },
                    new(
                        $"nested/{CliConstants.DirectoryPackagesPropsFileName}",
                        Framework: Frameworks.Net8_0
                    )
                    {
                        ReferenceType = ReferenceType.PackageVersion,
                        Packages = { ("Test2", "1.0") },
                    },
                    new("nested/project/project.csproj", Framework: Frameworks.Unspecified)
                    {
                        Packages = [("Test2", "")],
                    },
                },
            },
            Array.Empty<MockUpgrade>(),
            console: console
        );

        // Act
        var result = await command.ExecuteAsync(
            DefaultCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
{slnRoot.PathCombine("test.sln")}
|-- {slnRoot.PathCombine("Directory.Packages.props")}
|-- {slnRoot.PathCombine("nested/Directory.Packages.props")}
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
{slnRoot.PathCombine("test.sln")}
|-- {slnRoot.PathCombine("Directory.Packages.props")}
|   `-- All packages match their latest versions.
|
|-- {slnRoot.PathCombine("nested/Directory.Packages.props")}
|   `-- All packages match their latest versions.
|
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
    `-- All packages match their latest versions.
";

        CheckUpdateCommandOutputTests.AssertOutput(console, expected);
    }

    [Fact]
    public async Task Output_contains_Directory_Packages_props_when_found_in_solution()
    {
        var console = new TestConsole();

        var (cwd, _, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new($"{CliConstants.DirectoryPackagesPropsFileName}", Framework: Frameworks.Net8_0)
                    {
                        Packages = { ("Test", "1.0") },
                    },
                    new(
                        $"nested/{CliConstants.DirectoryPackagesPropsFileName}",
                        Framework: Frameworks.Net8_0
                    )
                    {
                        Packages = { ("Test2", "1.0") },
                    },
                    new("nested/project/project.csproj", Framework: "")
                    {
                        Packages = { ("Test2", ""), ("Test3", "1.0") },
                    },
                },
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "1.5" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
                new MockUpgrade("Test2")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
                new MockUpgrade("Test3")
                {
                    Versions = { "3.0" },
                    SupportedFrameworks = { Frameworks.Net8_0 },
                },
            },
            console: console
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
{slnRoot.PathCombine("test.sln")}
|-- {slnRoot.PathCombine("Directory.Packages.props")}
|-- {slnRoot.PathCombine("nested/Directory.Packages.props")}
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
{slnRoot.PathCombine("test.sln")}
|-- {slnRoot.PathCombine("Directory.Packages.props")}
|   `-- Test   1.0.0  →  1.5.0
|
|-- {slnRoot.PathCombine("nested/Directory.Packages.props")}
|   `-- Test2  1.0.0  →  2.0.0
|
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
    `-- Test3  1.0.0  →  3.0.0


Upgrading packages in {slnRoot.PathCombine("Directory.Packages.props")}
Upgrading packages in {slnRoot.PathCombine("nested/Directory.Packages.props")}
Upgrading packages in {slnRoot.PathCombine("nested/project/project.csproj")}

Run dotnet restore to install new versions
";

        CheckUpdateCommandOutputTests.AssertOutput(console, expected);
    }
}
