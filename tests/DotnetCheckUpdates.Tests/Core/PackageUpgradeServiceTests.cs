// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Frameworks;
using static DotnetCheckUpdates.Tests.TestUtils;

namespace DotnetCheckUpdates.Tests.Core;

public static class PackageUpgradeServiceTests
{
    public class GetPackageUpgrade
    {
        private static readonly NuGetFramework[] s_frameworks =
        [
            FrameworkConstants.CommonFrameworks.Net50,
            FrameworkConstants.CommonFrameworks.Net60,
            FrameworkConstants.CommonFrameworks.Net70,
        ];

        [Fact]
        public async Task ReturnsNullWhenNoPackagesFound()
        {
            // Arrange
            var service = new PackageUpgradeService(NullLogger, SetupMockNuGetService());

            // Act

            var upgrade = await service.GetPackageUpgrade(
                s_frameworks,
                PackageReference.From("NoSuchPackage", "1.0.0"),
                UpgradeTarget.Latest
            );

            // Assert

            upgrade.Should().BeNull();
        }

        [Theory]
        [InlineData(UpgradeTarget.Latest, "2.0.1")]
        [InlineData(UpgradeTarget.Greatest, "3.0.1-rc")]
        [InlineData(UpgradeTarget.Major, "2.0.1")]
        [InlineData(UpgradeTarget.Minor, "1.1.0")]
        [InlineData(UpgradeTarget.Patch, "1.0.1")]
        [InlineData(UpgradeTarget.PrereleaseMajor, "3.0.1-rc")]
        [InlineData(UpgradeTarget.PrereleaseMinor, "1.2.0-minor")]
        [InlineData(UpgradeTarget.PrereleasePatch, "1.0.2-patch")]
        public async Task ReturnsCorrectPackageForUpgradeTarget(
            UpgradeTarget target,
            string expected
        )
        {
            // Arrange
            var service = new PackageUpgradeService(
                NullLogger,
                SetupSimpleNuGetService(
                    new PackageDictionary()
                    {
                        ["SomePackage"] = new[]
                        {
                            "0.1".ToNuGetVersion(),
                            "1.0.0".ToNuGetVersion(),
                            "1.0.1".ToNuGetVersion(),
                            "1.0.1-patch".ToNuGetVersion(),
                            "1.0.2-patch".ToNuGetVersion(),
                            "1.1.0".ToNuGetVersion(),
                            "1.1.0-minor".ToNuGetVersion(),
                            "1.2.0-minor".ToNuGetVersion(),
                            "2.0.1".ToNuGetVersion(),
                            "2.0.1-alpha".ToNuGetVersion(),
                            "2.0.1-beta".ToNuGetVersion(),
                            "2.0.1-rc".ToNuGetVersion(),
                            "3.0.1-alpha".ToNuGetVersion(),
                            "3.0.1-beta".ToNuGetVersion(),
                            "3.0.1-rc".ToNuGetVersion(),
                        },
                    }
                )
            );

            // Act
            var upgrade = await service.GetPackageUpgrade(
                s_frameworks,
                PackageReference.From("SomePackage", "1.0.0"),
                target
            );

            // Assert
            upgrade!.Version.VersionString().Should().Be(expected);
        }
    }

    private static readonly ILogger<PackageUpgradeService> NullLogger =
        NullLogger<PackageUpgradeService>.Instance;
}
