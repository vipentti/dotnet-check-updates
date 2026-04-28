// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.NuGetUtils;
using NuGet.Frameworks;
using Spectre.Console.Testing;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.Commands.CheckUpdateCommandOutputTests;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandInteractiveOutputTests
{
    [Fact]
    public async Task InteractiveOutputIncludesConditionContextForDuplicatePackageNames()
    {
        var console = new TestConsole().Interactive();
        var cwd = RootedTestPath("some/path");

        var fileSystem = SetupFileSystem(
            currentDirectory: cwd,
            fileContents: new()
            {
                [cwd.PathCombine("project.csproj")] = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Example" />
                  </ItemGroup>
                </Project>
                """,
                [cwd.PathCombine(CliConstants.DirectoryPackagesPropsFileName)] = """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageVersion Include="Example" Version="1.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
                    <PackageVersion Include="Example" Version="1.5.0" Condition="'$(TargetFramework)' == 'net9.0'" />
                  </ItemGroup>
                </Project>
                """,
            }
        );

        var service = Substitute.For<INuGetService>();

        service
            .GetPackageVersionsAsync("Example", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new[] { "2.0.0".ToNuGetVersion(), "3.0.0".ToNuGetVersion() }.AsEnumerable()
                )
            );

        service
            .GetSupportedFrameworksAsync("Example", "2.0.0", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(ImmutableHashSet.Create(FrameworkConstants.CommonFrameworks.Net80))
            );

        service
            .GetSupportedFrameworksAsync("Example", "3.0.0", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(ImmutableHashSet.Create(FrameworkConstants.CommonFrameworks.Net90))
            );

        var command = CreateCommand(
            console: console,
            fileSystem,
            service,
            finder: default,
            solutionFileFormat: SolutionFileFormat.Sln
        );

        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        var result = await command
            .AsICommand()
            .ExecuteAsync(
                TestCommandContext,
                new CheckUpdateCommand.Settings
                {
                    Cwd = cwd,
                    Upgrade = true,
                    AsciiTree = true,
                    ShowAbsolute = true,
                    Interactive = true,
                    HideProgressAfterComplete = true,
                },
                CancellationToken.None
            );

        using var scope = new AssertionScope();
        result.Should().Be(0);

        var output = console.Output;
        output.Should().Contain("Example (net8.0) 1.0.0  → 2.0.0");
        output.Should().Contain("Example (net9.0) 1.5.0  → 3.0.0");
        output.Should().Contain("Example (net8.0)  1.0.0  →  2.0.0");
        output.Should().Contain("Example (net9.0)  1.5.0  →  3.0.0");
    }

    [Theory]
    [InlineData("test.sln", SolutionFileFormat.Sln)]
    [InlineData("test.slnx", SolutionFileFormat.Slnx)]
    public async Task OutputsMessageWhenNoMatchingPackages(
        string solutionFileName,
        SolutionFileFormat solutionFileFormat
    )
    {
        // Arrange
        var console = new TestConsole().Interactive();

        var (cwd, _, command) = SetupCommand(
            new MockSolution(solutionFileName, solutionFileFormat)
            {
                Projects =
                {
                    new("nested/project/project.csproj", Framework: Frameworks.Default)
                    {
                        Packages = { ("Test3", "1.0") },
                    },
                },
            },
            new[]
            {
                new MockUpgrade("Test3")
                {
                    Versions = { "3.0" },
                    SupportedFrameworks = { Frameworks.Net8_0 },
                },
            },
            console: console
        );

        // Act
        var result = await command
            .AsICommand()
            .ExecuteAsync(
                TestCommandContext,
                new CheckUpdateCommand.Settings
                {
                    Cwd = cwd,
                    Upgrade = true,
                    AsciiTree = true,
                    ShowAbsolute = true,
                    Interactive = true,
                    Include = ["ShouldNotMatchAnything"],
                },
                CancellationToken.None
            );

        // Assert
        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
No packages matched provided filters.
";

        AssertOutput(console, expected);
    }

    [Theory]
    [InlineData("test.sln", SolutionFileFormat.Sln)]
    [InlineData("test.slnx", SolutionFileFormat.Slnx)]
    public async Task SupportsSelectingPackagesToUpgradeUsingInput(
        string solutionFileName,
        SolutionFileFormat solutionFileFormat
    )
    {
        // Arrange
        var console = new TestConsole().Interactive();

        var (cwd, _, command) = SetupCommand(
            new MockSolution(solutionFileName, solutionFileFormat)
            {
                Projects =
                {
                    new("nested/project/project.csproj", Framework: Frameworks.Default)
                    {
                        Packages = { ("Test3", "1.0") },
                    },
                },
            },
            new[]
            {
                new MockUpgrade("Test3")
                {
                    Versions = { "3.0" },
                    SupportedFrameworks = { Frameworks.Net8_0 },
                },
            },
            console: console
        );

        // Act
        // Select all projects & packages
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        var cmdTask = command
            .AsICommand()
            .ExecuteAsync(
                TestCommandContext,
                new CheckUpdateCommand.Settings
                {
                    Cwd = cwd,
                    Upgrade = true,
                    AsciiTree = true,
                    ShowAbsolute = true,
                    Interactive = true,
                    HideProgressAfterComplete = true,
                },
                CancellationToken.None
            );

        var result = await cmdTask;

        // Assert
        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
Choose which packages to update

> [ ] {slnRoot / solutionFileName}
    [ ] {slnRoot / "nested/project/project.csproj"}
      [ ] Test3 1.0.0  → 3.0.0

(Press <space> to select, <enter> to accept, <ctrl + c> to cancel)Choose which packages to update

> [X] {slnRoot / solutionFileName}
    [X] {slnRoot / "nested/project/project.csproj"}
      [X] Test3 1.0.0  → 3.0.0

(Press <space> to select, <enter> to accept, <ctrl + c> to cancel)Upgrading selected packages
{slnRoot / solutionFileName}
`-- {slnRoot / "nested/project/project.csproj"}
    `-- Test3  1.0.0  →  3.0.0


Upgrading packages in {slnRoot / "nested/project/project.csproj"}

Run dotnet restore to install new versions
";

        AssertOutput(console, expected);
    }

    [Theory]
    [InlineData("test.sln", SolutionFileFormat.Sln)]
    [InlineData("test.slnx", SolutionFileFormat.Slnx)]
    public async Task OutputsMessageWhenAllPackagesMatchTheirLatestVersion(
        string solutionFileName,
        SolutionFileFormat solutionFileFormat
    )
    {
        // Arrange
        var console = new TestConsole().Interactive();

        var (cwd, _, command) = SetupCommand(
            new MockSolution(solutionFileName, solutionFileFormat)
            {
                Projects =
                {
                    new("nested/project/project.csproj", Framework: Frameworks.Default)
                    {
                        Packages = { ("Test3", "3.0") },
                    },
                },
            },
            new[]
            {
                new MockUpgrade("Test3")
                {
                    Versions = { "3.0" },
                    SupportedFrameworks = { Frameworks.Net8_0 },
                },
            },
            console: console
        );

        // Act

        var cmdTask = command
            .AsICommand()
            .ExecuteAsync(
                TestCommandContext,
                new CheckUpdateCommand.Settings
                {
                    Cwd = cwd,
                    Upgrade = true,
                    AsciiTree = true,
                    ShowAbsolute = true,
                    Interactive = true,
                    HideProgressAfterComplete = true,
                },
                CancellationToken.None
            );

        var result = await cmdTask;

        // Assert
        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
All packages match their latest versions.
";

        AssertOutput(console, expected);
    }
}
