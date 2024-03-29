﻿// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal class ProjectFileReader
{
    private readonly IFileSystem _fileSystem;

    public ProjectFileReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<ProjectFile> ReadProjectFile(string filePath)
    {
        var content = await _fileSystem.File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return ProjectFileParser.ParseLessStrictProjectFile(content, filePath);
    }
}
