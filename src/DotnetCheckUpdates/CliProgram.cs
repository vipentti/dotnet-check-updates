// Copyright 2023-2024 Ville Penttinen
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
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

var definedArgs = new HashSet<string>() { "--show-stack-trace" };

var showStackTrace = args.Any(it => it == "--show-stack-trace");

var nugetSourceIndex = Array.FindIndex(args, it => it == "--nuget-source");
var nugetSource =
    nugetSourceIndex > -1 && nugetSourceIndex + 1 < args.Length
        ? args[nugetSourceIndex + 1]
        : "https://api.nuget.org/v3/index.json";

var nugetApiBaseUrl = new Uri(nugetSource, UriKind.Absolute).GetLeftPart(UriPartial.Authority);

var cmdArgs = args.Where(it => !definedArgs.Contains(it)).ToArray();

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

        if (Enum.TryParse(logLevelEnvVar, ignoreCase: true, out LogLevel level))
        {
            logger.SetMinimumLevel(level);
        }
        else
        {
            logger.SetMinimumLevel(LogLevel.Information);
        }
    }
#if false
    else
    {
        logger.AddConsole().AddFilter("System.Net.Http.HttpClient", LogLevel.Information);
        logger.SetMinimumLevel(LogLevel.Trace);
    }
#endif
});

services.AddSingleton(_ => new SourceCacheContext());
services.AddSingleton<NuGetServiceFactory>();
services.AddSingleton(new NuGetPackageSourceProvider([new PackageSource(nugetSource)]));
services.AddSingleton<INuGetService, MultiSourceNuGetService>();
services.AddSingleton<ISolutionParser, DefaultSolutionParser>();
services.AddSingleton<PackageUpgradeService>();
services.AddSingleton<ProjectFileReader>();
services.AddSingleton<ProjectDiscovery>();
services.AddSingleton<IFileFinder, FileFinder>();
services.AddSingleton<IFileSystem>(_ => new FileSystem());

services.AddSingletonVia<INuGetPackageSourceProvider, NuGetConfigurationPackageSourceProvider>();
services.AddSingletonVia<IPackageUpgradeServiceFactory, PackageUpgradeServiceFactory>();
services.AddSingletonVia<ICurrentDirectory, CurrentDirectoryProvider>();

services
    .AddHttpClient<NuGetApiClient>(client => client.BaseAddress = new Uri(nugetApiBaseUrl))
    .ConfigurePrimaryHttpMessageHandler(
        () =>
            new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            }
    );

using var applicationExitHandler = new ApplicationExitHandler();

services.AddSingleton(applicationExitHandler);

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
});

Console.CancelKeyPress += (_, e) =>
{
    // We'll stop the process manually by using the CancellationToken
    e.Cancel = true;
    applicationExitHandler.Exit(force: true);
};

try
{
    return await app.RunAsync(cmdArgs);
}
catch (CommandParseException ex)
{
    if (!TryRenderPrettyException(ex))
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.Default);
    }

    AnsiConsole.MarkupLine("Run [cyan]dcu --help[/] to see available options.");
    AnsiConsole.MarkupLine("");
    return -1;
}
catch (CommandRuntimeException ex)
{
    WriteException(ex);
    return -1;
}
catch (PromptCanceledException)
{
    return -1;
}
catch (TaskCanceledException ex)
{
    if (!TryRenderPrettyException(ex))
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.Default);
    }
    return -1;
}
catch (Exception ex)
{
    WriteException(ex);
    return -1;
}

void WriteException(Exception ex)
{
    var showStack = showStackTrace;
#if DEBUG
    showStack = true;
#endif
    if (showStack || !TryRenderPrettyException(ex))
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.Default);
    }
}

static bool TryRenderPrettyException(Exception ex)
{
    if (GetRenderableErrorMessage(ex) is List<IRenderable?> pretty)
    {
        foreach (var item in pretty)
        {
            if (item is not null)
            {
                AnsiConsole.Write(item);
            }
        }
        return true;
    }

    return false;
}

static List<IRenderable?>? GetRenderableErrorMessage(Exception ex, bool convert = true)
{
    if (ex is CommandAppException renderable && renderable.Pretty is not null)
    {
        return [renderable.Pretty];
    }

    if (convert)
    {
        var converted = new List<IRenderable?>
        {
            new Markup($"[red]Error:[/] {ex.Message.EscapeMarkup()}{Environment.NewLine}")
        };

        // Got a renderable inner exception
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
