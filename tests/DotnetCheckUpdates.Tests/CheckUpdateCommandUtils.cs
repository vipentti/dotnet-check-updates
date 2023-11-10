// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.NuGetUtils;
using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Core.Utils;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static DotnetCheckUpdates.Tests.ProjectFileUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests;

internal static class CheckUpdateCommandUtils
{
    public static readonly CommandContext TestCommandContext =
        new(Substitute.For<IRemainingArguments>(), "test", null);

    public static (
        string Cwd,
        string[] ProjectFilePaths,
        MockFileSystem FileSystem,
        CheckUpdateCommand Command
    ) SetupCommand(IEnumerable<MockProject> projects, IEnumerable<MockUpgrade> upgrades)
    {
        var cwd = RootedTestPath("some/path");

        var projectsWithPaths = projects.ToDictionary(
            kvp => cwd.PathCombine(kvp.ProjectPath),
            kvp => kvp.ToXml()
        );

        var fileSystem = SetupFileSystem(currentDirectory: cwd, fileContents: projectsWithPaths);

        var service = SetupMockPackages(upgrades);

        var command = CreateCommand(console: default, fileSystem, service, finder: default);

        return (cwd, projectsWithPaths.Keys.ToArray(), fileSystem, command);
    }

    public static (string Cwd, MockFileSystem FileSystem, CheckUpdateCommand Command) SetupCommand(
        MockSolution solution,
        IEnumerable<MockUpgrade> upgrades
    )
    {
        var cwd = RootedTestPath("some/path");

        var solutionFilePath = cwd.PathCombine(solution.SolutionPath);

        var solutionPath = System.IO.Path.GetDirectoryName(solutionFilePath)!;

        var projectsWithPaths = solution
            .Projects
            .ToDictionary(kvp => solutionPath.PathCombine(kvp.ProjectPath), kvp => kvp.ToXml());

        var fileContents = new Dictionary<string, string>(projectsWithPaths)
        {
            { solutionFilePath, solution.GetSolution() },
        };

        var fileSystem = SetupFileSystem(currentDirectory: cwd, fileContents: fileContents);

        var service = SetupMockPackages(upgrades);

        var command = CreateCommand(console: default, fileSystem, service, finder: default);

        return (cwd, fileSystem, command);
    }

    public static (
        string Cwd,
        string[] ProjectFilePaths,
        MockFileSystem FileSystem,
        CheckUpdateCommand Command
    ) SetupCommand(Dictionary<string, string> projects, PackageDictionary packageUpgrades)
    {
        var cwd = RootedTestPath("some/path");

        var projectsWithPaths = projects.ToDictionary(
            kvp => cwd.PathCombine(kvp.Key),
            kvp => kvp.Value
        );

        var fileSystem = SetupFileSystem(currentDirectory: cwd, fileContents: projectsWithPaths);

        var service = SetupSimpleNuGetService(packageUpgrades);

        var command = CreateCommand(console: default, fileSystem, service, finder: default);

        return (cwd, projectsWithPaths.Keys.ToArray(), fileSystem, command);
    }

    public static (
        string Cwd,
        string[] ProjectFilePaths,
        MockFileSystem FileSystem,
        CheckUpdateCommand Command
    ) SetupCommand(
        TestConsole console,
        Dictionary<string, string> projects,
        PackageDictionary packageUpgrades
    )
    {
        var cwd = RootedTestPath("some/path");

        var projectsWithPaths = projects.ToDictionary(
            kvp => cwd.PathCombine(kvp.Key),
            kvp => kvp.Value
        );

        var fileSystem = SetupFileSystem(currentDirectory: cwd, fileContents: projectsWithPaths);

        var service = SetupSimpleNuGetService(packageUpgrades);

        var command = CreateCommand(console: console, fileSystem, service, finder: default);

        return (cwd, projectsWithPaths.Keys.ToArray(), fileSystem, command);
    }

    public static CheckUpdateCommand CreateCommand(
        TestConsole? console,
        IFileSystem fileSystem,
        INuGetService nugetService,
        IFileFinder? finder
    ) =>
        new(
            console ?? new TestConsole(),
            NullLogger<CheckUpdateCommand>.Instance,
            fileSystem,
            new ProjectFileReader(fileSystem),
            packageService: new PackageUpgradeService(
                NullLogger<PackageUpgradeService>.Instance,
                nugetService
            ),
            projectDiscovery: new ProjectDiscovery(
                finder ?? new FileFinder(fileSystem),
                new TestSolutionParser(fileSystem)
            )
        );

    public static async Task AssertPackages(
        string cwd,
        IFileSystem fs,
        string projectName,
        IEnumerable<(string id, string version)> packages
    )
    {
        var project = await cwd.ReadProjectFile(fs, projectName);

        var packageVersions = packages
            .Select(it => PackageReference.From(it.id, it.version))
            .ToArray();

        using (new AssertionScope())
        {
            project.PackageReferences.Should().BeEquivalentTo(packageVersions);

            project.ProjectFileToXml().Should().Be(ProjectFileXml(packages));
        }
    }
}
