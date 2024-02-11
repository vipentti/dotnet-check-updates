// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Core.Utils;
using NuGet.Versioning;
using Spectre.Console.Cli;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.ProjectFileUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public partial class CheckUpdateCommandTests
{
    public static readonly CommandContext DefaultCommandContext =
        new(Substitute.For<IRemainingArguments>(), "test", null);

    [Fact]
    public async Task DefaultFinderPattern()
    {
        // Arrange
        var cwd = RootedTestPath("some/path");

        var fileSystem = SetupFileSystem(currentDirectory: cwd, fileContents: []);

        var service = SetupMockNuGetService();

        var finder = Substitute.For<IFileFinder>();

        finder
            .GetMatchingPaths(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(Task.FromResult(Enumerable.Empty<string>()));

        var command = CreateCommand(console: default, fileSystem, service, finder);

        // Act
        var result = await command.ExecuteAsync(
            DefaultCommandContext,
            new CheckUpdateCommand.Settings { Cwd = cwd, Recurse = false, }
        );

        // Assert

        await finder
            .Received()
            .GetMatchingPaths(
                Arg.Is(cwd),
                Arg.Is<IEnumerable<string>>(it =>
                    it.SequenceEqual(new[] { "*.csproj", "*.fsproj" })
                )
            );
    }

    [Theory]
    [MemberData(nameof(RecursionDepthPatternData))]
    public async Task RecursionDepthPatterns(int depth, string[] expected)
    {
        // Arrange
        var cwd = RootedTestPath("some/path");

        var fileSystem = SetupFileSystem(currentDirectory: cwd, fileContents: []);

        var service = SetupMockNuGetService();

        var finder = Substitute.For<IFileFinder>();

        finder
            .GetMatchingPaths(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(Task.FromResult(Enumerable.Empty<string>()));

        var command = CreateCommand(console: default, fileSystem, service, finder);

        var result = await command.ExecuteAsync(
            new CommandContext(Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Recurse = true,
                Depth = depth,
            }
        );

        result.Should().Be(0);

        await finder
            .Received()
            .GetMatchingPaths(
                Arg.Is(cwd),
                Arg.Is<IEnumerable<string>>(it => it.SequenceEqual(expected))
            );
    }

    public static readonly TheoryData<int, string[]> RecursionDepthPatternData =
        new()
        {
            { 0, new[] { "**/*.csproj", "**/*.fsproj" } },
            { 1, new[] { "*.csproj", "*.fsproj", "*/*.csproj", "*/*.fsproj" } },
            {
                2,
                new[]
                {
                    "*.csproj",
                    "*.fsproj",
                    "*/*.csproj",
                    "*/*.fsproj",
                    "*/*/*.csproj",
                    "*/*/*.fsproj",
                }
            },
            {
                3,
                new[]
                {
                    "*.csproj",
                    "*.fsproj",
                    "*/*.csproj",
                    "*/*.fsproj",
                    "*/*/*.csproj",
                    "*/*/*.fsproj",
                    "*/*/*/*.csproj",
                    "*/*/*/*.fsproj",
                }
            },
            {
                4,
                new[]
                {
                    "*.csproj",
                    "*.fsproj",
                    "*/*.csproj",
                    "*/*.fsproj",
                    "*/*/*.csproj",
                    "*/*/*.fsproj",
                    "*/*/*/*.csproj",
                    "*/*/*/*.fsproj",
                    "*/*/*/*/*.csproj",
                    "*/*/*/*/*.fsproj",
                }
            },
        };

    [Fact]
    public async Task Writes_Upgraded_ProjectFile()
    {
        // Arrange
        var cwd = RootedTestPath("some/path");

        var fileSystem = SetupFileSystem(
            currentDirectory: cwd,
            fileContents: new()
            {
                [cwd.PathCombine("project.csproj")] = ProjectFileXml(
                    new[]
                    {
                        PackageReference.From("Flurl", "3.0.1"),
                        PackageReference.From("Flurl.Http", "3.0.1"),
                        PackageReference.From("SomePackage", "4.*"),
                    }
                ),
            }
        );

        var service = SetupMockNuGetService(
            BasicMockPackage("Flurl", "3.2.0"),
            BasicMockPackage("Flurl.Http", "3.2.1")
        );

        var command = CreateCommand(console: default, fileSystem, service, finder: default);

        var result = await command.ExecuteAsync(
            new CommandContext(Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings { Cwd = cwd, Upgrade = true, }
        );

        result.Should().Be(0);

        // var projectFile = ProjectFileParser.ParseProjectFile(TestProjectFile(), "testfile");
        var projectFile = await new ProjectFileReader(fileSystem).ReadProjectFile(
            cwd.PathCombine("project.csproj")
        );

        projectFile
            .FindPackage(pkg => pkg.Name == "Flurl")!
            .GetVersionString()
            .Should()
            .Be("3.2.0");

        projectFile
            .FindPackage(pkg => pkg.Name == "Flurl.Http")!
            .GetVersionString()
            .Should()
            .Be("3.2.1");
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", "1.0.0")]
    [InlineData("1.0.0", "1.0.1", "1.0.[green]1[/]")]
    [InlineData("1.0.0", "1.1.1", "1.[cyan]1.1[/]")]
    [InlineData("1.0.0", "2.1.1", "[red]2.1.1[/]")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.2", "1.0.0-[fuchsia]alpha.2[/]")]
    [InlineData("1.0.0-alpha.1", "1.0.1", "1.0.[green]1[/]")]
    [InlineData("[1.0.0,)", "[1.0.0,)", "[[1.0.0,)")]
    [InlineData("[1.0.0,)", "[1.0.1,)", "[[1.0.[green]1,)[/]")]
    [InlineData("[1.0.0,)", "[1.1.1,)", "[[1.[cyan]1.1,)[/]")]
    [InlineData("[1.0.0,)", "[2.1.1,)", "[red][[2.1.1,)[/]")]
    [InlineData("[1.0.0-alpha.1,)", "[1.0.0-alpha.2,)", "[[1.0.0-[fuchsia]alpha.2,)[/]")]
    [InlineData("[1.0.0-alpha.1,)", "[1.0.1,)", "[[1.0.[green]1,)[/]")]
    public void GetUpgradedVersionStringReturnsExpected(string lhs, string rhs, string expected)
    {
        var lhsRange = VersionRange.Parse(lhs);
        var rhsRange = VersionRange.Parse(rhs);

        var lhsRef = new PackageReference("", lhsRange);
        var rhsRef = new PackageReference("", rhsRange);

        var result = CheckUpdateCommandHelpers.GetUpgradedVersionString(lhsRef, rhsRef);

        result.Should().Be(expected);
    }
}
