// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Spectre.Console;

namespace DotnetCheckUpdates.Core.Extensions;

internal static class EnumerableExtensions
{
    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable)
        where T : class
    {
        return enumerable.Where(e => e is not null).Select(e => e!);
    }
}
