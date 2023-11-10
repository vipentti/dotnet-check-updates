// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Microsoft.Extensions.Logging.Abstractions;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Core;

public class UpgradeTargetTests
{
    [Theory]
    [MemberData(nameof(UpgradeTargetCases))]
    public async Task UpdateTargetAffectsUpgradedPackages(
        string description,
        UpgradeTarget target,
        string version,
        string[] versions,
        string expected
    )
    {
        _ = description;

        var nuget = SetupMockPackages(
            new[]
            {
                new MockUpgrade("UpgradeTest")
                {
                    Versions = versions.ToList(),
                    SupportedFrameworks = MockUpgrade.DefaultSupportedFrameworks,
                }
            }
        );

        var packageService = new PackageUpgradeService(
            NullLogger<PackageUpgradeService>.Instance,
            nuget
        );

        var current = PackageReference.From("UpgradeTest", version);

        var upgrade = await packageService.GetPackageUpgrade(
            new[] { "net6.0".ToNuGetFramework(), },
            current,
            target
        );

        // We are not expecting to upgrade when version and expected are equal
        if (string.Equals(version, expected))
        {
            upgrade.Should().BeNull();
        }
        else
        {
            upgrade.Should().NotBeNull();
            upgrade!.GetVersionString().Should().Be(expected);
        }
    }

    public static readonly TheoryData<
        string,
        UpgradeTarget,
        string,
        string[],
        string
    > UpgradeTargetCases =
        new()
        {
            {
                "prefers non-pre-release",
                UpgradeTarget.Latest,
                "1.0.0",
                new[] { "1.0.1-alpha", "1.0.1", },
                "1.0.1"
            },
            {
                "prefers greatest version including pre-releases",
                UpgradeTarget.Greatest,
                "1.0.0",
                new[]
                {
                    "1.0.1-alpha",
                    "1.0.1",
                    "1.0.2",
                    "1.0.3-beta",
                    "1.0.4-beta",
                    "1.0.4-release-candidate",
                },
                "1.0.4-release-candidate"
            },
            {
                "no matching major version",
                UpgradeTarget.Major,
                "1.0.0",
                new[] { "1.0.1-alpha", "1.0.1", "1.0.2", "1.0.3-beta", },
                "1.0.0"
            },
            {
                "greatest non-pre-release major version",
                UpgradeTarget.Major,
                "1.0.0",
                new[]
                {
                    "1.0.1-alpha",
                    "1.0.1",
                    "1.0.2",
                    "1.0.3-beta",
                    "3.0.0",
                    "2.0.0",
                    "4.0.0",
                    "5.0.0-rc",
                },
                "4.0.0"
            },
            {
                "greatest major version when pre-release",
                UpgradeTarget.Major,
                "1.0.0-beta",
                new[]
                {
                    "1.0.1-alpha",
                    "1.0.1",
                    "1.0.2",
                    "1.0.3-beta",
                    "3.0.0",
                    "2.0.0",
                    "4.0.0",
                    "5.0.0-rc",
                },
                "4.0.0"
            },
            {
                "greatest major version when pre-release",
                UpgradeTarget.PrereleaseMajor,
                "1.0.0-beta",
                new[]
                {
                    "1.0.1-alpha",
                    "1.0.1",
                    "1.0.2",
                    "1.0.3-beta",
                    "3.0.0",
                    "2.0.0",
                    "4.0.0",
                    "5.0.0-rc",
                },
                "5.0.0-rc"
            },
            {
                "greatest major version",
                UpgradeTarget.PrereleaseMajor,
                "1.0.0",
                new[]
                {
                    "1.0.1-alpha",
                    "1.0.1",
                    "1.0.2",
                    "1.0.3-beta",
                    "3.0.0",
                    "2.0.0",
                    "4.0.0",
                    "5.0.0-rc",
                },
                "5.0.0-rc"
            },
            {
                "no better minor version",
                UpgradeTarget.Minor,
                "1.0.0",
                new[] { "1.0.1-alpha", "1.0.1", "1.0.2", "1.0.3-beta", },
                "1.0.0"
            },
            {
                "no better minor version",
                UpgradeTarget.Minor,
                "1.0.0",
                new[] { "1.0.1-alpha", "2.1.1", "3.2.2", "4.0.3-beta", },
                "1.0.0"
            },
            {
                "no better minor version",
                UpgradeTarget.PrereleaseMinor,
                "1.0.0",
                new[] { "1.0.1-alpha", "2.1.1", "3.2.2", "4.0.3-beta", },
                "1.0.0"
            },
            {
                "greatest non-pre-release minor version",
                UpgradeTarget.Minor,
                "1.0.0-beta",
                new[] { "1.0.1-alpha", "1.1.1", "1.2.2", "1.3.3-beta", },
                "1.2.2"
            },
            {
                "greatest minor version",
                UpgradeTarget.PrereleaseMinor,
                "1.0.0-beta",
                new[] { "1.0.1-alpha", "1.1.1", "1.2.2", "1.3.3-beta", },
                "1.3.3-beta"
            },
            {
                "no better patch version",
                UpgradeTarget.Patch,
                "1.0.0",
                new[] { "1.0.1-alpha", "1.0.3-beta", },
                "1.0.0"
            },
            {
                "no better patch version",
                UpgradeTarget.Patch,
                "1.0.0",
                new[] { "1.0.1-alpha", "2.0.0", "2.0.1", },
                "1.0.0"
            },
            {
                "no better patch version",
                UpgradeTarget.PrereleasePatch,
                "1.0.0",
                new[] { "2.0.0", "2.0.1", },
                "1.0.0"
            },
            {
                "better patch version",
                UpgradeTarget.Patch,
                "1.0.0",
                new[] { "1.0.1-alpha", "1.0.1", "1.0.3-beta", },
                "1.0.1"
            },
            {
                "better patch version",
                UpgradeTarget.PrereleasePatch,
                "1.0.0",
                new[] { "1.0.1-alpha", "1.0.1", "1.0.3-beta", },
                "1.0.3-beta"
            },
        };
}
