// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using NuGet.Versioning;

namespace DotnetCheckUpdates.Core;

internal class PackageDictionary : Dictionary<string, NuGetVersion[]>
{
    public PackageDictionary()
        : base(StringComparer.OrdinalIgnoreCase) { }

    public PackageDictionary(IDictionary<string, NuGetVersion[]> dictionary)
        : base(dictionary, StringComparer.OrdinalIgnoreCase) { }

    public PackageDictionary(IEnumerable<KeyValuePair<string, NuGetVersion[]>> collection)
        : base(collection, StringComparer.OrdinalIgnoreCase) { }

    public PackageDictionary(int capacity)
        : base(capacity, StringComparer.OrdinalIgnoreCase) { }
}
