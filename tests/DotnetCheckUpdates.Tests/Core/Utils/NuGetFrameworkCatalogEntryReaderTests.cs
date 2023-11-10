// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.Utils;

namespace DotnetCheckUpdates.Tests.Core.Utils;

public class NuGetFrameworkCatalogEntryReaderTests
{
    [Fact]
    public void CanReadFrameworksFromCatalogDependencyGroups()
    {
        var json = """
{
  "dependencyGroups": [
    {
      "targetFramework": "net6.0"
    },
    {
      "targetFramework": "net7.0"
    },
    {
      "dependencies": [
        {
          "@type": "PackageDependency",
          "id": "System.Memory",
          "range": "[4.5.5, )"
        }
      ],
      "targetFramework": ".NETStandard2.0"
    },
    {
      "targetFramework": ".NETStandard2.1"
    }
  ],
}
""".Trim();

        var frameworks = NuGetFrameworkCatalogEntryReader.ReadFrameworksFromCatalogJson(json);

        frameworks.Should().HaveCount(4);
        frameworks.Should().ContainEquivalentOf("net6.0".ToNuGetFramework());
        frameworks.Should().ContainEquivalentOf("net7.0".ToNuGetFramework());
        frameworks.Should().ContainEquivalentOf("netstandard2.0".ToNuGetFramework());
        frameworks.Should().ContainEquivalentOf("netstandard2.1".ToNuGetFramework());
    }

    [Fact]
    public void CanReadFrameworksFromCatalogPackageEntries()
    {
        var json = """
{
  "packageEntries": [
    {
      "fullName": "TestProjectExample.nuspec",
      "name": "TestProjectExample.nuspec"
    },
    {
      "fullName": "lib/net6.0/TestProjectExample.dll",
      "name": "TestProjectExample.dll"
    },
    {
      "fullName": "lib/net6.0/TestProjectExample.xml",
      "name": "TestProjectExample.xml"
    },
    {
      "fullName": "lib/net7.0/TestProjectExample.dll",
      "name": "TestProjectExample.dll"
    },
    {
      "fullName": "lib/net7.0/TestProjectExample.xml",
      "name": "TestProjectExample.xml"
    },
    {
      "fullName": "lib/netstandard2.0/TestProjectExample.dll",
      "name": "TestProjectExample.dll"
    },
    {
      "fullName": "lib/netstandard2.0/TestProjectExample.xml",
      "name": "TestProjectExample.xml"
    },
    {
      "fullName": "lib/netstandard2.1/TestProjectExample.dll",
      "name": "TestProjectExample.dll"
    },
    {
      "fullName": "lib/netstandard2.1/TestProjectExample.xml",
      "name": "TestProjectExample.xml"
    },
    {
      "fullName": "LICENSE.md",
      "name": "LICENSE.md"
    },
    {
      "fullName": "icon.png",
      "name": "icon.png"
    },
    {
      "fullName": "README.md",
      "name": "README.md"
    },
    {
      "fullName": ".signature.p7s",
      "name": ".signature.p7s"
    }
  ],
}
""".Trim();

        var frameworks = NuGetFrameworkCatalogEntryReader.ReadFrameworksFromCatalogJson(json);

        frameworks.Should().HaveCount(4);
        frameworks.Should().ContainEquivalentOf("net6.0".ToNuGetFramework());
        frameworks.Should().ContainEquivalentOf("net7.0".ToNuGetFramework());
        frameworks.Should().ContainEquivalentOf("netstandard2.0".ToNuGetFramework());
        frameworks.Should().ContainEquivalentOf("netstandard2.1".ToNuGetFramework());
    }
}
