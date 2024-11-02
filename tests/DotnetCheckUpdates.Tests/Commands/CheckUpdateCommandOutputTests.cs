// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.ProjectFileUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandOutputTests
{
    [Fact]
    public async Task OutputsToAnsiConsoleWhenNothingWasUpgraded()
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
            BasicMockPackage("Flurl", "3.0.1"),
            BasicMockPackage("Flurl.Http", "3.0.1")
        );

        var console = new TestConsole();

        var command = CreateCommand(console: console, fileSystem, service, finder: default);

        var result = await command.ExecuteAsync(
            new CommandContext([], Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        result.Should().Be(0);

        var expected =
            $@"
Projects
`-- {cwd.PathCombine("project.csproj")}
Projects
`-- {cwd.PathCombine("project.csproj")}
    `-- All packages match their latest versions.

";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task Outputs_To_AnsiConsole_When_Upgrading()
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

        var console = new TestConsole();

        var command = CreateCommand(console: console, fileSystem, service, finder: default);

        var result = await command.ExecuteAsync(
            new CommandContext([], Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        result.Should().Be(0);

        var expected =
            $@"
Projects
`-- {cwd.PathCombine("project.csproj")}
Projects
`-- {cwd.PathCombine("project.csproj")}
    `-- Flurl        3.0.1  →  3.2.0
        Flurl.Http   3.0.1  →  3.2.1


Upgrading packages in {cwd.PathCombine("project.csproj")}

Run dotnet restore to install new versions
";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task Outputs_When_No_Packages_Were_Matched_With_Include()
    {
        // Arrange
        var console = new TestConsole();

        var (cwd, _, command) = SetupCommand(
            new MockSolution("test.sln")
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
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
                Include = ["ShouldNotMatchAnything"],
            }
        );

        // Assert
        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
{slnRoot.PathCombine("test.sln")}
`-- {slnRoot.PathCombine("nested/project/project.csproj")}

No packages matched provided filters.
";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task Outputs_When_No_Packages_Were_Matched_With_Exclude()
    {
        // Arrange
        var console = new TestConsole();

        var (cwd, _, command) = SetupCommand(
            new MockSolution("test.sln")
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
        var result = await command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
                Exclude = ["*"],
            }
        );

        // Assert
        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
{slnRoot.PathCombine("test.sln")}
`-- {slnRoot.PathCombine("nested/project/project.csproj")}

No packages matched provided filters.
";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task Output_contains_Directory_Build_props_when_found_in_solution()
    {
        var console = new TestConsole();

        var (cwd, _, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new($"{CliConstants.DirectoryBuildPropsFileName}", Framework: Frameworks.Net8_0)
                    {
                        Packages = { ("Test", "1.0") },
                    },
                    new(
                        $"nested/{CliConstants.DirectoryBuildPropsFileName}",
                        Framework: Frameworks.Net8_0
                    )
                    {
                        Packages = { ("Test2", "1.0") },
                    },
                    new("nested/project/project.csproj", Framework: "")
                    {
                        Packages = { ("Test3", "1.0") },
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
|-- {slnRoot.PathCombine("Directory.Build.props")}
|-- {slnRoot.PathCombine("nested/Directory.Build.props")}
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
{slnRoot.PathCombine("test.sln")}
|-- {slnRoot.PathCombine("Directory.Build.props")}
|   `-- Test   1.0.0  →  1.5.0
|
|-- {slnRoot.PathCombine("nested/Directory.Build.props")}
|   `-- Test2  1.0.0  →  2.0.0
|
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
    `-- Test3  1.0.0  →  3.0.0


Upgrading packages in {slnRoot.PathCombine("Directory.Build.props")}
Upgrading packages in {slnRoot.PathCombine("nested/Directory.Build.props")}
Upgrading packages in {slnRoot.PathCombine("nested/project/project.csproj")}

Run dotnet restore to install new versions
";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task Output_contains_Directory_Build_props_when_found_in_solution_with_no_upgrades()
    {
        var console = new TestConsole();

        var (cwd, _fileSystem, command) = SetupCommand(
            new MockSolution("test.sln")
            {
                Projects =
                {
                    new($"{CliConstants.DirectoryBuildPropsFileName}", Framework: Frameworks.Net8_0)
                    {
                        Packages = { ("Test", "1.0") },
                    },
                    new(
                        $"nested/{CliConstants.DirectoryBuildPropsFileName}",
                        Framework: Frameworks.Net8_0
                    )
                    {
                        Packages = { ("Test2", "1.0") },
                    },
                    new("nested/project/project.csproj", Framework: Frameworks.Unspecified)
                    {
                        Packages = { ("Test3", "1.0") },
                    },
                },
            },
            Array.Empty<MockUpgrade>(),
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
|-- {slnRoot.PathCombine("Directory.Build.props")}
|-- {slnRoot.PathCombine("nested/Directory.Build.props")}
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
{slnRoot.PathCombine("test.sln")}
|-- {slnRoot.PathCombine("Directory.Build.props")}
|   `-- All packages match their latest versions.
|
|-- {slnRoot.PathCombine("nested/Directory.Build.props")}
|   `-- All packages match their latest versions.
|
`-- {slnRoot.PathCombine("nested/project/project.csproj")}
    `-- All packages match their latest versions.
";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task OutputIsAlignedWhenListing()
    {
        var console = new TestConsole();

        var (cwd, _, _, command) = SetupCommand(
            console,
            new Dictionary<string, string>()
            {
                ["test0.csproj"] = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Nuke.Common" Version="11.0.6" />
    <PackageReference Include="Nuke.Components" Version="12.0.6" />
    <PackageReference Include="Vipentti.Nuke.Components" Version="13.3.1" />
  </ItemGroup>

</Project>

""",
                ["test1.csproj"] = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="6.0.0" />
    <PackageReference Include="System.Net.Http.Json" Version="6.0.1" />
  </ItemGroup>

</Project>
""",
            },
            new PackageDictionary()
            {
                ["Microsoft.Extensions.Caching.Memory"] = new[] { "9.0.0".ToNuGetVersion() },
                ["System.Net.Http.Json"] = new[] { "10.0.0".ToNuGetVersion() },
            }
        );

        var result = await command.ExecuteAsync(
            new CommandContext([], Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = false,
                List = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        result.Should().Be(0);

        var expected =
            $@"
Projects
|-- {cwd.PathCombine("test0.csproj")}
`-- {cwd.PathCombine("test1.csproj")}
Projects
|-- {cwd.PathCombine("test0.csproj")}
|   `-- Nuke.Common                                     11.0.6
|       Nuke.Components                                 12.0.6
|       Vipentti.Nuke.Components                        13.3.1
|
`-- {cwd.PathCombine("test1.csproj")}
    `-- Microsoft.Extensions.Caching.Memory  6.0.0   →  9.0.0
        System.Net.Http.Json                 6.0.1   →  10.0.0


Run dotnet-check-updates --cwd {cwd} -u to upgrade
";
        AssertOutput(console, expected);
    }

    internal static void AssertOutput(TestConsole console, string expected)
    {
        var actualOutput = console.Output.Trim().Replace("\r\n", "\n");
        var expectedOutput = expected.Trim().Replace("\r\n", "\n");

        var actualLines = actualOutput
            .Split("\n", StringSplitOptions.None)
            .Select(it => it.TrimEnd())
            .ToArray();
        var expectedLines = expectedOutput
            .Split("\n", StringSplitOptions.None)
            .Select(it => it.TrimEnd())
            .ToArray();

        using (new AssertionScope())
        {
            var actualLinesText = string.Join("\n", actualLines);
            var expectedLinesText = string.Join("\n", expectedLines);

            actualLines
                .Should()
                .BeEquivalentTo(
                    expectedLines,
                    because: $"\nActual:\n{actualLinesText}\nExpected:\n{expectedLinesText}\n"
                );
        }
    }
}
