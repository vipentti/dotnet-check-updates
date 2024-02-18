// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Microsoft.Extensions.Logging;

namespace DotnetCheckUpdates.Core.NuGetUtils;

internal partial class NuGetLoggerAdapter : NuGet.Common.ILogger
{
    private readonly ILogger _logger;

    public NuGetLoggerAdapter(ILogger logger)
    {
        _logger = logger;
    }

    [LoggerMessage(Message = "{Data}")]
    private static partial void LogData(ILogger logger, LogLevel level, string data);

    [LoggerMessage(Message = "{@Details}")]
    private static partial void LogDetails(ILogger logger, LogLevel level, object details);

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
        LogData(_logger, MapLogLevel(level), data);
    }

    public void Log(NuGet.Common.ILogMessage message)
    {
        LogDetails(_logger, MapLogLevel(message.Level), message);
    }

    public Task LogAsync(NuGet.Common.LogLevel level, string data)
    {
        LogData(_logger, MapLogLevel(level), data);
        return Task.CompletedTask;
    }

    public Task LogAsync(NuGet.Common.ILogMessage message)
    {
        LogDetails(_logger, MapLogLevel(message.Level), message);
        return Task.CompletedTask;
    }

    public void LogDebug(string data)
    {
        LogData(_logger, LogLevel.Debug, data);
    }

    public void LogError(string data)
    {
        LogData(_logger, LogLevel.Error, data);
    }

    public void LogInformation(string data)
    {
        LogData(_logger, LogLevel.Information, data);
    }

    public void LogInformationSummary(string data)
    {
        LogData(_logger, LogLevel.Information, data);
    }

    public void LogMinimal(string data)
    {
        LogData(_logger, LogLevel.Trace, data);
    }

    public void LogVerbose(string data)
    {
        LogData(_logger, LogLevel.Trace, data);
    }

    public void LogWarning(string data)
    {
        LogData(_logger, LogLevel.Warning, data);
    }
}
