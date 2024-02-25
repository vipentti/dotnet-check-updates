// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;

namespace DotnetCheckUpdates.Tests;

internal sealed class MockPackageUpgradeFactory(PackageUpgradeService service)
    : IPackageUpgradeServiceFactory
{
    public PackageUpgradeService GetPackageUpgradeService() => service;
}
