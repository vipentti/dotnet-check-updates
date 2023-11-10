// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Tests.Core;

public class UpgradeTypeTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", UpgradeType.None)]
    [InlineData("1.0.0", "1.0.1", UpgradeType.Patch)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.2", UpgradeType.Release)]
    [InlineData("1.0.0-alpha.1", "1.0.0-beta.1", UpgradeType.Release)]
    [InlineData("1.0.0-alpha.1", "1.0.0-rc.1", UpgradeType.Release)]
    [InlineData("1.0.0-beta", "1.0.0-open", UpgradeType.Release)]
    [InlineData("1.0.0-beta", "1.0.0-rc", UpgradeType.Release)]
    [InlineData("1.0.0-rc.123", "1.0.0-zzz", UpgradeType.Release)]
    [InlineData("1.0.0-rc", "1.0.0-zzz", UpgradeType.Release)]
    [InlineData("1.0.0-alpha.1", "1.0.1", UpgradeType.Patch)]
    [InlineData("1.0.0", "1.1.1", UpgradeType.Minor)]
    [InlineData("1.0.0", "2.1.1", UpgradeType.Major)]
    [InlineData("[1.0.0,)", "[1.0.1,)", UpgradeType.Patch)]
    [InlineData("[1.0.0,)", "[1.1.1,)", UpgradeType.Minor)]
    [InlineData("[1.0.0,)", "[2.1.1,)", UpgradeType.Major)]
    [InlineData("1.0.0-rc", "1.0.0-beta", UpgradeType.None)]
    [InlineData("1.0.0-open", "1.0.0-beta", UpgradeType.None)]
    [InlineData("1.0.0-rc.1", "1.0.0-rc.0", UpgradeType.None)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.1", UpgradeType.None)]
    [InlineData("1.0.0-alpha.2", "1.0.0-alpha.1", UpgradeType.None)]
    [InlineData("1.0.0-beta.1", "1.0.0-alpha.1", UpgradeType.None)]
    [InlineData("1.0.0-zzz", "1.0.0-rc", UpgradeType.None)]
    [InlineData("1.0.0-zzz", "1.0.0-rc.1", UpgradeType.None)]
    [InlineData("1.0.0-zzz", "1.0.0-rc999", UpgradeType.None)]
    [InlineData("[1.0.0,)", "[1.0.0,)", UpgradeType.None)]
    public void ReturnsExpectedUpgradeType(string lhs, string rhs, UpgradeType expected)
    {
        var lhsRange = VersionRange.Parse(lhs);
        var rhsRange = VersionRange.Parse(rhs);

        var type = lhsRange.GetUpgradeTypeTo(rhsRange);

        type.Should().Be(expected);
    }
}
