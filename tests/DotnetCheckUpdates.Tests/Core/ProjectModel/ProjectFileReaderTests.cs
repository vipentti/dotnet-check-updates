// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using static DotnetCheckUpdates.Tests.ProjectFileUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Core.ProjectModel;

public class ProjectFileReaderTests
{
    [Fact]
    public async Task CanReadValidProjectFile()
    {
        // Arrange
        var cwd = RootedTestPath();

        var projectPath = cwd.PathCombine("some/path/project.csproj");

        var fileSystem = SetupFileSystem(
            currentDirectory: cwd,
            fileContents: new()
            {
                [projectPath] = ProjectFileXml(new[] { ("Test", "1.0") }, framework: "net7.0"),
            }
        );

        var reader = new ProjectFileReader(fileSystem);

        var content = await reader.ReadProjectFile(projectPath);

        content.FilePath.Should().Be(projectPath);
        content
            .TargetFrameworks.Should()
            .ContainEquivalentOf("net7.0".ToNuGetFramework())
            .And.HaveCount(1);
    }
}
