// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core;

internal class PromptCanceledException : Exception
{
    public PromptCanceledException()
    {
    }

    public PromptCanceledException(string? message) : base(message)
    {
    }

    public PromptCanceledException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
