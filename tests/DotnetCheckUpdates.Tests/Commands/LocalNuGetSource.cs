// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;

namespace DotnetCheckUpdates.Tests.Commands;

internal sealed class LocalNuGetSource : IDisposable
{
    public string Path { get; }

    private readonly string _root;

    private LocalNuGetSource(string path, string root)
    {
        Path = path;
        _root = root;
    }

    public static LocalNuGetSource CreateWithFlurl(string version)
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dcu-feed-" + Guid.NewGuid().ToString("N")
        );
        var feed = System.IO.Path.Combine(root, "feed");
        Directory.CreateDirectory(feed);
        var projDir = System.IO.Path.Combine(root, "pkg");
        Directory.CreateDirectory(projDir);
        File.WriteAllText(
            System.IO.Path.Combine(projDir, "Flurl.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <PackageId>Flurl</PackageId>
                <Version>{version}</Version>
                <Authors>Test</Authors>
                <Description>Test package for deterministic CLI tests</Description>
              </PropertyGroup>
            </Project>
            """.Trim()
        );
        File.WriteAllText(System.IO.Path.Combine(projDir, "Class1.cs"), "public class C { }");
        var psi = new ProcessStartInfo(
            "dotnet",
            $"pack \"{System.IO.Path.Combine(projDir, "Flurl.csproj")}\" -c Release -o \"{feed}\" -v q"
        )
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet pack failed ({proc.ExitCode}): {stderr} {stdout}"
            );
        }

        var nupkgs = Directory.GetFiles(feed, "*.nupkg");
        if (nupkgs.Length == 0)
        {
            throw new InvalidOperationException(
                $"dotnet pack produced no nupkg in {feed}: stdout={stdout} stderr={stderr}"
            );
        }

        return new LocalNuGetSource(feed, root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch { }
    }
}
