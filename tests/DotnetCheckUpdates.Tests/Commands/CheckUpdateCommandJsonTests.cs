// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Spectre.Console.Testing;
using static DotnetCheckUpdates.Tests.CheckUpdateCommandUtils;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandJsonTests
{
    [Fact]
    public void JsonModeClassifier_RejectsExplicitValueForms()
    {
        JsonModeClassifier.Classify(["--json=true"]).Mode.Should().Be(JsonMode.Reject);
        JsonModeClassifier.Classify(["--json:true"]).Mode.Should().Be(JsonMode.Reject);
        JsonModeClassifier.Classify(["--json", "true"]).Mode.Should().Be(JsonMode.Reject);
        JsonModeClassifier.Classify(["--json", "false"]).Mode.Should().Be(JsonMode.Reject);
        JsonModeClassifier.Classify(["--json", "TRUE"]).Mode.Should().Be(JsonMode.Reject);
        JsonModeClassifier.Classify(["--json", "False"]).Mode.Should().Be(JsonMode.Reject);
    }

    [Fact]
    public void JsonModeClassifier_SelectsBareJsonBeforeBoundary()
    {
        JsonModeClassifier.Classify(["--json"]).Mode.Should().Be(JsonMode.JsonIntent);
        JsonModeClassifier.Classify(["--json", "--list"]).Mode.Should().Be(JsonMode.JsonIntent);
        JsonModeClassifier
            .Classify(["--cwd", "foo", "--json"])
            .Mode.Should()
            .Be(JsonMode.JsonIntent);
    }

    [Fact]
    public void JsonModeClassifier_IgnoresTokensAfterBoundary()
    {
        JsonModeClassifier.Classify(["--", "--json"]).Mode.Should().Be(JsonMode.Default);
        JsonModeClassifier.Classify(["--list", "--", "--json"]).Mode.Should().Be(JsonMode.Default);
        JsonModeClassifier
            .Classify(["--json=true", "--", "--json"])
            .Mode.Should()
            .Be(JsonMode.Reject);
        JsonModeClassifier.Classify(["--", "--json=true"]).Mode.Should().Be(JsonMode.Default);
    }

    [Fact]
    public void JsonModeClassifier_SelectsOpenCliCaseInsensitive()
    {
        JsonModeClassifier
            .Classify(["--help-dump-opencli", "--json"])
            .Mode.Should()
            .Be(JsonMode.OpenCli);
        JsonModeClassifier
            .Classify(["--HELP-DUMP-OPENCLI", "--json"])
            .Mode.Should()
            .Be(JsonMode.OpenCli);
        JsonModeClassifier
            .Classify(["--Help-Dump-OpenCli", "--json"])
            .Mode.Should()
            .Be(JsonMode.OpenCli);
    }

    [Fact]
    public void JsonPathHelper_GetKind()
    {
        JsonPathHelper
            .GetKind("/some/Directory.Build.props")
            .Should()
            .Be(JsonOutputKind.DirectoryBuildProps);
        JsonPathHelper
            .GetKind("/some/Directory.Packages.props")
            .Should()
            .Be(JsonOutputKind.DirectoryPackagesProps);
        JsonPathHelper.GetKind("/some/app.csproj").Should().Be(JsonOutputKind.Project);
        JsonPathHelper.GetKind("/some/app.fsproj").Should().Be(JsonOutputKind.Project);
    }

    [Fact]
    public void JsonPathHelper_ToJsonDisplayPath()
    {
        var cwd = RootedTestPath("cwd").ToString();
        var file = RootedTestPath("cwd/src/App/App.csproj").ToString();
        JsonPathHelper
            .ToJsonDisplayPath(file, cwd, showAbsolute: false)
            .Should()
            .Be(Path.Combine("src", "App", "App.csproj"));
        JsonPathHelper.ToJsonDisplayPath(file, cwd, showAbsolute: true).Should().Be(file);
    }

    [Fact]
    public void JsonUpgradeTypeTokens_Maps()
    {
        JsonUpgradeTypeTokens.FromUpgradeType(UpgradeType.None).Should().Be("none");
        JsonUpgradeTypeTokens.FromUpgradeType(UpgradeType.Major).Should().Be("major");
        JsonUpgradeTypeTokens.FromUpgradeType(UpgradeType.Minor).Should().Be("minor");
        JsonUpgradeTypeTokens.FromUpgradeType(UpgradeType.Patch).Should().Be("patch");
        JsonUpgradeTypeTokens.FromUpgradeType(UpgradeType.Release).Should().Be("release");
    }

    [Fact]
    public async Task Json_NonList_OmitsUnchangedPackages()
    {
        var rawJson = await CaptureJsonAsync(
            [("Flurl", "3.0.0"), ("Other", "1.0.0")],
            [
                new MockUpgrade("Flurl")
                {
                    Versions = { "4.0.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            ],
            new CheckUpdateCommand.Settings { Cwd = RootedTestPath("cap").ToString(), Json = true }
        );
        var doc = JsonDocument.Parse(rawJson);
        var checkedFiles = doc.RootElement.GetProperty("checkedFiles");
        checkedFiles[0].GetProperty("packageCount").GetInt32().Should().Be(2);
        var packages = checkedFiles[0].GetProperty("packages");
        packages.GetArrayLength().Should().Be(1);
        packages[0].GetProperty("name").GetString().Should().Be("Flurl");
    }

    [Fact]
    public async Task Json_List_IncludesUnchangedAndVersionless()
    {
        var rawJson = await CaptureJsonAsync(
            [("Flurl", "3.0.0"), ("Other", "1.0.0"), ("Versionless", "")],
            [
                new MockUpgrade("Flurl")
                {
                    Versions = { "4.0.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            ],
            new CheckUpdateCommand.Settings
            {
                Cwd = RootedTestPath("cap").ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(rawJson);
        var pkgs = doc.RootElement.GetProperty("checkedFiles")[0].GetProperty("packages");
        pkgs.GetArrayLength().Should().Be(3);
        var versionless = pkgs.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "Versionless");
        versionless.GetProperty("currentVersion").ValueKind.Should().Be(JsonValueKind.Null);
        versionless.GetProperty("targetVersion").ValueKind.Should().Be(JsonValueKind.Null);
        versionless.GetProperty("upgradeType").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Json_ReportsFilteredPackageCount()
    {
        var rawJson = await CaptureJsonAsync(
            [("Flurl", "3.0.0"), ("Other", "1.0.0")],
            [
                new MockUpgrade("Flurl")
                {
                    Versions = { "4.0.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            ],
            new CheckUpdateCommand.Settings
            {
                Cwd = RootedTestPath("cap").ToString(),
                Json = true,
                Include = ["Flurl*"],
            }
        );
        var doc = JsonDocument.Parse(rawJson);
        doc.RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("packageCount")
            .GetInt32()
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Json_ShowsAbsolutePaths()
    {
        var cwdAndFs = SetupSimple();
        var cwd = cwdAndFs.Cwd;
        var rawJson = await CaptureProjectJsonAsync(
            cwd,
            showAbsolute: true,
            upgrades:
            [
                new MockUpgrade("Flurl")
                {
                    Versions = { "4.0.0" },
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                },
            ]
        );
        var doc = JsonDocument.Parse(rawJson);
        var path = doc.RootElement.GetProperty("checkedFiles")[0].GetProperty("path").GetString()!;
        path.Should().Contain(cwd.ToString());
    }

    [Fact]
    public async Task Json_UpgradeRequestedAndApplied()
    {
        var cwd = RootedTestPath("p");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [cwd.PathCombine("a.csproj")] = ProjectFileUtils.ProjectFileXml([
                    ("Flurl", "3.0.0"),
                ]),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json1 = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                Upgrade = false,
            }
        );
        JsonDocument
            .Parse(json1)
            .RootElement.GetProperty("upgradeRequested")
            .GetBoolean()
            .Should()
            .BeFalse();
        JsonDocument
            .Parse(json1)
            .RootElement.GetProperty("upgradesApplied")
            .GetBoolean()
            .Should()
            .BeFalse();
        var json2 = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                Upgrade = true,
            }
        );
        JsonDocument
            .Parse(json2)
            .RootElement.GetProperty("upgradeRequested")
            .GetBoolean()
            .Should()
            .BeTrue();
        JsonDocument
            .Parse(json2)
            .RootElement.GetProperty("upgradesApplied")
            .GetBoolean()
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task Json_UpgradeWithNoApplicableUpgrades()
    {
        var cwd = RootedTestPath("p");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [cwd.PathCombine("a.csproj")] = ProjectFileUtils.ProjectFileXml([
                    ("Flurl", "3.0.0"),
                ]),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "3.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                Upgrade = true,
            }
        );
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("upgradeRequested").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("upgradesApplied").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Json_OverlappingSolutionsDeduplicatesCheckedFiles()
    {
        var cwd = RootedTestPath("overlap");
        var slnA = cwd.PathCombine("A.sln");
        var slnB = cwd.PathCombine("B.sln");
        var proj = cwd.PathCombine("src/App/App.csproj");
        var slnContent = ProjectFileUtils.SolutionFile([("App", "src/App/App.csproj")]);
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [slnA] = slnContent,
                [slnB] = slnContent,
                [proj] = ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")]),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings { Cwd = cwd.ToString(), Json = true }
        );
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("checkedFiles").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("solutions").GetArrayLength().Should().Be(2);
        var projPath = doc
            .RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("path")
            .GetString()!;
        foreach (var sol in doc.RootElement.GetProperty("solutions").EnumerateArray())
        {
            sol.GetProperty("projects").EnumerateArray().First().GetString().Should().Be(projPath);
        }
    }

    [Fact]
    public async Task Json_ReportsKindsAndTargetFrameworks()
    {
        var cwd = RootedTestPath("kinds");
        var proj = cwd.PathCombine("app.csproj");
        var props = cwd.PathCombine("Directory.Packages.props");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [proj] = ProjectFileUtils.ProjectFileXml(
                    [("Flurl", "3.0.0")],
                    framework: Frameworks.Net8_0
                ),
                [props] = ProjectFileUtils.ProjectFileXml(
                    [("Central", "1.0.0")],
                    framework: Frameworks.Net8_0,
                    referenceType: ReferenceType.PackageVersion
                ),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
            new MockUpgrade("Central")
            {
                Versions = { "2.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(json);
        var byPath = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .ToDictionary(e => e.GetProperty("path").GetString()!, e => e);
        byPath.Should().ContainKey("Directory.Packages.props");
        byPath["Directory.Packages.props"]
            .GetProperty("kind")
            .GetString()
            .Should()
            .Be("directoryPackagesProps");
        byPath["app.csproj"].GetProperty("kind").GetString().Should().Be("project");
        byPath["app.csproj"]
            .GetProperty("targetFrameworks")
            .EnumerateArray()
            .First()
            .GetString()
            .Should()
            .Be("net8.0");
    }

    [Fact]
    public void Settings_Validate_RejectsJsonWithInteractiveAndVersion()
    {
        var s1 = new CheckUpdateCommand.Settings { Json = true, Interactive = true };
        s1.Validate().Successful.Should().BeFalse();
        s1.Validate().Message.Should().Contain("--json cannot be used with --interactive");
        var s2 = new CheckUpdateCommand.Settings { Json = true, ShowVersion = true };
        s2.Validate().Successful.Should().BeFalse();
        s2.Validate().Message.Should().Contain("--json cannot be used with --version");
    }

    [Fact]
    public async Task Json_DeterministicOrdering()
    {
        var cwd = RootedTestPath("order");
        var a = cwd.PathCombine("a.csproj");
        var b = cwd.PathCombine("b.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [a] = ProjectFileUtils.ProjectFileXml([("ZPkg", "1.0.0"), ("APkg", "1.0.0")]),
                [b] = ProjectFileUtils.ProjectFileXml([("MPkg", "1.0.0")]),
            }
        );
        var service = SetupMockPackages([]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(json);
        var files = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .Select(e => e.GetProperty("path").GetString())
            .ToArray();
        files.Should().BeInAscendingOrder(StringComparer.Ordinal);
        var pkgs = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .First(e => e.GetProperty("path").GetString() == "a.csproj")
            .GetProperty("packages")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToArray();
        pkgs[0].Should().Be("APkg");
        pkgs[1].Should().Be("ZPkg");
    }

    [Fact]
    public async Task Json_EmptyArraysAndNulls()
    {
        var cwd = RootedTestPath("empty");
        var fs = SetupFileSystem(cwd.ToString(), new Dictionary<string, string>());
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            SetupMockPackages([]),
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings { Cwd = cwd.ToString(), Json = true }
        );
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("solutions").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("checkedFiles").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Json_VersionRangesAndInferredFrameworks()
    {
        var cwd = RootedTestPath("ranges");
        var a = cwd.PathCombine("a.csproj");
        var b = cwd.PathCombine("b.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [a] = ProjectFileUtils.ProjectFileXml(
                    [("RangePkg", "[1.0.0,)")],
                    framework: Frameworks.Net8_0
                ),
                [b] = ProjectFileUtils.ProjectFileXml(
                    [("NoFrameworkPkg", "1.0.0")],
                    framework: Frameworks.Unspecified
                ),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("RangePkg")
            {
                Versions = { "2.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(json);
        var byPath = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .ToDictionary(e => e.GetProperty("path").GetString()!, e => e);
        byPath["a.csproj"]
            .GetProperty("packages")
            .EnumerateArray()
            .First()
            .GetProperty("currentVersion")
            .GetString()
            .Should()
            .Be("[1.0.0,)");
        var targetVer = byPath["a.csproj"]
            .GetProperty("packages")
            .EnumerateArray()
            .First()
            .GetProperty("targetVersion")
            .GetString()!;
        targetVer.Should().Be("[2.0.0,)");
        byPath["b.csproj"]
            .GetProperty("targetFrameworks")
            .EnumerateArray()
            .First()
            .GetString()
            .Should()
            .Be("net8.0");
    }

    [Fact]
    public async Task Json_ConditionedDuplicatesRemainSeparate()
    {
        var cwd = RootedTestPath("cond");
        var props = cwd.PathCombine("Directory.Packages.props");
        var proj = cwd.PathCombine("proj.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [proj] = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup>
                  <ItemGroup><PackageReference Include="Example" /></ItemGroup>
                </Project>
                """,
                [props] = """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Example" Version="1.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
                    <PackageVersion Include="Example" Version="1.5.0" Condition="'$(TargetFramework)' == 'net9.0'" />
                  </ItemGroup>
                </Project>
                """,
            }
        );
        var service = Substitute.For<DotnetCheckUpdates.Core.NuGetUtils.INuGetService>();
        service
            .GetPackageVersionsAsync("Example", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new[] { "2.0.0".ToNuGetVersion(), "3.0.0".ToNuGetVersion() }.AsEnumerable()
                )
            );
        service
            .GetSupportedFrameworksAsync("Example", "2.0.0", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    ImmutableHashSet.Create(
                        NuGet.Frameworks.FrameworkConstants.CommonFrameworks.Net80
                    )
                )
            );
        service
            .GetSupportedFrameworksAsync("Example", "3.0.0", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    ImmutableHashSet.Create(
                        NuGet.Frameworks.FrameworkConstants.CommonFrameworks.Net90
                    )
                )
            );
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(json);
        var pkgs = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .First(e => e.GetProperty("path").GetString() == "Directory.Packages.props")
            .GetProperty("packages");
        pkgs.GetArrayLength().Should().Be(2);
        pkgs.EnumerateArray()
            .Select(p => p.GetProperty("currentVersion").GetString())
            .Should()
            .Contain("1.0.0");
        pkgs.EnumerateArray()
            .Select(p => p.GetProperty("currentVersion").GetString())
            .Should()
            .Contain("1.5.0");
    }

    [Fact]
    public async Task Json_ShowPackageCountInert()
    {
        var cwd = RootedTestPath("count");
        var a = cwd.PathCombine("a.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [a] = ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0"), ("Other", "1.0.0")]),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var json = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                ShowPackageCount = true,
            }
        );
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("packageCount")
            .GetInt32()
            .Should()
            .Be(2);
    }

    [Fact]
    public async Task Json_FiltersPreserveSemantics()
    {
        var cwd = RootedTestPath("filter");
        var a = cwd.PathCombine("a.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [a] = ProjectFileUtils.ProjectFileXml([
                    ("System.Text.Json", "1.0.0"),
                    ("Flurl", "3.0.0"),
                ]),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var jsonInclude = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                Include = ["Flurl*"],
                List = true,
            }
        );
        JsonDocument
            .Parse(jsonInclude)
            .RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("packages")
            .EnumerateArray()
            .All(p =>
                p.GetProperty("name")
                    .GetString()!
                    .StartsWith("Flurl", StringComparison.OrdinalIgnoreCase)
            )
            .Should()
            .BeTrue();
        var jsonExclude = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                Exclude = ["Flurl*"],
                List = true,
            }
        );
        JsonDocument
            .Parse(jsonExclude)
            .RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("packages")
            .EnumerateArray()
            .All(p =>
                !p.GetProperty("name")
                    .GetString()!
                    .StartsWith("Flurl", StringComparison.OrdinalIgnoreCase)
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task Json_RelativeAndAbsolutePaths()
    {
        var cwd = RootedTestPath("rel");
        var a = cwd.PathCombine("a.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new() { [a] = ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")]) }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var rel = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings { Cwd = cwd.ToString(), Json = true }
        );
        JsonDocument
            .Parse(rel)
            .RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("path")
            .GetString()
            .Should()
            .Be("a.csproj");
        var abs = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                ShowAbsolute = true,
            }
        );
        JsonDocument
            .Parse(abs)
            .RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("path")
            .GetString()
            .Should()
            .Be(a);
    }

    [Fact]
    public async Task Json_TextModeUnchanged()
    {
        var cwd = RootedTestPath("text");
        var a = cwd.PathCombine("a.csproj");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new() { [a] = ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")]) }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var console = new TestConsole();
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            console,
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        await cmd.AsICommand()
            .ExecuteAsync(
                new Spectre.Console.Cli.CommandContext(
                    [],
                    Substitute.For<Spectre.Console.Cli.IRemainingArguments>(),
                    "test",
                    null
                ),
                new CheckUpdateCommand.Settings
                {
                    Cwd = cwd.ToString(),
                    ShowAbsolute = true,
                    AsciiTree = true,
                },
                CancellationToken.None
            );
        console.Output.Should().Contain("a.csproj");
        console.Output.Should().Contain("Flurl");
    }

    private static (FullPath Cwd, MockFileSystem Fs) SetupSimple()
    {
        var cwd = RootedTestPath("simple");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [cwd.PathCombine("a.csproj")] = ProjectFileUtils.ProjectFileXml([
                    ("Flurl", "3.0.0"),
                ]),
            }
        );
        return (cwd, fs);
    }

    private static async Task<string> CaptureProjectJsonAsync(
        FullPath cwd,
        bool showAbsolute,
        IEnumerable<MockUpgrade> upgrades
    )
    {
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [cwd.PathCombine("a.csproj")] = ProjectFileUtils.ProjectFileXml([
                    ("Flurl", "3.0.0"),
                ]),
            }
        );
        var service = SetupMockPackages(upgrades);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        return await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                ShowAbsolute = showAbsolute,
            }
        );
    }

    private static async Task<string> CaptureJsonAsync(
        IEnumerable<(string id, string version)> packages,
        IEnumerable<MockUpgrade> upgrades,
        CheckUpdateCommand.Settings settings
    )
    {
        var cwd = RootedTestPath("cap");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new() { [cwd.PathCombine("a.csproj")] = ProjectFileUtils.ProjectFileXml(packages) }
        );
        var service = SetupMockPackages(upgrades);
        var cmd = CheckUpdateCommandUtils.CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        return await RunJsonAsync(cmd, cwd.ToString(), settings);
    }

    [Fact]
    public async Task Json_SolutionExcludesPropsFromProjects()
    {
        var cwd = RootedTestPath("withprops");
        var sln = cwd.PathCombine("a.sln");
        var proj = cwd.PathCombine("src/app/app.csproj");
        var propsBuild = cwd.PathCombine("Directory.Build.props");
        var propsPackages = cwd.PathCombine("Directory.Packages.props");
        var slnContent = ProjectFileUtils.SolutionFile([("app", "src/app/app.csproj")]);
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [sln] = slnContent,
                [proj] = ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")]),
                [propsBuild] = ProjectFileUtils.ProjectFileXml([("BuildPkg", "1.0.0")]),
                [propsPackages] = ProjectFileUtils.ProjectFileXml(
                    [("Central", "1.0.0")],
                    referenceType: ReferenceType.PackageVersion
                ),
            }
        );
        var service = SetupMockPackages([
            new MockUpgrade("Flurl")
            {
                Versions = { "4.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
            new MockUpgrade("BuildPkg")
            {
                Versions = { "2.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
            new MockUpgrade("Central")
            {
                Versions = { "2.0.0" },
                SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
            },
        ]);
        var cmd = CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var rawJson = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(rawJson);
        var solProjects = doc.RootElement.GetProperty("solutions")[0].GetProperty("projects");
        solProjects
            .EnumerateArray()
            .Any(p => p.GetString()!.Contains("Directory."))
            .Should()
            .BeFalse();
        var byPath = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .ToDictionary(
                e => e.GetProperty("path").GetString()!,
                e => e.GetProperty("kind").GetString()
            );
        byPath["Directory.Build.props"].Should().Be("directoryBuildProps");
        byPath["Directory.Packages.props"].Should().Be("directoryPackagesProps");
    }

    [Fact]
    public void Json_OutsideCwdOrderingByEmittedPath()
    {
        var cwd = RootedTestPath("cwd").ToString();
        // Canonical absolute paths where lexical absolute order differs from relative emitted order
        // /outer/a/a.csproj (/outer/a...) vs /outer/b/b.csproj vs /cwd/inner/inner.csproj
        // Absolute sorted: /cwd/inner..., /outer/a..., /outer/b...
        // But emitted relative from /cwd: inner/inner.csproj, ../outer/a/outerA.csproj, ../outer/b/outerB.csproj
        // Relative sorted ordinal: ../outer/a..., ../outer/b..., inner/...
        var a = RootedTestPath("outer/a/outerA.csproj").ToString();
        var b = RootedTestPath("outer/b/outerB.csproj").ToString();
        var cwdPath = RootedTestPath("cwd").ToString();
        var inner = Path.Combine(cwdPath, "inner", "inner.csproj");
        // Build results with canonical paths in absolute-sorted order to prove builder reorders by emitted path
        var resInner = MakeResult(inner, "inner/inner.csproj");
        var resA = MakeResult(a, "../outer/a/outerA.csproj");
        var resB = MakeResult(b, "../outer/b/outerB.csproj");
        var results = new[] { resInner, resA, resB }.ToImmutableArray();
        var doc = JsonResultBuilder.Build(
            results,
            ImmutableDictionary<string, string[]>.Empty,
            cwdPath,
            showAbsolute: false,
            upgradeRequested: false,
            upgradesApplied: false
        );
        var paths = doc.CheckedFiles.Select(c => c.Path).ToArray();
        // Should be sorted by emitted path ordinal, not canonical
        paths.Should().BeInAscendingOrder(StringComparer.Ordinal);
        var expectedA = Path.GetRelativePath(cwdPath, a);
        var expectedB = Path.GetRelativePath(cwdPath, b);
        var expectedInner = Path.GetRelativePath(cwdPath, inner);
        var expectedSorted = new[] { expectedA, expectedB, expectedInner }
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        paths.Should().Equal(expectedSorted);
    }

    private static JsonProjectCheckResult MakeResult(string canonical, string expectedRelative)
    {
        var dummy = new PackageReference("Dummy", NuGet.Versioning.VersionRange.Parse("1.0.0"));
        return new JsonProjectCheckResult(
            canonical,
            JsonPathHelper.GetKind(canonical),
            1,
            ImmutableArray<NuGet.Frameworks.NuGetFramework>.Empty,
            ImmutableArray.Create<(
                PackageReference,
                NuGet.Versioning.VersionRange?,
                DotnetCheckUpdates.Core.UpgradeType,
                ImmutableArray<NuGet.Frameworks.NuGetFramework>
            )>(
                (
                    dummy,
                    null,
                    DotnetCheckUpdates.Core.UpgradeType.None,
                    ImmutableArray<NuGet.Frameworks.NuGetFramework>.Empty
                )
            )
        );
    }

    [Fact]
    public async Task Json_CaseVariantPropsKindIsCaseInsensitive()
    {
        var cwd = RootedTestPath("caseprops");
        var lower = cwd.PathCombine("directory.build.props");
        var upper = cwd.PathCombine("DIRECTORY.PACKAGES.PROPS");
        var fs = SetupFileSystem(
            cwd.ToString(),
            new()
            {
                [cwd.PathCombine("a.csproj")] = ProjectFileUtils.ProjectFileXml([
                    ("Flurl", "3.0.0"),
                ]),
                [lower] = ProjectFileUtils.ProjectFileXml([("BuildPkg", "1.0.0")]),
                [upper] = ProjectFileUtils.ProjectFileXml(
                    [("Central", "1.0.0")],
                    referenceType: ReferenceType.PackageVersion
                ),
            }
        );
        var service = SetupMockPackages([]);
        var cmd = CreateCommand(
            new TestConsole(),
            fs,
            service,
            finder: null,
            SolutionFileFormat.Sln
        );
        var rawJson = await RunJsonAsync(
            cmd,
            cwd.ToString(),
            new CheckUpdateCommand.Settings
            {
                Cwd = cwd.ToString(),
                Json = true,
                List = true,
            }
        );
        var doc = JsonDocument.Parse(rawJson);
        var paths = doc
            .RootElement.GetProperty("checkedFiles")
            .EnumerateArray()
            .Select(e => e.GetProperty("path").GetString()!)
            .ToArray();
        // GetKind uses OS-dependent comparison: exact on Linux (case-sensitive FS), case-insensitive on Windows
        JsonPathHelper.GetKind("Directory.Build.props").Should().Be("directoryBuildProps");
        JsonPathHelper.GetKind("Directory.Packages.props").Should().Be("directoryPackagesProps");
        if (OperatingSystem.IsWindows())
        {
            JsonPathHelper.GetKind("directory.build.props").Should().Be("directoryBuildProps");
            JsonPathHelper
                .GetKind("DIRECTORY.PACKAGES.PROPS")
                .Should()
                .Be("directoryPackagesProps");
        }
        else
        {
            JsonPathHelper.GetKind("directory.build.props").Should().Be("project");
            JsonPathHelper.GetKind("DIRECTORY.PACKAGES.PROPS").Should().Be("project");
            JsonPathHelper.GetKind("DIRECTORY.BUILD.PROPS").Should().Be("project");
            JsonPathHelper.GetKind("directory.packages.props").Should().Be("project");
        }
    }

    private static async Task<string> RunJsonAsync(
        CheckUpdateCommand cmd,
        string cwd,
        CheckUpdateCommand.Settings settings
    )
    {
        var sw = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(sw);
        try
        {
            await cmd.AsICommand()
                .ExecuteAsync(
                    new Spectre.Console.Cli.CommandContext(
                        [],
                        Substitute.For<Spectre.Console.Cli.IRemainingArguments>(),
                        "test",
                        null
                    ),
                    settings,
                    CancellationToken.None
                );
        }
        finally
        {
            Console.SetOut(prev);
        }
        return sw.ToString().Trim();
    }
}
