// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Tests;

internal class MockFiles : Dictionary<string, string>
{
    public MockFiles() { }

    public MockFiles(IDictionary<string, string> dictionary)
        : base(dictionary) { }

    public MockFiles(IEnumerable<KeyValuePair<string, string>> collection)
        : base(collection) { }

    public MockFiles(IEqualityComparer<string>? comparer)
        : base(comparer) { }

    public MockFiles(int capacity)
        : base(capacity) { }

    public MockFiles(IDictionary<string, string> dictionary, IEqualityComparer<string>? comparer)
        : base(dictionary, comparer) { }

    public MockFiles(
        IEnumerable<KeyValuePair<string, string>> collection,
        IEqualityComparer<string>? comparer
    )
        : base(collection, comparer) { }

    public MockFiles(int capacity, IEqualityComparer<string>? comparer)
        : base(capacity, comparer) { }
}
