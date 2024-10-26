// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Tests.Core;
using Spectre.Console.Cli;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.ProjectFileUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandPackageUpgradeTests
{
    public static CommandContext TestCommandContext =>
        new([], Substitute.For<IRemainingArguments>(), "test", null);

    [Fact]
    public async Task DoesNotUpgradeWhenNoSupportedFrameworkIsFound()
    {
        // Arrange
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test1.csproj", Frameworks.Net5_0)
                {
                    Packages =
                    {
                        ("MinShort", "1.0.0"),
                    }
                }
            },
            new[]
            {
                new MockUpgrade("MinShort")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.Net6_0, Frameworks.Net7_0 },
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
        using var _s = new AssertionScope();

        result.Should().Be(0);
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("MinShort", "1.0.0"),
        }, framework: Frameworks.Net5_0);
    }

    [Fact]
    public async Task DoesUpgradeWhenSupportedFrameworkIsFound()
    {
        // Arrange
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test1.csproj", Frameworks.Net5_0)
                {
                    Packages =
                    {
                        ("MinShort", "1.0.0"),
                    }
                }
            },
            new[]
            {
                new MockUpgrade("MinShort")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0 },
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
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("MinShort", "1.1.0"),
        }, framework: Frameworks.Net5_0);
    }

    [Fact]
    public async Task SupportsDirectoryBuildPropsFilesWithProject()
    {
        // Arrange
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test1.csproj", Frameworks.Unspecified)
                {
                    Packages =
                    {
                        ("ProjectPackage", "1.0.0"),
                    }
                },
                new MockProject(CliConstants.DirectoryBuildPropsFileName, Frameworks.Default)
                {
                    Packages =
                    {
                        ("MinShort", "1.0.0"),
                    }
                }
            },
            new[]
            {
                new MockUpgrade("MinShort")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
                },
                new MockUpgrade("ProjectPackage")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
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
            ("MinShort", "1.1.0"),
        });
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("ProjectPackage", "1.1.0"),
        }, framework: Frameworks.Unspecified);
    }

    [Fact]
    public async Task SupportsFsharpProjects()
    {
        // Arrange
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test1.fsproj", Frameworks.Default)
                {
                    Packages =
                    {
                        ("MinShort", "1.0.0"),
                    }
                }
            },
            new[]
            {
                new MockUpgrade("MinShort")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
                },
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            new CommandContext([], Substitute.For<IRemainingArguments>(), "test", null),
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
        await AssertPackages(cwd, fileSystem, "test1.fsproj", new[]
        {
            ("MinShort", "1.1.0"),
        });
    }

    [Fact]
    public async Task UpgradesOnlyPackagesMatchingBothFilters()
    {
        // Arrange
        static MockUpgrade MakeUpgrade(string name) =>
            new(name)
            {
                Versions = { "2.0.0", },
                SupportedFrameworks = { Frameworks.NetStandard2_0, },
            };
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test.test1.csproj", Frameworks.Default)
                {
                    Packages =
                    {
                        ("Package1.Include", "1.0.0"),
                        ("Package1.Exclude", "1.0.0"),
                        ("Package2.Include", "1.0.0"),
                        ("Package2.Exclude", "1.0.0"),
                    }
                },
            },
            new[]
            {
                MakeUpgrade("Package1.Include"),
                MakeUpgrade("Package1.Exclude"),
                MakeUpgrade("Package2.Include"),
                MakeUpgrade("Package2.Exclude"),
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            new CommandContext([], Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest,
                Include = ["Package2.Include"],
                Exclude = ["*Exclude* Package1.Include"],
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test.test1.csproj", new[]
        {
            ("Package1.Include", "1.0.0"),
            ("Package1.Exclude", "1.0.0"),
            ("Package2.Include", "2.0.0"),
            ("Package2.Exclude", "1.0.0"),
        }, framework: Frameworks.Default);
    }

    [Fact]
    public async Task UpgradesOnlyPackagesMatchingIncludeFilters()
    {
        // Arrange
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test.test1.csproj", Frameworks.Default)
                {
                    Packages =
                    {
                        ("ProjectPackage.Include", "1.0.0"),
                        ("ProjectPackage.Exclude", "1.0.0"),
                    }
                },
                new MockProject("test.test2.csproj", Frameworks.Default)
                {
                    Packages =
                    {
                        ("ProjectPackage.Include", "1.0.0"),
                        ("ProjectPackage.Exclude", "1.0.0"),
                    }
                },
            },
            new[]
            {
                new MockUpgrade("ProjectPackage.Include")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
                },
                new MockUpgrade("ProjectPackage.Exclude")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
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
                Include = ["Include"],
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test.test1.csproj", new[]
        {
            ("ProjectPackage.Include", "1.1.0"),
            ("ProjectPackage.Exclude", "1.0.0"),
        }, framework: Frameworks.Default);
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test.test2.csproj", new[]
        {
            ("ProjectPackage.Include", "1.1.0"),
            ("ProjectPackage.Exclude", "1.0.0"),
        }, framework: Frameworks.Default);
    }

    [Fact]
    public async Task UpgradesOnlyPackagesNotMatchingExcludeFilters()
    {
        // Arrange
        // csharpier-ignore
        var (cwd, _, fileSystem, command) = SetupCommand(
            new[]
            {
                new MockProject("test.test1.csproj", Frameworks.Default)
                {
                    Packages =
                    {
                        ("ProjectPackage.Include", "1.0.0"),
                        ("ProjectPackage.Exclude", "1.0.0"),
                    }
                },
                new MockProject("test.test2.csproj", Frameworks.Default)
                {
                    Packages =
                    {
                        ("ProjectPackage.Include", "1.0.0"),
                        ("ProjectPackage.Exclude", "1.0.0"),
                    }
                },
            },
            new[]
            {
                new MockUpgrade("ProjectPackage.Include")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
                },
                new MockUpgrade("ProjectPackage.Exclude")
                {
                    Versions = { "1.1.0", },
                    SupportedFrameworks = { Frameworks.NetStandard2_0, },
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
                Exclude = ["Exclude"],
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test.test1.csproj", new[]
        {
            ("ProjectPackage.Include", "1.1.0"),
            ("ProjectPackage.Exclude", "1.0.0"),
        }, framework: Frameworks.Default);
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test.test2.csproj", new[]
        {
            ("ProjectPackage.Include", "1.1.0"),
            ("ProjectPackage.Exclude", "1.0.0"),
        }, framework: Frameworks.Default);
    }

    [Fact]
    public async Task PrefersLatestNonPreReleaseWhenOriginalIsNotPreRelease()
    {
        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new() { ["test1.csproj"] = ProjectFileXml(new[] { ("MinShort", "1.0.0"), }), },
            new PackageDictionary()
            {
                ["MinShort"] = new[]
                {
                    "1.0.1-aaa".ToNuGetVersion(),
                    "1.0.1".ToNuGetVersion(),
                    "1.0.1-zzz".ToNuGetVersion(),
                    "1.0.1-beta".ToNuGetVersion(),
                    "1.0.2-beta".ToNuGetVersion(),
                    "1.0.3-beta".ToNuGetVersion(),
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
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("MinShort", "1.0.1"),
        });
    }

    [Fact]
    public async Task PrefersLatestNonPreReleaseInSameVersionRange()
    {
        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new() { ["test1.csproj"] = ProjectFileXml(new[] { ("MinShort", "1.0.1-alpha2"), }), },
            new PackageDictionary()
            {
                ["MinShort"] = new[]
                {
                    "1.0.1-alpha2".ToNuGetVersion(),
                    "1.0.1-alpha".ToNuGetVersion(),
                    "1.0.1-aaa".ToNuGetVersion(),
                    "1.0.1".ToNuGetVersion(),
                    "1.0.1-zzz".ToNuGetVersion(),
                    "1.0.1-rc".ToNuGetVersion(),
                    "1.0.1-open".ToNuGetVersion(),
                    "1.0.1-beta".ToNuGetVersion(),
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
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("MinShort", "1.0.1"),
        });
    }

    [Fact]
    public async Task PrefersLatestPreReleaseWhenOriginalIsPreRelease()
    {
        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new() { ["test1.csproj"] = ProjectFileXml(new[] { ("MinShort", "1.0.0-alpha.1"), }), },
            new PackageDictionary()
            {
                ["MinShort"] = new[]
                {
                    "1.0.1-aaa".ToNuGetVersion(),
                    "1.0.1".ToNuGetVersion(),
                    "1.0.1-zzz".ToNuGetVersion(),
                    "1.0.1-beta".ToNuGetVersion(),
                    "1.0.2-beta".ToNuGetVersion(),
                    "1.0.3-beta".ToNuGetVersion(),
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
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("MinShort", "1.0.3-beta"),
        });
    }

    [Fact]
    public async Task CanUpgradePackagesInMultipleProjects()
    {
        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new()
            {
                ["test1.csproj"] = ProjectFileXml(
                    new[]
                    {
                        ("NoUpgrade1", "1.0.0"),
                        ("NoUpgrade2", "1.0.0"),
                        ("NoUpgrade3", "1.0.0"),
                        ("Upgrade1", "1.0.0"),
                        ("NoUpgrade4", "1.0.0"),
                        ("Upgrade2", "1.0.0"),
                        ("NoUpgrade5", "1.0.0"),
                        ("Upgrade3", "1.0.0"),
                    }
                ),
                ["test2.csproj"] = ProjectFileXml(
                    new[]
                    {
                        ("NoUpgrade1", "1.0.0"),
                        ("NoUpgrade5", "1.0.0"),
                        ("Upgrade3", "1.0.0"),
                    }
                ),
                ["test3.csproj"] = ProjectFileXml(
                    new[]
                    {
                        ("NoUpgrade1", "1.0.0"),
                        ("NoUpgrade5", "1.0.0"),
                        ("NoUpgrade1", "1.0.0"),
                    }
                ),
            },
            new PackageDictionary()
            {
                ["Upgrade1"] = new[] { "1.2.0".ToNuGetVersion() },
                ["Upgrade2"] = new[] { "2.0.0".ToNuGetVersion() },
                ["Upgrade3"] = new[] { "3.0.0".ToNuGetVersion() },
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

        await AssertPackages(
            cwd,
            fileSystem,
            "test1.csproj",
            new[]
            {
                ("NoUpgrade1", "1.0.0"),
                ("NoUpgrade2", "1.0.0"),
                ("NoUpgrade3", "1.0.0"),
                ("Upgrade1", "1.2.0"),
                ("NoUpgrade4", "1.0.0"),
                ("Upgrade2", "2.0.0"),
                ("NoUpgrade5", "1.0.0"),
                ("Upgrade3", "3.0.0"),
            }
        );

        await AssertPackages(
            cwd,
            fileSystem,
            "test2.csproj",
            new[] { ("NoUpgrade1", "1.0.0"), ("NoUpgrade5", "1.0.0"), ("Upgrade3", "3.0.0"), }
        );

        await AssertPackages(
            cwd,
            fileSystem,
            "test3.csproj",
            new[] { ("NoUpgrade1", "1.0.0"), ("NoUpgrade5", "1.0.0"), ("NoUpgrade1", "1.0.0"), }
        );
    }

    [Fact]
    public async Task CorrectlyUpgradesPackagesInOrder()
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
                        ("NoUpgrade1", "1.0.0"),
                        ("NoUpgrade2", "1.0.0"),
                        ("NoUpgrade3", "1.0.0"),
                        ("Upgrade1", "1.0.0"),
                        ("NoUpgrade4", "1.0.0"),
                        ("Upgrade2", "1.0.0"),
                        ("NoUpgrade5", "1.0.0"),
                        ("Upgrade3", "1.0.0"),
                    }
                ),
            }
        );

        var service = SetupSimpleNuGetService(
            new PackageDictionary()
            {
                ["Upgrade1"] = new[] { "1.2.0".ToNuGetVersion() },
                ["Upgrade2"] = new[] { "2.0.0".ToNuGetVersion() },
                ["Upgrade3"] = new[] { "3.0.0".ToNuGetVersion() },
            }
        );

        var command = CreateCommand(console: default, fileSystem, service, finder: default);

        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = UpgradeTarget.Latest
            }
        );

        result.Should().Be(0);

        // var projectFile = ProjectFileParser.ParseProjectFile(TestProjectFile(), "testfile");
        var projectFile = await new ProjectFileReader(fileSystem).ReadProjectFile(
            cwd.PathCombine("project.csproj")
        );

        projectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                ProjectFileXml(
                    new[]
                    {
                        ("NoUpgrade1", "1.0.0"),
                        ("NoUpgrade2", "1.0.0"),
                        ("NoUpgrade3", "1.0.0"),
                        ("Upgrade1", "1.2.0"),
                        ("NoUpgrade4", "1.0.0"),
                        ("Upgrade2", "2.0.0"),
                        ("NoUpgrade5", "1.0.0"),
                        ("Upgrade3", "3.0.0"),
                    }
                )
            );
    }

    [Fact]
    public async Task MaintainsVersionRangeFormats()
    {
        var content = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Exact"" Version=""[1.0]"" />
    <PackageReference Include=""MinShort"" Version=""1.0"" />
    <PackageReference Include=""MinInclusive"" Version=""[1.0,)"" />
    <PackageReference Include=""MinExclusive"" Version=""(1.0,)"" />
    <PackageReference Include=""MaxInclusive"" Version=""(,1.0]"" />
    <PackageReference Include=""MaxExclusive"" Version=""(,1.0)"" />
    <PackageReference Include=""ExactInclusive"" Version=""[1.0,2.0]"" />
    <PackageReference Include=""ExactExclusive"" Version=""(1.0,2.0)"" />
    <PackageReference Include=""ExactMinInclusive"" Version=""[1.0,2.0)"" />
    <PackageReference Include=""ExactMaxInclusive"" Version=""(1.0,2.0]"" />
  </ItemGroup>
</Project>
".TrimStart();

        var nugetVersions = new[]
        {
            "0.5".ToNuGetVersion(),
            "1.5".ToNuGetVersion(),
            "2.0".ToNuGetVersion(),
            "3.0".ToNuGetVersion(),
        };

        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new() { ["test1.csproj"] = content, },
            new PackageDictionary()
            {
                ["Exact"] = nugetVersions,
                ["MinShort"] = nugetVersions,
                ["MinInclusive"] = nugetVersions,
                ["MinExclusive"] = nugetVersions,
                ["MaxInclusive"] = nugetVersions,
                ["MaxExclusive"] = nugetVersions,
                ["ExactInclusive"] = nugetVersions,
                ["ExactExclusive"] = nugetVersions,
                ["ExactMinInclusive"] = nugetVersions,
                ["ExactMaxInclusive"] = nugetVersions,
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

        // using var _s = new AssertionScope();

        var newProjectFile = await cwd.ReadProjectFile(fileSystem, "test1.csproj");
        newProjectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Exact"" Version=""[3.0.0]"" />
    <PackageReference Include=""MinShort"" Version=""3.0.0"" />
    <PackageReference Include=""MinInclusive"" Version=""[3.0.0,)"" />
    <PackageReference Include=""MinExclusive"" Version=""(1.0,)"" />
    <PackageReference Include=""MaxInclusive"" Version=""(,1.0]"" />
    <PackageReference Include=""MaxExclusive"" Version=""(,1.0)"" />
    <PackageReference Include=""ExactInclusive"" Version=""[1.0,2.0]"" />
    <PackageReference Include=""ExactExclusive"" Version=""(1.0,2.0)"" />
    <PackageReference Include=""ExactMinInclusive"" Version=""[1.0,2.0)"" />
    <PackageReference Include=""ExactMaxInclusive"" Version=""(1.0,2.0]"" />
  </ItemGroup>
</Project>
".TrimStart()
            );
    }

    [Fact]
    public async Task SupportsPrereleases()
    {
        var content = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""MinShort"" Version=""1.0-alpha.1"" />
  </ItemGroup>
</Project>
".TrimStart();

        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new() { ["test1.csproj"] = content, },
            new PackageDictionary()
            {
                ["MinShort"] = new[]
                {
                    "1.0-alpha.1".ToNuGetVersion(),
                    "1.0-alpha.2".ToNuGetVersion(),
                    "1.0-beta.1".ToNuGetVersion(),
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

        // using var _s = new AssertionScope();

        var newProjectFile = await cwd.ReadProjectFile(fileSystem, "test1.csproj");
        newProjectFile
            .ProjectFileToXml()
            .Should()
            .Be(
                @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net5.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""MinShort"" Version=""1.0.0-beta.1"" />
  </ItemGroup>
</Project>
".TrimStart()
            );
    }

    [Theory]
    [MemberData(
        nameof(UpgradeTargetTests.UpgradeTargetCases),
        MemberType = typeof(UpgradeTargetTests)
    )]
    public async Task UpgradeTargetAffectsUpgradedPackages(
        string description,
        UpgradeTarget target,
        string version,
        string[] versions,
        string expected
    )
    {
        _ = description;

        // Arrange
        var (cwd, _, fileSystem, command) = SetupCommand(
            new() { ["test1.csproj"] = ProjectFileXml(new[] { ("UpgradeTest", version), }), },
            new PackageDictionary()
            {
                ["UpgradeTest"] = versions.ConvertAll(VersionExtensions.ToNuGetVersion),
            }
        );

        // Act
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                Target = target
            }
        );

        // Assert
        result.Should().Be(0);

        using var _s = new AssertionScope();
        // csharpier-ignore
        await AssertPackages(cwd, fileSystem, "test1.csproj", new[]
        {
            ("UpgradeTest", expected),
        });
    }
}
