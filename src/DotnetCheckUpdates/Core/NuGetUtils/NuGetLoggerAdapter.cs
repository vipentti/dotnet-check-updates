// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal class NuGetLoggerAdapter : NuGet.Common.ILogger
{
    private readonly ILogger _logger;

    public NuGetLoggerAdapter(ILogger logger)
    {
        _logger = logger;
    }

    public static LogLevel MapLogLevel(NuGet.Common.LogLevel level) =>
        level switch
        {
            NuGet.Common.LogLevel.Debug => LogLevel.Debug,
            NuGet.Common.LogLevel.Verbose => LogLevel.Trace,
            NuGet.Common.LogLevel.Information => LogLevel.Information,
            NuGet.Common.LogLevel.Minimal => LogLevel.Trace,
            NuGet.Common.LogLevel.Warning => LogLevel.Warning,
            NuGet.Common.LogLevel.Error => LogLevel.Error,
            _ => throw new NotImplementedException(level.ToString()),
        };

    public void Log(NuGet.Common.LogLevel level, string data)
    {
        _logger.Log(MapLogLevel(level), "{data}", data);
    }

    public void Log(NuGet.Common.ILogMessage message)
    {
        _logger.Log(MapLogLevel(message.Level), "{@Details}", message);
    }

    public Task LogAsync(NuGet.Common.LogLevel level, string data)
    {
        _logger.Log(MapLogLevel(level), "{data}", data);
        return Task.CompletedTask;
    }

    public Task LogAsync(NuGet.Common.ILogMessage message)
    {
        _logger.Log(MapLogLevel(message.Level), "{@Details}", message);
        return Task.CompletedTask;
    }

    public void LogDebug(string data)
    {
        _logger.LogDebug("{data}", data);
    }

    public void LogError(string data)
    {
        _logger.LogError("{data}", data);
    }

    public void LogInformation(string data)
    {
        _logger.LogInformation("{data}", data);
    }

    public void LogInformationSummary(string data)
    {
        _logger.LogInformation("{data}", data);
    }

    public void LogMinimal(string data)
    {
        _logger.LogTrace("{data}", data);
    }

    public void LogVerbose(string data)
    {
        _logger.LogTrace("{data}", data);
    }

    public void LogWarning(string data)
    {
        _logger.LogWarning("{data}", data);
    }
}
