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
            // Derive active TFM and configuration from test assembly location, e.g. .../bin/Release/net10.0/ or .../bin/Debug/net8.0/
            var tfm = Path.GetFileName(Path.GetDirectoryName(baseDir) ?? "net10.0");
            var config = Path.GetFileName(
                Path.GetDirectoryName(Path.GetDirectoryName(baseDir) ?? "") ?? "Release"
            );
            if (config is not ("Release" or "Debug"))
            {
                config = "Release";
            }
            foreach (var c in new[] { config, "Release", "Debug" }.Distinct(StringComparer.Ordinal))
            {
                foreach (
                    var t in new[] { tfm, "net10.0", "net9.0", "net8.0" }.Distinct(
                        StringComparer.Ordinal
                    )
                )
                {
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
                            c,
                            t,
                            "dotnet-check-updates.dll"
                        )
                    );
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return Path.GetFullPath(
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
        stderr.Should().Contain("does not exist");
    }

    [Fact]
    public async Task Cli_Logging_GoesToStderr_StdoutValidJson()
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
        var (code, stdout, stderr) = await RunCliAsync(
            ["--json", "--cwd", dir.Path],
            env: new() { ["DCU_ENABLE_LOGGING"] = "1" }
        );
        code.Should().Be(0);
        JsonDocument
            .Parse(stdout)
            .RootElement.GetProperty("schemaVersion")
            .GetInt32()
            .Should()
            .Be(1);
        stdout.Should().NotContain("CACHE");
        stdout.Should().NotContain("info:");
        // Logging should appear on stderr when enabled, not on stdout - even with no packages, logger still initializes
        // At minimum stdout must remain valid JSON and not contain log markers
        stderr.Should().NotContain("\"schemaVersion\"");
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
        using var dir = TempDir.Create();
        var projPath = Path.Combine(dir.Path, "a.csproj");
        File.WriteAllText(projPath, ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")]));
        // Make file read-only to force save failure on --upgrade
        var originalAttrs = File.GetAttributes(projPath);
        File.SetAttributes(projPath, originalAttrs | FileAttributes.ReadOnly);
        try
        {
            var (code, stdout, stderr) = await RunCliAsync([
                "--json",
                "--upgrade",
                "--cwd",
                dir.Path,
            ]);
            code.Should().NotBe(0);
            stdout.Should().BeEmpty();
        }
        finally
        {
            File.SetAttributes(projPath, originalAttrs);
        }
    }

    [Fact]
    public async Task Cli_RestoreFailure_StderrWithEmptyStdout()
    {
        using var dir = TempDir.Create();
        var projPath = Path.Combine(dir.Path, "a.csproj");
        // Use Invalid SDK so dotnet restore always fails, while our tool still upgrades PackageReference
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
            "--cwd",
            dir.Path,
        ]);
        code.Should().NotBe(0);
        stdout.Should().BeEmpty();
        stderr.Should().NotBeEmpty();
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
