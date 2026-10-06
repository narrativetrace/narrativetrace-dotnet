// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Cli.Init;

/// <summary>
/// Finds the skills carrier <c>narrativetrace init</c> installs from — locally, always.
/// </summary>
/// <remarks>
/// <para>INTENT: this launcher makes NO network call of its own (D4 as ruled). The carrier is
/// embedded in the CLI's own assembly, so <c>dotnet tool run dotnet-narrativetrace init</c> works
/// from the one package a consumer already fetched; <c>--from</c> overrides it with a directory, a
/// <c>.nupkg</c>, or an <c>Id/Version</c> coordinate already unpacked under the local NuGet cache.</para>
/// <para>The .NET analogue of Java's classloader lookup is the assembly's own manifest resources
/// (Phase 3 D1): the pages are read through <see cref="Assembly.GetManifestResourceStream(string)"/>,
/// never a path, so where the tool store unpacked the package cannot matter.</para>
/// <para><b>@sideEffects</b> Reads embedded resources, the filesystem, and the NuGet-cache
/// environment. Never writes, never resolves anything remotely.</para>
/// </remarks>
public static class CarrierLocator
{
    /// <summary>
    /// The embedded logical-name prefix the CLI packs the carrier under (see the
    /// <c>EmbeddedResource</c> item in <c>NarrativeTrace.Cli.csproj</c>), matching
    /// <c>Carrier.CarrierRoot</c> in the tooling library.
    /// </summary>
    private const string EmbeddedRoot = "NarrativeTrace.Skills/";

    /// <summary>The carrier content root inside a <c>.nupkg</c> or its unpacked NuGet-cache directory.</summary>
    private const string PackageCarrierRoot = "skills/";

    private const string CatalogueFile = "catalogue.json";

    /// <summary>
    /// The carrier a run should install from: the one bundled in this CLI when <paramref name="from"/>
    /// is null or blank, otherwise whatever it names locally.
    /// </summary>
    /// <exception cref="ArgumentException">When there is no carrier to open, naming what was looked for.</exception>
    public static Carrier Open(string? from) => Open(from, DefaultNuGetPackagesRoot());

    internal static Carrier Open(string? from, string nuGetPackagesRoot) =>
        string.IsNullOrWhiteSpace(from) ? Bundled() : Named(from.Trim(), nuGetPackagesRoot);

    /// <summary>The carrier that shipped inside this launcher, stamped with the CLI's own version.</summary>
    private static Carrier Bundled()
    {
        var assembly = typeof(CarrierLocator).Assembly;
        if (assembly.GetManifestResourceInfo(EmbeddedRoot + CatalogueFile) is null)
        {
            throw new ArgumentException(
                "this build of narrativetrace carries no skills — name one with --from");
        }

        return Carrier.FromSource(BundledVersion(assembly), name => ReadResource(assembly, EmbeddedRoot + name));
    }

    /// <summary>A directory, a <c>.nupkg</c>, or an <c>Id/Version</c> coordinate — a path tried first.</summary>
    private static Carrier Named(string from, string nuGetPackagesRoot)
    {
        if (Directory.Exists(from))
        {
            return Carrier.Open(from);
        }

        if (File.Exists(from))
        {
            return FromArchive(from);
        }

        if (LooksLikeCoordinate(from))
        {
            return FromNuGetCache(from, nuGetPackagesRoot);
        }

        throw new ArgumentException(
            "no carrier at " + from + " — --from takes a directory, a .nupkg, or an Id/Version"
            + " coordinate already in the local NuGet cache");
    }

    /// <summary>Reads the carrier out of a <c>.nupkg</c> (a zip), stamped with the package's own version.</summary>
    private static Carrier FromArchive(string archive)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            var version = VersionFromNuspec(zip) ?? "unknown";
            return Carrier.FromSource(version, name => ReadZipEntry(zip, PackageCarrierRoot + name));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException)
        {
            // A file that is not a readable zip, or whose nuspec is not valid XML, is refused by name
            // — never leaked as a raw ZipException/XmlException, so `init` still exits 1 with a message.
            throw new ArgumentException("cannot read the carrier at " + archive + ": " + exception.Message, exception);
        }
    }

    /// <summary>The carrier already unpacked at <c>&lt;cache&gt;/&lt;id&gt;/&lt;version&gt;/skills/</c>.</summary>
    private static Carrier FromNuGetCache(string coordinate, string nuGetPackagesRoot)
    {
        var slash = coordinate.IndexOf('/', StringComparison.Ordinal);
        var id = coordinate[..slash];
        var version = coordinate[(slash + 1)..];
        var root = Path.Combine(nuGetPackagesRoot, id.ToLowerInvariant(), version, PackageCarrierRoot);
        if (!Directory.Exists(root))
        {
            throw new ArgumentException(
                "no carrier for " + coordinate + " in the local NuGet cache (looked under " + root
                + ") — restore the package, or point --from at a directory or a .nupkg");
        }

        return Carrier.FromSource(version, name => ReadFileOrNull(Path.Combine(root, name)));
    }

    /// <summary>
    /// An <c>Id/Version</c> coordinate: two non-empty halves, the version half beginning with a digit
    /// (every NuGet version does). The digit test keeps a two-segment path that reached here — neither
    /// a directory nor a file — from being mistaken for a coordinate, so it earns the plain "no carrier
    /// at" refusal rather than a NuGet-cache one.
    /// </summary>
    private static bool LooksLikeCoordinate(string from)
    {
        var parts = from.Split('/');
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && char.IsDigit(parts[1][0]);
    }

    private static string BundledVersion(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return "unknown";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }

    private static string? VersionFromNuspec(ZipArchive zip)
    {
        var nuspec = zip.Entries.FirstOrDefault(
            entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspec is null)
        {
            return null;
        }

        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        return document.Descendants().FirstOrDefault(
            element => element.Name.LocalName == "version")?.Value;
    }

    private static string? ReadResource(Assembly assembly, string logicalName)
    {
        using var stream = assembly.GetManifestResourceStream(logicalName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadZipEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadFileOrNull(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;

    private static string DefaultNuGetPackagesRoot()
    {
        var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        return string.IsNullOrEmpty(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
            : configured;
    }
}
