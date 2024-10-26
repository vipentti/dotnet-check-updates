// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Commands.CheckUpdate;
using Spectre.Console.Testing;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.Commands.CheckUpdateCommandOutputTests;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandInteractiveOutputTests
{
    [Fact]
    public async Task OutputsMessageWhenNoMatchingPackages()
    {
        // Arrange
        var console = new TestConsole().Interactive();

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
                Interactive = true,
                Include = ["ShouldNotMatchAnything"],
            }
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

    [Fact]
    public async Task SupportsSelectingPackagesToUpgradeUsingInput()
    {
        // Arrange
        var console = new TestConsole().Interactive();

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
        // Select all projects & packages
        console.Input.PushKey(ConsoleKey.Spacebar);
        console.Input.PushKey(ConsoleKey.Enter);

        var cmdTask = command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
                Interactive = true,
                HideProgressAfterComplete = true,
            }
        );

        var result = await cmdTask;

        // Assert
        using var scope = new AssertionScope();
        result.Should().Be(0);

        var slnRoot = cwd;

        var expected =
            $@"
Choose which packages to update

> [ ] {slnRoot / "test.sln"}
    [ ] {slnRoot / "nested/project/project.csproj"}
      [ ] Test3 1.0.0  → 3.0.0

(Press <space> to select, <enter> to accept, <ctrl + c> to cancel)Choose which packages to update

> [X] {slnRoot / "test.sln"}
    [X] {slnRoot / "nested/project/project.csproj"}
      [X] Test3 1.0.0  → 3.0.0

(Press <space> to select, <enter> to accept, <ctrl + c> to cancel)Upgrading selected packages
{slnRoot / "test.sln"}
`-- {slnRoot / "nested/project/project.csproj"}
    `-- Test3  1.0.0  →  3.0.0


Upgrading packages in {slnRoot / "nested/project/project.csproj"}

Run dotnet restore to install new versions
";

        AssertOutput(console, expected);
    }

    [Fact]
    public async Task OutputsMessageWhenAllPackagesMatchTheirLatestVersion()
    {
        // Arrange
        var console = new TestConsole().Interactive();

        var (cwd, _, command) = SetupCommand(
            new MockSolution("test.sln")
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

        var cmdTask = command.ExecuteAsync(
            TestCommandContext,
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
                Interactive = true,
                HideProgressAfterComplete = true,
            }
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
