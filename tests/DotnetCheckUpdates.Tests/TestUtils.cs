// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.NuGetUtils;
using DotnetCheckUpdates.Core.ProjectModel;
using NuGet.Frameworks;
using NuGet.Protocol.Core.Types;
using Nuke.Common.IO;

namespace DotnetCheckUpdates.Tests;

internal static class TestUtils
{
    public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static string TestPathRoot() => IsWindows() ? @"C:\" : "/";

    public static FullPath RootedTestPath(string? relativePath = default) =>
        FullPath.Create(PathCombine(TestPathRoot(), relativePath ?? ""));

    public static string PathCombine(this string left, string right) =>
        PathConstruction.NormalizePath(PathConstruction.Combine(left, right));

    public static async Task<ProjectFile> ReadProjectFile(
        this string cwd,
        IFileSystem fs,
        string path
    )
    {
        return await new ProjectFileReader(fs).ReadProjectFile(cwd.PathCombine(path));
    }

    public static async Task<ProjectFile> ReadProjectFile(
        this FullPath cwd,
        IFileSystem fs,
        string path
    )
    {
        return await new ProjectFileReader(fs).ReadProjectFile(cwd.PathCombine(path));
    }

    public static IPackageSearchMetadata BasicMockPackage(string id, string version)
    {
        var sub = Substitute.For<IPackageSearchMetadata>();
        sub.Identity.Returns(
            new NuGet.Packaging.Core.PackageIdentity(
                id,
                NuGet.Versioning.NuGetVersion.Parse(version)
            )
        );
        return sub;
    }

    public static INuGetService SetupMockPackages(IEnumerable<MockUpgrade> packages)
    {
        var client = Substitute.For<INuGetService>();

        foreach (var (name, versions, frameworks) in packages)
        {
            client
                .GetPackageVersionsAsync(Arg.Is(name), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromResult(
                        versions.ConvertAll(VersionExtensions.ToNuGetVersion).AsEnumerable()
                    )
                );

            client
                .GetSupportedFrameworksAsync(
                    Arg.Is(name),
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(
                    Task.FromResult(
                        frameworks.Select(VersionExtensions.ToNuGetFramework).ToImmutableHashSet()
                    )
                );
        }

        return client;
    }

    public static INuGetService SetupSimpleNuGetService(PackageDictionary packages)
    {
        var client = Substitute.For<INuGetService>();

        foreach (var (name, versions) in packages)
        {
            client
                .GetPackageVersionsAsync(Arg.Is(name), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(versions.AsEnumerable()));
        }

        client
            .GetSupportedFrameworksAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(
                    ImmutableHashSet.Create(
                        FrameworkConstants.CommonFrameworks.Net50,
                        FrameworkConstants.CommonFrameworks.Net60,
                        FrameworkConstants.CommonFrameworks.Net70,
                        FrameworkConstants.CommonFrameworks.NetStandard20,
                        FrameworkConstants.CommonFrameworks.NetStandard21
                    )
                )
            );

        return client;
    }

    public static INuGetService SetupMockNuGetService(params IPackageSearchMetadata[] packages)
    {
        var client = Substitute.For<INuGetService>();

        var packagesByVersions = packages
            .GroupBy(it => it.Identity.Id)
            .ToDictionary(
                group => group.Key,
                group => group.Select(it => it.Identity.Version).ToArray()
            );

        foreach (var (name, versions) in packagesByVersions)
        {
            client
                .GetPackageVersionsAsync(Arg.Is(name), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(versions.AsEnumerable()));
        }

        client
            .GetSupportedFrameworksAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(
                    ImmutableHashSet.Create(
                        FrameworkConstants.CommonFrameworks.Net50,
                        FrameworkConstants.CommonFrameworks.Net60,
                        FrameworkConstants.CommonFrameworks.Net70,
                        FrameworkConstants.CommonFrameworks.NetStandard20,
                        FrameworkConstants.CommonFrameworks.NetStandard21
                    )
                )
            );

        return client;
    }

    public static MockFileSystem SetupFileSystem(
        string currentDirectory,
        Dictionary<string, string> fileContents
    )
    {
        return new MockFileSystem(
            fileContents.ToDictionary(kvp => kvp.Key, kvp => new MockFileData(kvp.Value)),
            new MockFileSystemOptions() { CurrentDirectory = currentDirectory }
        );
    }
}
