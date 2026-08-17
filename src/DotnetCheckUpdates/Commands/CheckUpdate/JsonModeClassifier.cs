// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal enum JsonMode
{
    Default,
    JsonIntent,
    OpenCli,
    Reject,
}

internal static class JsonModeClassifier
{
    public static (JsonMode Mode, string? Error) Classify(string[] cmdArgs)
    {
        if (
            cmdArgs.Length > 0
            && string.Equals(cmdArgs[0], "--help-dump-opencli", StringComparison.OrdinalIgnoreCase)
        )
        {
            return (JsonMode.OpenCli, null);
        }

        var boundaryIndex = Array.IndexOf(cmdArgs, "--");
        var preBoundaryEnd = boundaryIndex >= 0 ? boundaryIndex : cmdArgs.Length;

        for (var i = 0; i < preBoundaryEnd; i++)
        {
            var arg = cmdArgs[i];
            if (
                arg.StartsWith("--json=", StringComparison.Ordinal)
                || arg.StartsWith("--json:", StringComparison.Ordinal)
            )
            {
                return (JsonMode.Reject, "Error: --json does not accept a value. Use bare --json.");
            }

            if (
                string.Equals(arg, "--json", StringComparison.Ordinal)
                && i + 1 < preBoundaryEnd
                && (
                    string.Equals(cmdArgs[i + 1], "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cmdArgs[i + 1], "false", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return (JsonMode.Reject, "Error: --json does not accept a value. Use bare --json.");
            }
        }

        for (var i = 0; i < preBoundaryEnd; i++)
        {
            if (string.Equals(cmdArgs[i], "--json", StringComparison.Ordinal))
            {
                return (JsonMode.JsonIntent, null);
            }
        }

        return (JsonMode.Default, null);
    }
}
