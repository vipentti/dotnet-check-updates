// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;
using NuGet.Configuration;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal partial class NuGetSettingsProvider(
    ILogger<NuGetSettingsProvider> logger,
    ICurrentDirectory currentDirectory
)
{
    private ISettings? _settings;
    public ISettings NuGetSettings => _settings ??= LoadSettings();

    private ISettings LoadSettings()
    {
        if (string.IsNullOrWhiteSpace(currentDirectory.CurrentDirectory))
        {
            throw new InvalidOperationException("Root must be set.");
        }

        LogLoadingDefaultSettings(logger, currentDirectory.CurrentDirectory);

        var config = Settings.LoadDefaultSettings(currentDirectory.CurrentDirectory);

        LogFoundConfigurations(logger, config.GetConfigFilePaths());

        return config;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading default settings from {Root}")]
    private static partial void LogLoadingDefaultSettings(ILogger logger, string root);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Found NuGet configurations: {@ConfigFilePaths}"
    )]
    private static partial void LogFoundConfigurations(
        ILogger logger,
        IEnumerable<string> configFilePaths
    );
}
