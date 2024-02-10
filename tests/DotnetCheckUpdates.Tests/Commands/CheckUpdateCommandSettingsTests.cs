// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Spectre.Console;
using Settings = DotnetCheckUpdates.Commands.CheckUpdate.CheckUpdateCommand.Settings;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCommandSettingsTests
{
    [Fact]
    public void DefaultSettingsAreValid()
    {
        var settings = new Settings();
        settings.Validate().Should().BeEquivalentTo(ValidationResult.Success());
    }

    [Fact]
    public void SpecifyingBothProjectAndSolutionIsAnError()
    {
        var settings = new Settings() { Project = "not null", Solution = "not null", };

        var result = settings.Validate();
        result.Successful.Should().BeFalse();
        result.Message.Should().Be("Only one of --project, --solution may be specified.");
    }

    [Fact]
    public void DepthGreaterThanMaximumIsInvalid()
    {
        var settings = new Settings() { Depth = int.MaxValue, };

        var result = settings.Validate();
        result.Successful.Should().BeFalse();
        result.Message.Should().Be("--depth must be between 0 and 16.");
    }

    [Fact]
    public void DepthBelowZeroIsInvalid()
    {
        var settings = new Settings() { Depth = -1, };

        var result = settings.Validate();
        result.Successful.Should().BeFalse();
        result.Message.Should().Be("--depth must be between 0 and 16.");
    }

    [Fact]
    public void ConcurrencyBelowOneIsInvalid()
    {
        var settings = new Settings() { Concurrency = 0, };

        var result = settings.Validate();
        result.Successful.Should().BeFalse();
        result.Message.Should().Be("--concurrency must be between 1 and 32.");
    }

    [Fact]
    public void ConcurrencyAboveMaximumIsInvalid()
    {
        var settings = new Settings() { Concurrency = int.MaxValue, };

        var result = settings.Validate();
        result.Successful.Should().BeFalse();
        result.Message.Should().Be("--concurrency must be between 1 and 32.");
    }

    [Fact]
    public void CanReportMoreThanOneValidationError()
    {
        var settings = new Settings()
        {
            Project = "not null",
            Solution = "not null",
            Depth = int.MaxValue,
            Concurrency = int.MaxValue,
        };

        var result = settings.Validate();
        result.Successful.Should().BeFalse();
        result
            .Message.Should()
            .Be(
                "Only one of --project, --solution may be specified. --depth must be between 0 and 16. --concurrency must be between 1 and 32."
            );
    }
}
