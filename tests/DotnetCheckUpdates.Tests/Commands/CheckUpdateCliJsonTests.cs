// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using System.Text.Json;

namespace DotnetCheckUpdates.Tests.Commands;

public class CheckUpdateCliJsonTests
{
    private static string DotnetCheckUpdatesDll =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "DotnetCheckUpdates",
                "bin",
                "Release",
                "net10.0",
                "dotnet-check-updates.dll"
            )
        );

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string[] args,
        string? cwd = null,
        Dictionary<string, string>? env = null
    )
    {
        var psi = new ProcessStartInfo("dotnet", $"\"{DotnetCheckUpdatesDll}\" {string.Join(" ", args.Select(EscapeArg))}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = cwd ?? Path.GetTempPath(),
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
            ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")])
        );
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--cwd", dir.Path]);
        code.Should().Be(0);
        var doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        stderr.Should().BeEmpty();
        doc.RootElement.GetProperty("checkedFiles")[0].GetProperty("packages")[0].GetProperty("name").GetString().Should().Be("Flurl");
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
    public async Task Cli_HelpAfterBoundary_IsJsonExecutionNotHelp()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(
            Path.Combine(dir.Path, "a.csproj"),
            ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")])
        );
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--", "--help", "--cwd", dir.Path]);
        code.Should().Be(0);
        JsonDocument.Parse(stdout).RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cli_JsonAfterBoundary_IsDefaultMode()
    {
        using var dir = TempDir.Create();
        File.WriteAllText(
            Path.Combine(dir.Path, "a.csproj"),
            ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")])
        );
        var (code, stdout, stderr) = await RunCliAsync(["--", "--json", "--cwd", dir.Path]);
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
        var (code, stdout, stderr) = await RunCliAsync(["--show-stack-trace", "--help-dump-opencli", "--json"]);
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
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--project", "/nonexistent.csproj"]);
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
            ProjectFileUtils.ProjectFileXml([("Flurl", "3.0.0")])
        );
        var (code, stdout, stderr) = await RunCliAsync(
            ["--json", "--cwd", dir.Path],
            env: new() { ["DCU_ENABLE_LOGGING"] = "1" }
        );
        code.Should().Be(0);
        JsonDocument.Parse(stdout).RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        stdout.Should().NotContain("CACHE");
    }

    [Fact]
    public async Task Cli_HelpShortCircuit_EmptyStdout()
    {
        var (code, stdout, stderr) = await RunCliAsync(["--json", "--help"]);
        stdout.Should().BeEmpty();
        stderr.Should().Contain("dotnet-check-updates");
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dcu-test-" + Guid.NewGuid().ToString("N"));

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
            catch
            {
            }
        }
    }
}
