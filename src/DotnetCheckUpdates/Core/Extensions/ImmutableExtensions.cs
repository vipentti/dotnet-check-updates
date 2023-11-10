// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.Extensions;

internal static class ImmutableExtensions
{
    public static ImmutableArray<U> ConvertAll<T, U>(this ImmutableArray<T> arr, Func<T, U> conv)
    {
        var len = arr.Length;
        var result = ImmutableArray.CreateBuilder<U>(len);

        for (var i = 0; i < len; ++i)
        {
            result.Add(conv(arr[i]));
        }

        return result.MoveToImmutable();
    }

    public static T? MaybeAt<T>(this ImmutableArray<T> arr, int index) =>
        index >= 0 && index < arr.Length ? arr[index] : default;

    public static T? Find<T>(this ImmutableArray<T> array, Predicate<T> predicate) =>
        array.FindIndex(predicate) is int i && i > -1 ? array[i] : default;

    public static int FindIndex<T>(this ImmutableArray<T> array, Predicate<T> predicate)
    {
        var len = array.Length;
        for (var i = 0; i < len; ++i)
        {
            if (predicate(array[i]))
            {
                return i;
            }
        }
        return -1;
    }
}
