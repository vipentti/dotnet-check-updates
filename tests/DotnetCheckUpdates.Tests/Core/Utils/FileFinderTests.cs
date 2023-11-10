// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Utils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Core.Utils;

public class FileFinderTests
{
    [Fact]
    public async Task CanGetMatchingPaths()
    {
        // Arrange
        var cwd = RootedTestPath();

        var fileSystem = SetupFileSystem(
            currentDirectory: cwd,
            fileContents: new()
            {
                [cwd.PathCombine("some/path/project.csproj")] = "test-content",
                [cwd.PathCombine("some/path/project.cs")] = "not-project",
            }
        );

        var reader = new FileFinder(fileSystem);

        var projectFiles = await reader.GetMatchingPaths(cwd.PathCombine("some"), "**/*.csproj");

        projectFiles.Should().HaveCount(1);
        projectFiles
            .Should()
            .SatisfyRespectively(
                fst => fst.Should().Be(RootedTestPath("some/path/project.csproj"))
            );
    }

    [Fact]
    public async Task GetMatchingPathsRespectsGitignore()
    {
        const string gitignore =
            @"
ignored/
            ";
        // Arrange
        var cwd = RootedTestPath();

        var fileSystem = SetupFileSystem(
            currentDirectory: cwd,
            fileContents: new()
            {
                { RootedTestPath("some/path/project.csproj"), "test" },
                { RootedTestPath("some/path/nested/project.csproj"), "test" },
                { RootedTestPath("some/path/project.cs"), "not-project" },
                { RootedTestPath("some/ignored/ignored.csproj"), "ignored project" },
                { RootedTestPath("some/.gitignore"), gitignore.Trim() },
            }
        );

        var reader = new FileFinder(fileSystem);

        var projectFiles = await reader.GetMatchingPaths(cwd.PathCombine("some"), "**/*.csproj");

        projectFiles.Should().HaveCount(2);
        projectFiles
            .Should()
            .SatisfyRespectively(
                it => it.Should().Be(RootedTestPath("some/path/project.csproj")),
                it => it.Should().Be(RootedTestPath("some/path/nested/project.csproj"))
            );
    }
}
