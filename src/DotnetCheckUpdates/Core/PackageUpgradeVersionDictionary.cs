// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using NuGet.Versioning;

namespace DotnetCheckUpdates.Core;

internal class PackageUpgradeVersionDictionary : Dictionary<string, VersionRange>
{
    public PackageUpgradeVersionDictionary()
        : base(StringComparer.OrdinalIgnoreCase) { }

    public PackageUpgradeVersionDictionary(IDictionary<string, VersionRange> dictionary)
        : base(dictionary, StringComparer.OrdinalIgnoreCase) { }

    public PackageUpgradeVersionDictionary(
        IEnumerable<KeyValuePair<string, VersionRange>> collection
    )
        : base(collection, StringComparer.OrdinalIgnoreCase) { }

    public PackageUpgradeVersionDictionary(int capacity)
        : base(capacity, StringComparer.OrdinalIgnoreCase) { }
}
