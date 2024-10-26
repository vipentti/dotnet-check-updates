// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests.Core.ProjectModel;

using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Core.Utils;
using static DotnetCheckUpdates.Tests.TestUtils;

public class ImportTests
{
    public const string FileName = CliConstants.DirectoryBuildPropsFileName;

    public class GetImportedProjectPathTests
    {
        [Fact]
        public void CanResolveImportedMsBuildPathOfFileAbove()
        {
            var testRoot = RootedTestPath();

            var fileSystem = SetupFileSystem(
                testRoot,
                new()
                {
                    [testRoot.PathCombine($"root/{CliConstants.DirectoryBuildPropsFileName}")] = """
                    <Project></Project>
                    """,
                }
            );

            var cwd = testRoot.PathCombine("root/nested");

            var fileFinder = new FileFinder(fileSystem);

            var import = new Import(
                "$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))"
            );

            var pathOfFileAbove = import.GetImportedProjectPath(fileFinder, cwd);

            pathOfFileAbove
                .Should()
                .Be(testRoot.PathCombine($"root/{CliConstants.DirectoryBuildPropsFileName}"));
        }

        [Theory]
        [InlineData("root", "$(MSBuildThisFileDirectory)../", "")]
        [InlineData("root", "$(MSBuildThisFileDirectory)", "root")]
        [InlineData("root", ".", "root")]
        [InlineData("root/nested", "$(MSBuildThisFileDirectory)../", "root/")]
        [InlineData("root/nested", "$(MSBuildThisFileDirectory)", "root/nested")]
        [InlineData("root/nested", ".", "root/nested")]
        [InlineData("root/nested/nested1", "$(MSBuildThisFileDirectory)", "root/nested")]
        [InlineData("root/nested/nested1", ".", "root/nested")]
        public void StartingDirectoryIsUsed(string start, string importPath, string expectedPath)
        {
            var testRoot = RootedTestPath();

            var fileSystem = SetupFileSystem(
                testRoot,
                new()
                {
                    [testRoot.PathCombine($"root/{FileName}")] = """
                    <Project></Project>
                    """,
                    [testRoot.PathCombine($"root/nested/{FileName}")] = """
                    <Project></Project>
                    """,
                }
            );

            var cwd = testRoot.PathCombine(start);

            fileSystem.Directory.SetCurrentDirectory(cwd);

            var fileFinder = new FileFinder(fileSystem);

            var import = new Import(
                $"$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '{importPath}'))"
            );

            var pathOfFileAbove = import.GetImportedProjectPath(fileFinder, cwd);

            if (expectedPath.Length == 0)
            {
                pathOfFileAbove.Should().BeEmpty();
            }
            else
            {
                pathOfFileAbove.Should().Be(testRoot.PathCombine($"{expectedPath}/{FileName}"));
            }
        }

        [Fact]
        public void ReturnsEmptyWhenPathOfFileAboveWasNotFound()
        {
            var testRoot = RootedTestPath();

            var fileSystem = SetupFileSystem(testRoot, []);

            var cwd = testRoot.PathCombine("root/nested");

            var fileFinder = new FileFinder(fileSystem);

            var import = new Import(
                "$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))"
            );

            var pathOfFileAbove = import.GetImportedProjectPath(fileFinder, cwd);

            pathOfFileAbove.Should().BeEmpty();
        }
    }
}
