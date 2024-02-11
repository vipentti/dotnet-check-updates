// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;

namespace DotnetCheckUpdates.Tests.Core;

public class FilterTests
{
    [Theory]
    [InlineData("bar", "some.bar.baz", true)]
    [InlineData("bar*", "some.bar.baz", false)]
    [InlineData("some.*", "some.bar.baz", true)]
    [InlineData("*baz", "some.bar.baz", true)]
    [InlineData("*baz*", "some.bar.baz", true)]
    [InlineData("*bar*", "some.bar.baz", true)]
    public void IsMatchReturnsExpected(string filter, string input, bool expected)
    {
        var sut = new Filter(filter);
        var result = sut.IsMatch(input);
        result.Should().Be(expected);
    }
}
