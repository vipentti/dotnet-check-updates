// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;
using DotnetCheckUpdates;
using DotnetCheckUpdates.Commands.CheckUpdate;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.NuGetUtils;
using DotnetCheckUpdates.Core.ProjectModel;
using DotnetCheckUpdates.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

var definedArgs = new HashSet<string>() { "--show-stack-trace" };

var showStackTrace = Array.Exists(args, it => it == "--show-stack-trace");

var nugetApiBaseUrl = new Uri(NuGetConstants.V3FeedUrl, UriKind.Absolute).GetLeftPart(
    UriPartial.Authority
);

var cmdArgs = args.Where(it => !definedArgs.Contains(it)).ToArray();

var (jsonMode, jsonError) = JsonModeClassifier.Classify(cmdArgs);

if (jsonMode == JsonMode.Reject)
{
    await Console.Error.WriteLineAsync(jsonError);
    return -1;
}

var isJsonIntent = jsonMode == JsonMode.JsonIntent;

var services = new ServiceCollection();

var useLoggingEnvVar = Environment.GetEnvironmentVariable("DCU_ENABLE_LOGGING")?.ToLowerInvariant();
var logLevelEnvVar =
    Environment.GetEnvironmentVariable("DCU_LOGLEVEL")?.ToLowerInvariant() ?? "Information";

services.AddLogging(logger =>
{
    logger.ClearProviders();

    if (useLoggingEnvVar is not null && (useLoggingEnvVar == "true" || useLoggingEnvVar == "1"))
    {
        logger
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            })
            .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        logger.AddFilter("NuGetLogger", LogLevel.Warning);

        if (isJsonIntent)
        {
            logger.Services.Configure<ConsoleLoggerOptions>(opts =>
            {
                opts.LogToStandardErrorThreshold = LogLevel.Trace;
            });
        }

        if (Enum.TryParse(logLevelEnvVar, ignoreCase: true, out LogLevel level))
        {
            logger.SetMinimumLevel(level);
        }
        else
        {
            logger.SetMinimumLevel(LogLevel.Information);
        }
    }
});

services.AddSingleton(_ => new SourceCacheContext());
services.AddSingleton<NuGetServiceFactory>();
services.AddSingleton<INuGetService, MultiSourceNuGetService>();
services.AddSingleton<ISolutionParser, DefaultSolutionParser>();
services.AddSingleton<PackageUpgradeService>();
services.AddSingleton<ProjectFileReader>();
services.AddSingleton<ProjectDiscovery>();
services.AddSingleton<IFileFinder, FileFinder>();
services.AddSingleton<IFileSystem>(_ => new FileSystem());
services.AddSingleton<NuGetSettingsProvider>();
services.AddSingletonVia<IFeatureCollection, FeatureCollection>();
services.AddSingletonVia<INuGetPackageSourceProvider, NuGetConfigurationPackageSourceProvider>();
services.AddSingletonVia<IPackageUpgradeServiceFactory, PackageUpgradeServiceFactory>();
services.AddSingletonVia<ICurrentDirectory, CurrentDirectoryProvider>();

services
    .AddHttpClient<NuGetApiClient>(client => client.BaseAddress = new Uri(nugetApiBaseUrl))
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler() { AutomaticDecompression = System.Net.DecompressionMethods.All }
    );

using var applicationExitHandler = new ApplicationExitHandler();

services.AddSingleton(applicationExitHandler);

IAnsiConsole stderrConsole = AnsiConsole.Create(
    new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) }
);

if (isJsonIntent)
{
    services.AddSingleton<IAnsiConsole>(stderrConsole);
}

var app = new CommandApp<CheckUpdateCommand>(new TypeRegistrar(services));

app.WithDescription(
    $"""
{CliConstants.CliName} checks for possible upgrades of NuGet packages in C# projects.

By default, {CliConstants.CliName} will search the current directory for C# project (.csproj) files.
If no project files are found, it will then search for solution (.sln) files in the current directory.
"""
);

app.Configure(config =>
{
    config.SetApplicationName(CliConstants.CliName);
    config.UseStrictParsing();
    config.PropagateExceptions();
    config.ValidateExamples();
    if (isJsonIntent)
    {
        config.ConfigureConsole(stderrConsole);
    }
});

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    applicationExitHandler.Exit(force: true);
};

IAnsiConsole diagnosticsConsole = isJsonIntent ? stderrConsole : AnsiConsole.Console;

try
{
    return await app.RunAsync(cmdArgs);
}
catch (CommandParseException ex)
{
    if (!TryRenderPrettyException(ex, diagnosticsConsole))
    {
        diagnosticsConsole.WriteException(ex, ExceptionFormats.Default);
    }

    diagnosticsConsole.MarkupLine("Run [cyan]dcu --help[/] to see available options.");
    diagnosticsConsole.MarkupLine("");
    return -1;
}
catch (CommandRuntimeException ex)
{
    WriteException(ex, diagnosticsConsole);
    return -1;
}
catch (PromptCanceledException)
{
    return -1;
}
catch (TaskCanceledException ex)
{
    if (!TryRenderPrettyException(ex, diagnosticsConsole))
    {
        diagnosticsConsole.WriteException(ex, ExceptionFormats.Default);
    }
    return -1;
}
catch (Exception ex)
{
    WriteException(ex, diagnosticsConsole);
    return -1;
}

void WriteException(Exception ex, IAnsiConsole console)
{
#pragma warning disable S1854
    var showStack = showStackTrace;
#if DEBUG
    showStack = true;
#endif
#pragma warning disable S2583
    if (showStack || !TryRenderPrettyException(ex, console))
    {
        console.WriteException(ex, ExceptionFormats.Default);
    }
#pragma warning restore S2583
#pragma warning restore S1854
}

bool TryRenderPrettyException(Exception ex, IAnsiConsole console)
{
    if (GetRenderableErrorMessage(ex) is List<IRenderable?> pretty)
    {
        foreach (var item in pretty)
        {
            if (item is not null)
            {
                console.Write(item);
            }
        }
        return true;
    }

    return false;
}

List<IRenderable?>? GetRenderableErrorMessage(Exception ex, bool convert = true)
{
    if (ex is CommandAppException renderable && renderable.Pretty is not null)
    {
        return [renderable.Pretty];
    }

    if (convert)
    {
        var converted = new List<IRenderable?>
        {
            new Markup($"[red]Error:[/] {ex.Message.EscapeMarkup()}{Environment.NewLine}"),
        };

        if (ex.InnerException is not null)
        {
            var innerRenderable = GetRenderableErrorMessage(ex.InnerException, convert: false);
            if (innerRenderable is not null)
            {
                converted.AddRange(innerRenderable);
            }
        }

        return converted;
    }

    return null;
}
