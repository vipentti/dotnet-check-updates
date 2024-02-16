// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.Extensions;

internal static class ArrayExtensions
{
    public static U[] ConvertAll<T, U>(this T[] arr, Func<T, U> conv)
    {
        var len = arr.Length;
        var result = new U[len];

        for (var i = 0; i < len; ++i)
        {
            result[i] = conv(arr[i]);
        }

        return result;
    }

    public static T? MaybeAt<T>(this T[] arr, int index) =>
        index >= 0 && index < arr.Length ? arr[index] : default;

    public static T? Find<T>(this T[] arr, Predicate<T> predicate) =>
        Array.Find(arr, predicate);
}
