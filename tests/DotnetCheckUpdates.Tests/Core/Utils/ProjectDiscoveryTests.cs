// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Core.Utils;

public class ProjectDiscoveryTests
{
    public const string FileName = CliConstants.DirectoryBuildPropsFileName;

    [Fact]
    public async Task FindsDirectoryBuildPropsFileInDirectoryOfProjectWithDefaultOptions()
    {
        // Arrange
        var testRoot = RootedTestPath();

        var fileSystem = SetupFileSystem(
            currentDirectory: testRoot.PathCombine("some/path"),
            fileContents: new()
            {
                [testRoot.PathCombine("some/path/project.csproj")] = "test-content",
                [testRoot.PathCombine($"some/path/{FileName}")] = "test-content",
                [testRoot.PathCombine($"some/{FileName}")] = "not-found",
                [testRoot.PathCombine($"{FileName}")] = "not-found",
            }
        );

        var sut = new ProjectDiscovery(
            NullLogger<ProjectDiscovery>.Instance,
            new FileFinder(fileSystem),
            new TestSolutionParser(fileSystem)
        );

        // Act

        var result = await sut.DiscoverProjectsAndSolutions(
            new() { Cwd = fileSystem.Directory.GetCurrentDirectory() }
        );

        // Asssert
        using var scope = new AssertionScope();

        result.ProjectFiles.Should().HaveCount(2);
        result
            .ProjectFiles.Should()
            .BeEquivalentTo(
                [
                    testRoot.PathCombine($"some/path/{FileName}"),
                    testRoot.PathCombine("some/path/project.csproj"),
                ]
            );
    }

    [Fact]
    public async Task FindDirectoryBuildPropsFilesWhenDiscoveringSolution()
    {
        // Arrange
        var testRoot = RootedTestPath();

        var solution = new MockSolution("solution/test.sln")
        {
            Projects =
            {
                new MockProject("src/project1/project1.csproj"),
                new MockProject("src/project2/project2.csproj"),
                new MockProject("tests/project3/project3.csproj"),
            },
        };

        var fileSystem = SetupFileSystem(
            currentDirectory: testRoot.PathCombine("solution"),
            fileContents: new(solution.GetMockFiles(testRoot))
            {
                [testRoot.PathCombine($"{FileName}")] = "not-found",
                [testRoot.PathCombine($"some/{FileName}")] = "not-found",
                [testRoot.PathCombine($"solution/not-found/{FileName}")] = "not-found",

                [testRoot.PathCombine($"solution/{FileName}")] = "found",
                [testRoot.PathCombine($"solution/src/{FileName}")] = "found",
                [testRoot.PathCombine($"solution/src/project2/{FileName}")] = "found",
                [testRoot.PathCombine($"solution/tests/{FileName}")] = "found",
            }
        );

        var sut = new ProjectDiscovery(
            NullLogger<ProjectDiscovery>.Instance,
            new FileFinder(fileSystem),
            new TestSolutionParser(fileSystem)
        );

        // Act

        var result = await sut.DiscoverProjectsAndSolutions(
            new() { Cwd = fileSystem.Directory.GetCurrentDirectory() }
        );

        // Asssert
        using var scope = new AssertionScope();

        result.ProjectFiles.Should().HaveCount(7);
        result
            .ProjectFiles.Should()
            .BeEquivalentTo(
                [
                    testRoot.PathCombine($"solution/{FileName}"),
                    testRoot.PathCombine($"solution/src/{FileName}"),
                    testRoot.PathCombine("solution/src/project1/project1.csproj"),
                    testRoot.PathCombine($"solution/src/project2/{FileName}"),
                    testRoot.PathCombine("solution/src/project2/project2.csproj"),
                    testRoot.PathCombine($"solution/tests/{FileName}"),
                    testRoot.PathCombine("solution/tests/project3/project3.csproj"),
                ]
            );
    }
}
