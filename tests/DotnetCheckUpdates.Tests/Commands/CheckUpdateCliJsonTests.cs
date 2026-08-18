// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using System.Text.Json;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCliJsonTests
{
    private static string DotnetCheckUpdatesDll
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            // Derive exact TFM and configuration from test assembly location, e.g. .../bin/Release/net10.0/
            var trimmed = baseDir.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            var dirInfo = new DirectoryInfo(trimmed);
            var tfm = dirInfo.Name;
            var config = dirInfo.Parent?.Name ?? "";
            if (string.IsNullOrWhiteSpace(tfm) || string.IsNullOrWhiteSpace(config))
            {
                throw new InvalidOperationException(
                    $"Unable to derive TFM/config from test assembly location: {baseDir}"
                );
            }
            if (config is not ("Release" or "Debug"))
            {
                throw new InvalidOperationException(
                    $"Unexpected configuration '{config}' derived from {baseDir}; expected Release or Debug"
                );
            }
            var candidate = Path.GetFullPath(
                Path.Combine(
                    baseDir,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "DotnetCheckUpdates",
                    "bin",
                    config,
                    tfm,
                    "dotnet-check-updates.dll"
                )
            );
            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException(
                    $"Expected product artifact not found at {candidate}. Build {config}/{tfm} first (dotnet build -c {config}).",
                    candidate
                );
            }
            return candidate;
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string[] args,
        string? cwd = null,
        Dictionary<string, string>? env = null
    )
    {
        var psi = new ProcessStartInfo(
            "dotnet",
            $"\"{DotnetCheckUpdatesDll}\" {string.Join(" ", args.Select(EscapeArg))}"
        )
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = cwd ?? EmptyTempDir.Value,
        };
        if (env is not null)
        {
            foreach (var kv in env)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }
        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string EscapeArg(string arg)
    {
        if (arg.Contains(' ') || arg.Contains('"'))
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
        return arg;
    }

    // Spectre wraps exception/message text at the console width when no terminal is
    // attached (falling back to 80 columns), which can split an asserted substring
    // across lines. Remove line breaks so Contains works regardless of wrap width.
    private static string UnwrapLines(string value) => value.Replace("\r", "").Replace("\n", "");

    [Fact]
    public async Task Cli_BareJson_EmitsValidJsonOnStdout()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(
            Path.Combine(dir.Path, "a.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """.Trim()
        );
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--cwd", dir.Path]);
        code.Should().Be(0);
        var doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        stderr.Should().BeEmpty();
        doc.RootElement.GetProperty("checkedFiles")[0]
            .GetProperty("path")
            .GetString()
            .Should()
            .Contain("a.csproj");
    }

    [Theory]
    [InlineData("--json=true")]
    [InlineData("--json:true")]
    public async Task Cli_RejectsValueAssignedJsonForms(string arg)
    {
        var (code, stdout, stderr) = await RunCliAsync([arg]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("does not accept a value");
    }

    [Theory]
    [InlineData("--json", "true")]
    [InlineData("--json", "false")]
    public async Task Cli_RejectsBareJsonWithBoolNext(string first, string second)
    {
        var (code, stdout, stderr) = await RunCliAsync([first, second]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("does not accept a value");
    }

    [Theory]
    [InlineData("--json", "-uh")]
    [InlineData("--json", "-u?")]
    public async Task Cli_GroupedHelp_GoesToStderrWithEmptyStdout(string first, string second)
    {
        var (code, stdout, stderr) = await RunCliAsync([first, second]);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("dotnet-check-updates");
    }

    [Fact]
    public async Task Cli_MissingValue_FollowedByHelp_GoesToStderr()
    {
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--target"]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("UpgradeTarget");
    }

    [Fact]
    public async Task Cli_MissingValue_WithHelpToken_PreservesFrameworkStatus()
    {
        var (codeStd, stdoutStd, stderrStd) = await RunCliAsync(["--target", "--help"]);
        var (codeJson, stdoutJson, stderrJson) = await RunCliAsync([
            "--json",
            "--target",
            "--help",
        ]);
        codeJson.Should().Be(codeStd);
        stdoutJson.Should().BeEmpty();
        stderrJson.Should().Contain("dotnet-check-updates");
        (stdoutStd + stderrStd).Should().Contain("dotnet-check-updates");
    }

    [Fact]
    public async Task Cli_HelpAfterBoundary_IsJsonExecutionNotHelp()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(
            Path.Combine(dir.Path, "a.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """.Trim()
        );
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--", "--help"], cwd: dir.Path);
        code.Should().Be(0);
        JsonDocument
            .Parse(stdout)
            .RootElement.GetProperty("schemaVersion")
            .GetInt32()
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Cli_JsonAfterBoundary_IsDefaultMode()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(
            Path.Combine(dir.Path, "a.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """.Trim()
        );
        var (code, stdout, stderr) = await RunCliAsync(["--", "--json"], cwd: dir.Path);
        code.Should().Be(0);
        stdout.Should().Contain("Projects");
        stdout.Should().NotContain("schemaVersion");
    }

    [Fact]
    public async Task Cli_OpenCli_PreservesOutputOnStdout()
    {
        var (code, stdout, stderr) = await RunCliAsync(["--help-dump-opencli", "--json"]);
        code.Should().Be(0);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("opencli");
    }

    [Fact]
    public async Task Cli_OpenCli_CaseVariant_PreservesOutput()
    {
        var (code, stdout, stderr) = await RunCliAsync(["--HELP-DUMP-OPENCLI", "--json"]);
        code.Should().Be(0);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("opencli");
    }

    [Fact]
    public async Task Cli_OpenCli_WithShowStackTrace_PreservesOutput()
    {
        var (code, stdout, stderr) = await RunCliAsync([
            "--show-stack-trace",
            "--help-dump-opencli",
            "--json",
        ]);
        code.Should().Be(0);
        stderr.Should().BeEmpty();
        stdout.Should().Contain("opencli");
    }

    [Fact]
    public async Task Cli_UnknownOption_FailsWithEmptyStdout()
    {
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--unknown-option"]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("Unknown option");
    }

    [Fact]
    public async Task Cli_ValidationFailure_EmptyStdout()
    {
        var (code, stdout, stderr) = await RunCliAsync([
            "--json",
            "--project",
            "/nonexistent.csproj",
        ]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        UnwrapLines(stderr).Should().Contain("does not exist");
    }

    [Fact]
    public async Task Cli_Logging_GoesToStderr_StdoutValidJson()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(
            Path.Combine(dir.Path, "a.csproj"),
            ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")])
        );
        var (code, stdout, stderr) = await RunCliAsync(
            ["--json", "--cwd", dir.Path],
            env: new() { ["DCU_ENABLE_LOGGING"] = "1", ["DCU_LOGLEVEL"] = "Debug" }
        );
        code.Should().Be(0);
        var doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        stdout.Should().NotContain("CACHE");
        stdout.Should().NotContain("info:");
        // With packages and Debug level, settings log should be on stderr, not stdout
        stderr.Should().NotBeEmpty();
        stderr.Should().Contain("Settings");
        stdout.Should().NotContain("Settings");
    }

    [Fact]
    public async Task Cli_RuntimeFailure_MalformedProject_EmptyStdout()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(Path.Combine(dir.Path, "a.csproj"), "<Project><Invalid></Project>");
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--cwd", dir.Path]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Cli_RuntimeFailure_EmptyStdout()
    {
        var (code, stdout, stderr) = await RunCliAsync([
            "--json",
            "--cwd",
            "/nonexistent-" + Guid.NewGuid().ToString("N"),
        ]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("does not exist");
    }

    [Fact]
    public async Task Cli_GroupedHelp_ExitZeroAndStderr()
    {
        var (codeStd, stdoutStd, stderrStd) = await RunCliAsync(["-uh"]);
        var (codeJson, stdoutJson, stderrJson) = await RunCliAsync(["--json", "-uh"]);
        codeJson.Should().Be(codeStd);
        stdoutJson.Should().BeEmpty();
        stderrJson.Should().Contain("dotnet-check-updates");
        // In default mode help goes to stdout, in json mode to stderr, but content is same help text
        (stderrStd + stdoutStd)
            .Should()
            .Contain("dotnet-check-updates");
        stderrJson.Should().Contain("dotnet-check-updates");
    }

    [Fact]
    public async Task Cli_HelpShortCircuit_EmptyStdout()
    {
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--help"]);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("dotnet-check-updates");
        // Help short-circuit should preserve framework exit code (0) even with json intent
        var (codeStd, _, _) = await RunCliAsync(["--help"]);
        code.Should().Be(codeStd);
    }

    [Fact]
    public async Task Cli_SaveFailure_EmptyStdout()
    {
        using var localSource = LocalNuGetSource.CreateWithFlurl("4.0.0");
        using var dir = TempDir.Create();
        var projPath = Path.Combine(dir.Path, "a.csproj");
        File.WriteAllText(projPath, ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")]));
        var originalAttrs = File.GetAttributes(projPath);
        File.SetAttributes(projPath, originalAttrs | FileAttributes.ReadOnly);
        try
        {
            var (code, stdout, stderr) = await RunCliAsync([
                "--json",
                "--upgrade",
                "--nuget-source",
                localSource.Path,
                "--cwd",
                dir.Path,
            ]);
            code.Should().NotBe(0);
            stdout.Should().BeEmpty();
            stderr.Should().NotBeEmpty();
            UnwrapLines(stderr).Should().Contain("a.csproj");
        }
        finally
        {
            File.SetAttributes(projPath, originalAttrs);
        }
    }

    [Fact]
    public async Task Cli_RestoreFailure_StderrWithEmptyStdout()
    {
        using var localSource = LocalNuGetSource.CreateWithFlurl("4.0.0");
        using var dir = TempDir.Create();
        var projPath = Path.Combine(dir.Path, "a.csproj");
        // Use Invalid SDK so dotnet restore always fails, while our tool still upgrades PackageReference via local source
        File.WriteAllText(
            projPath,
            """
            <Project Sdk="Invalid.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Flurl" Version="3.0.0" /></ItemGroup>
            </Project>
            """.Trim()
        );
        var (code, stdout, stderr) = await RunCliAsync([
            "--json",
            "--upgrade",
            "--restore",
            "--nuget-source",
            localSource.Path,
            "--cwd",
            dir.Path,
        ]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().NotBeEmpty();
        stderr.Should().Contain("Invalid.Sdk");
        stderr.ToLowerInvariant().Should().Contain("restore");
    }

    private static class EmptyTempDir
    {
        public static readonly string Value = CreateEmpty();

        private static string CreateEmpty()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "dcu-empty-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(path);
            return path;
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dcu-test-" + Guid.NewGuid().ToString("N")
            );

        public static TempDir Create()
        {
            var d = new TempDir();
            Directory.CreateDirectory(d.Path);
            return d;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch { }
        }
    }
}
