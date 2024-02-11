// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandSolutionTests
{
    [Fact]
    public async Task SupportsSpecifyingSolutionPath()
    {
        // csharpier-ignore
        var (cwd, fileSystem, command) = SetupCommand(
            new MockSolution("inner/test.sln")
            {
                Projects =
                {
                    new("nested/project.csproj")
                    {
                        Packages =
                        {
                            ("Test", "1.0")
                        }
                    }
                }
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest,
                Solution = cwd.PathCombine("inner/test.sln"),
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "inner/nested/project.csproj", new[]
        {
            ("Test", "2.0.0"),
        });
    }

    [Fact]
    public async Task SupportsUpgradingMultipleProjectsInSolution()
    {
        // csharpier-ignore
        var (cwd, fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new("nested1/project1.csproj")
                    {
                        Packages =
                        {
                            ("Test", "1.0"),
                        },
                    },
                    new("nested2/project2.csproj")
                    {
                        Packages =
                        {
                            ("Test", "1.5"),
                        },
                    },
                    new("nested3/project3.csproj")
                    {
                        Packages =
                        {
                            ("Test", "3.0"),
                        },
                    },
                }
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest,
                Solution = cwd.PathCombine("test.sln"),
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested1/project1.csproj", new[]
        {
            ("Test", "2.0.0"),
        });
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested2/project2.csproj", new[]
        {
            ("Test", "2.0.0"),
        });
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested3/project3.csproj", new[]
        {
            ("Test", "3.0.0"),
        });
    }

    [Fact]
    public async Task SupportsFsharpProjectsInSolution()
    {
        var (cwd, fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects = { new("nested/project.fsproj") { Packages = { ("Test", "1.0") } } }
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested/project.fsproj", new[]
        {
            ("Test", "2.0.0"),
        });
    }

    [Fact]
    public async Task DoesNothingIfTheProjectFileDoesNotHaveCsprojSuffix()
    {
        var (cwd, fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects = { new("nested/project.notcsproj") { Packages = { ("Test", "1.0") } } }
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested/project.notcsproj", new[]
        {
            ("Test", "1.0.0"),
        });
    }

    [Fact]
    public async Task SupportsLoadingProjectsFromSolutionWithSpecificPath()
    {
        var (cwd, fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new("nested/project.csproj", Frameworks.Net5_0)
                    {
                        Packages = { ("Test", "1.0") }
                    }
                }
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested/project.csproj", new[]
        {
            ("Test", "2.0.0"),
        }, framework: Frameworks.Net5_0);
    }

    [Fact]
    public async Task SupportsSolutionsWithDirectoryBuildProps()
    {
        var (cwd, fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new($"{CliConstants.DirectoryBuildPropsFileName}", Framework: Frameworks.Net8_0)
                    {
                        Packages = { ("Test", "1.0") }
                    },
                    new(
                        $"nested/{CliConstants.DirectoryBuildPropsFileName}",
                        Framework: Frameworks.Net8_0
                    )
                    {
                        Packages = { ("Test2", "1.0") }
                    },
                    new("nested/project/project.csproj", Framework: "")
                    {
                        Packages = { ("Test3", "1.0") }
                    },
                }
            },
            new[]
            {
                new MockUpgrade("Test")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
                new MockUpgrade("Test2")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
                new MockUpgrade("Test3")
                {
                    Versions = { "2.0" },
                    SupportedFrameworks = { Frameworks.Net8_0 },
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, CliConstants.DirectoryBuildPropsFileName, new[]
        {
            ("Test", "2.0.0"),
        }, framework: Frameworks.Net8_0);
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested/" + CliConstants.DirectoryBuildPropsFileName, new[]
        {
            ("Test2", "2.0.0"),
        }, framework: Frameworks.Net8_0);
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "nested/project/project.csproj", new[]
        {
            ("Test3", "2.0.0"),
        }, framework: Frameworks.Unspecified);
    }
}
