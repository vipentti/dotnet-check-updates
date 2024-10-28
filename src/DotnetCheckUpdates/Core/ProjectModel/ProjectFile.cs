// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Diagnostics;
using System.IO.Abstractions;
using System.Xml;
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

    public string? Sdk { get; init; }

    public string? EncodingName { get; init; }

    public bool HasUtf8ByteOrderMarker { get; init; }

    // TODO: This can be simplified once we drop support for older frameworks
#pragma warning disable IDE0301 // Simplify collection initialization
    public ImmutableArray<Import> Imports { get; init; } = ImmutableArray<Import>.Empty;
#pragma warning restore IDE0301 // Simplify collection initialization

    public bool IsDirectoryBuildProps =>
        FilePath.EndsWith(CliConstants.DirectoryBuildPropsFileName, StringComparison.Ordinal);

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
        FindIndexByName(name) is var index && index > -1
            ? (index, PackageReferences[index])
            : (-1, null);

    public int FindIndexByName(string name) => FindIndex(pkg => pkg.HasName(name));

    public int FindIndex(Predicate<PackageReference> predicate) =>
        PackageReferences.FindIndex(predicate);

    public void Save(IFileSystem fileSystem)
    {
        fileSystem.File.WriteAllText(FilePath, ProjectFileToXml());
    }

    public string ProjectFileToXml()
    {
        if (NeedsUpdate)
        {
            Xml = GetUpdatedXml();
            NeedsUpdate = false;
        }

        Encoding encoding;

        try
        {
            if (string.Equals(EncodingName, "utf-8", StringComparison.OrdinalIgnoreCase))
            {
                encoding = HasUtf8ByteOrderMarker
                    ? Encoding.UTF8
                    : ProjectFileParser.Utf8WithoutBom;
            }
            else
            {
                encoding = string.IsNullOrEmpty(Xml.Declaration?.Encoding)
                    ? ProjectFileParser.Utf8WithoutBom
                    : Encoding.GetEncoding(Xml.Declaration.Encoding);
            }
        }
        catch
        {
            // Default to utf8 without bom
            encoding = ProjectFileParser.Utf8WithoutBom;
        }

        using var ms = new MemoryStream();
        using (
            var xw = XmlWriter.Create(
                ms,
                new XmlWriterSettings()
                {
                    Encoding = encoding,
                    OmitXmlDeclaration = Xml.Declaration is null,
                }
            )
        )
        {
            Xml.Save(xw);
        }

        return encoding.GetString(ms.ToArray());
    }

    private XDocument GetUpdatedXml()
    {
        var newDocument = new XDocument(Xml);

        var references = newDocument.GetPackageReferenceElements().ToImmutableArray();

        foreach (var element in references)
        {
            var foundRef = PackageReferences.Find(item =>
                item.HasName((string?)element.Attribute("Include") ?? "")
            );

            // Skip filtered packages
            if (foundRef is null)
            {
                continue;
            }

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
