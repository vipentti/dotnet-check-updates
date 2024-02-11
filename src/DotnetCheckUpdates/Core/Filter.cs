// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Text.RegularExpressions;

namespace DotnetCheckUpdates.Core;

public readonly record struct Filter
{
    private readonly string _originalPattern;

    private readonly Regex? _regex;

    public Filter(string originalPattern)
    {
        _originalPattern = originalPattern;

        if (_originalPattern.Contains('*'))
        {
            _regex = new Regex(
                "^" + Regex.Escape(originalPattern).Replace(@"\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
            );
        }
    }

    public bool IsMatch(string input)
    {
        if (_regex is not null)
        {
            return _regex.IsMatch(input);
        }

        return input.Contains(_originalPattern, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() =>
        $"Filter({_originalPattern}, {(_regex is null ? "(null)" : _regex)})";
}
