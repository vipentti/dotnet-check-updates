// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;

namespace DotnetCheckUpdates.Core;

internal class CurrentDirectoryProvider(IFileSystem fileSystem) : ICurrentDirectory
{
    public string CurrentDirectory { get; internal set; } =
        fileSystem.Directory.GetCurrentDirectory();
}
