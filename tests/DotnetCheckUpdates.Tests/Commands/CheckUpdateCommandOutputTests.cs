// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Text.Json;
using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using FluentAssertions.Execution;
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
            new CommandContext(Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        result.Should().Be(0);

        var expected = @"
Projects
`-- C:\some\path\project.csproj
Projects
`-- C:\some\path\project.csproj
    `-- All packages match their latest versions.

".Trim().Replace("\r\n", "\n");

        var escapedOutput = JsonSerializer.Serialize(console.Output.Trim());
        var escapedExpect = JsonSerializer.Serialize(expected);

        using (new AssertionScope())
        {
            escapedOutput.Should().Be(escapedExpect);
            console.Output.Trim().Should().Be(expected);
        }
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
            new CommandContext(Substitute.For<IRemainingArguments>(), "test", null),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd,
                Upgrade = true,
                AsciiTree = true,
                ShowAbsolute = true,
            }
        );

        result.Should().Be(0);

        var expected = $@"
Projects
`-- C:\some\path\project.csproj
Projects
`-- C:\some\path\project.csproj
    `-- Flurl        3.0.1  →  3.2.0
        Flurl.Http   3.0.1  →  3.2.1
                                    

Upgrading packages in {cwd.PathCombine("project.csproj")}

Run dotnet restore to install new versions
".Trim().Replace("\r\n", "\n");

        var escapedOutput = JsonSerializer.Serialize(console.Output.Trim());
        var escapedExpect = JsonSerializer.Serialize(expected);

        using (new AssertionScope())
        {
            escapedOutput.Should().Be(escapedExpect);
            console.Output.Trim().Should().Be(expected);
        }
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
                ["Microsoft.Extensions.Caching.Memory"] = new[] { "9.0.0".ToNuGetVersion(), },
                ["System.Net.Http.Json"] = new[] { "10.0.0".ToNuGetVersion(), },
            }
        );

        var result = await command.ExecuteAsync(
            new CommandContext(Substitute.For<IRemainingArguments>(), "test", null),
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

        var expected = @"
Projects
|-- C:\some\path\test0.csproj
`-- C:\some\path\test1.csproj
Projects
|-- C:\some\path\test0.csproj
|   `-- Nuke.Common                                     11.0.6
|       Nuke.Components                                 12.0.6
|       Vipentti.Nuke.Components                        13.3.1
|                                                             
`-- C:\some\path\test1.csproj
    `-- Microsoft.Extensions.Caching.Memory  6.0.0   →  9.0.0 
        System.Net.Http.Json                 6.0.1   →  10.0.0
                                                              

Run dotnet-check-updates --cwd C:\some\path -u to upgrade
".Trim().Replace("\r\n", "\n");

        var escapedOutput = JsonSerializer.Serialize(console.Output.Trim());
        var escapedExpect = JsonSerializer.Serialize(expected);

        using (new AssertionScope())
        {
            escapedOutput.Should().Be(escapedExpect);
            console.Output.Trim().Should().Be(expected);
        }
    }
}
