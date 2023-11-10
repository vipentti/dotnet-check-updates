// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using System.IO.Abstractions;
using System.Xml.Linq;
using DotnetCheckUpdates.Core.Extensions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace DotnetCheckUpdates.Core.ProjectModel;

internal record ProjectFile(
    string FilePath,
    ImmutableArray<NuGetFramework> TargetFrameworks,
    ImmutableArray<PackageReference> PackageReferences
)
{
    public int PackageCount => PackageReferences.Length;

    public XDocument Xml { get; internal set; } = new();

    private bool NeedsUpdate { get; set; }

    public PackageReference? FindPackage(string name) => FindPackage(pkg => pkg.HasName(name));

    public PackageReference? FindPackage(Predicate<PackageReference> predicate)
    {
        var index = PackageReferences.FindIndex(predicate);

        if (index > -1)
        {
            return PackageReferences[index];
        }

        return null;
    }

    public ProjectFile UpdatePackageReferences(PackageUpgradeVersionDictionary packages)
    {
        ImmutableArray<PackageReference>.Builder? builder = null;

        var index = 0;
        foreach (var pkgRef in PackageReferences)
        {
            if (packages.TryGetValue(pkgRef.Name, out var version))
            {
                if (builder is null)
                {
                    builder = ImmutableArray.CreateBuilder<PackageReference>(
                        PackageReferences.Length
                    );
                    // copy the non updated packages as-is
                    if (index > 0)
                    {
                        builder.AddRange(PackageReferences, index);
                    }
                }

                builder.Add(pkgRef with { Version = version });
            }
            else
            {
                builder?.Add(pkgRef);
            }
            ++index;
        }

        if (builder is not null)
        {
            Debug.Assert(builder.Count == PackageCount, "Updated PackageReference counts differ.");

            return this with
            {
                NeedsUpdate = true,
                PackageReferences = builder.MoveToImmutable(),
            };
        }

        return this;
    }

    public (int index, PackageReference? reference) FindByNameWithIndex(string name) =>
        FindIndexByName(name) is int index && index > -1
            ? (index, PackageReferences[index])
            : (-1, null);

    public int FindIndexByName(string name) => FindIndex(pkg => pkg.HasName(name));

    public int FindIndex(Predicate<PackageReference> predicate) =>
        PackageReferences.FindIndex(predicate);

    public void Save(IFileSystem fileSystem)
    {
        if (FilePath is null)
        {
            return;
        }

        fileSystem.File.WriteAllText(FilePath, ProjectFileToXml());
    }

    public string ProjectFileToXml()
    {
        if (NeedsUpdate)
        {
            Xml = GetUpdatedXm();
            NeedsUpdate = false;
        }

        return Xml.ToString(SaveOptions.DisableFormatting);
    }

    private XDocument GetUpdatedXm()
    {
        var newDocument = new XDocument(Xml);

        var references = newDocument.GetPackageReferenceElements().ToImmutableArray();

        var refs = PackageReferences;

        foreach (var element in references)
        {
            var foundRef = refs.Find(
                item => item.HasName((string?)element.Attribute("Include") ?? "")
            );

            Debug.Assert(foundRef is not null, "PackageReferences should all be found.");

            var versionString = (string?)element.Attribute("Version");

            if (
                VersionRange.TryParse(versionString ?? "", out var versionRange)
                && !foundRef.Version.Equals(versionRange)
            )
            {
                element.SetAttributeValue("Version", foundRef.GetVersionString());
            }
        }

        return newDocument;
    }
}
