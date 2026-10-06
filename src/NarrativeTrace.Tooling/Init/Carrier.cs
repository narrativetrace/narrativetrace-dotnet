// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.ObjectModel;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// An opened skills carrier: the catalogue, every flavour's rendered page, and the coordinate that
/// stamps whatever gets installed from it.
/// </summary>
/// <remarks>
/// <para>INTENT: the installer's read side. A carrier is opened once, validated whole, and then
/// behaves as a value — every page is in memory, so nothing downstream holds a file handle or can see
/// a carrier change under it mid-install.</para>
/// <para>A carrier whose catalogue is malformed, whose catalogue lists a page the carrier does not
/// have, or whose catalogue names a skill twice is REFUSED here with a message naming the entry.
/// Validation happens before any plan exists, so a broken carrier can never half-install.</para>
/// <para>This port reads a carrier from an <b>exploded directory</b>. Java also opens a jar and
/// resolves a Maven coordinate under <c>~/.m2</c>; the .NET analogues — reading the carrier from the
/// running tool's embedded resources, from the project's resolved <c>NarrativeTrace.Skills</c> NuGet
/// package, or from a <c>--from</c> archive — are the entry-point's source resolution (Phase 3
/// milestones 1 and 3), layered over this same value object. The coordinate this port stamps with is
/// always the skills package's, <c>NarrativeTrace.Skills/&lt;version&gt;</c> (design D6), so the Id is
/// fixed here rather than parsed from a file name as Java's is.</para>
/// </remarks>
public sealed class Carrier
{
    /// <summary>The Id half of every coordinate a carrier stamps with — the skills package (D6).</summary>
    internal const string Id = "NarrativeTrace.Skills";

    /// <summary>Where the carrier's resources live under an exploded root — the resource prefix.</summary>
    internal const string CarrierRoot = "NarrativeTrace.Skills/";

    internal const string CatalogueFile = "catalogue.json";

    /// <summary>Written into the carrier by the build that publishes it; absent from a source tree.</summary>
    private const string VersionFile = "narrativetrace-version.properties";

    /// <summary>The version part of a coordinate that names no version — honest, and visibly stale.</summary>
    private const string UnknownVersion = "unknown";

    private readonly IReadOnlyDictionary<string, string> _pages;

    private Carrier(string coordinate, SkillCatalogue catalogue, IReadOnlyDictionary<string, string> pages)
    {
        Coordinate = coordinate;
        Catalogue = catalogue;
        _pages = pages;
        if (!Invariant())
        {
            throw new ArgumentException("a carrier must be complete the moment it exists");
        }
    }

    /// <summary>The coordinate that stamps everything installed from this carrier.</summary>
    public string Coordinate { get; }

    /// <summary>The carrier's index, already validated.</summary>
    public SkillCatalogue Catalogue { get; }

    /// <summary>Every skill the carrier carries, in catalogue order.</summary>
    public IReadOnlyList<SkillEntry> Skills => Catalogue.Skills;

    /// <summary>
    /// Opens a carrier from an exploded directory (either the resource root or the carrier root
    /// itself).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When the path is null, does not exist, is not a directory, carries no catalogue, or carries a
    /// catalogue that does not describe what is there.
    /// </exception>
    public static Carrier Open(string source)
    {
        if (source is null)
        {
            throw new ArgumentException("a carrier path must be given");
        }

        if (!Directory.Exists(source) && !File.Exists(source))
        {
            throw new ArgumentException("no carrier at " + source);
        }

        if (!Directory.Exists(source))
        {
            throw new ArgumentException(
                "no carrier at " + source + " — this milestone reads an exploded directory carrier, "
                + "not an archive");
        }

        return OpenDirectory(source);
    }

    /// <summary>
    /// Opens a carrier whose pages come from an arbitrary source rather than a directory: the entry
    /// point's embedded resources, a <c>.nupkg</c>'s entries, or a NuGet-cache directory, all read
    /// through one reader keyed by carrier-relative path (<c>catalogue.json</c>,
    /// <c>agents/&lt;name&gt;/SKILL.md</c>, …). The coordinate is stamped
    /// <c>NarrativeTrace.Skills/&lt;version&gt;</c> (D6) — the caller supplies the version its source
    /// knows (the running CLI's informational version, a package's nuspec version, a cache coordinate).
    /// </summary>
    /// <param name="version">The version half of the coordinate everything installed is stamped with.</param>
    /// <param name="readCarrierFile">
    /// Reads one carrier-relative entry, returning its text or <see langword="null"/> when absent.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When the reader is null, the version does not name a coordinate, or the source is missing the
    /// catalogue or a page the catalogue lists.
    /// </exception>
    public static Carrier FromSource(string version, Func<string, string?> readCarrierFile)
    {
        if (readCarrierFile is null)
        {
            throw new ArgumentException("a carrier reader must be given");
        }

        return Read(CoordinateOf(version), readCarrierFile);
    }

    /// <summary>
    /// The rendered page for one skill in one flavour, exactly as the carrier carries it.
    /// </summary>
    /// <exception cref="ArgumentException">When the skill is not this carrier's.</exception>
    public string Body(SkillEntry skill, SkillFlavour flavour)
    {
        if (skill is null)
        {
            throw new ArgumentException("a skill must be given");
        }

        if (!_pages.TryGetValue(skill.PathFor(flavour), out var page))
        {
            throw new ArgumentException(
                "this carrier does not carry " + skill.Name + " (" + Coordinate + ")");
        }

        return page;
    }

    /// <summary>
    /// Every page the catalogue lists is present, and the coordinate names both parts. A carrier that
    /// fails this could half-install a project.
    /// </summary>
    internal bool Invariant()
    {
        if (Coordinate is null || Coordinate.Split('/').Length != 2)
        {
            return false;
        }

        return Catalogue.Skills.All(skill =>
            _pages.ContainsKey(skill.PathFor(SkillFlavour.Agents))
                && _pages.ContainsKey(skill.PathFor(SkillFlavour.Claude)));
    }

    private static Carrier OpenDirectory(string directory)
    {
        var root = File.Exists(System.IO.Path.Combine(directory, CatalogueFile))
            ? directory
            : System.IO.Path.Combine(directory, CarrierRoot);
        var version = ReadFile(System.IO.Path.Combine(directory, VersionFile)) is { } properties
            ? VersionIn(properties)
            : UnknownVersion;
        return FromSource(version, name => ReadFile(System.IO.Path.Combine(root, name)));
    }

    /// <summary>Validates the whole carrier, then freezes it: every listed page is read here or nowhere.</summary>
    private static Carrier Read(string coordinate, Func<string, string?> source)
    {
        var text = source(CatalogueFile)
            ?? throw new ArgumentException(
                "the carrier " + coordinate + " carries no " + CatalogueFile);
        var catalogue = ReadCatalogue(coordinate, text);
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var skill in catalogue.Skills)
        {
            foreach (var flavour in new[] { SkillFlavour.Agents, SkillFlavour.Claude })
            {
                var path = skill.PathFor(flavour);
                pages[path] = source(path) ?? throw Missing(coordinate, skill, path);
            }
        }

        return new Carrier(coordinate, catalogue, new ReadOnlyDictionary<string, string>(pages));
    }

    private static SkillCatalogue ReadCatalogue(string coordinate, string text)
    {
        try
        {
            return CatalogueReader.Read(text);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "the carrier " + coordinate + "'s " + CatalogueFile + " is unusable: " + exception.Message,
                exception);
        }
    }

    private static ArgumentException Missing(string coordinate, SkillEntry skill, string path) =>
        new("the carrier " + coordinate + " lists " + skill.Name + " at " + path
            + ", which it does not carry");

    private static string? ReadFile(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;

    /// <summary>
    /// The <c>version=</c> line of a carrier's own properties file, or <c>unknown</c> when it names
    /// none. Read line-wise: the file has exactly one key, written by the build.
    /// </summary>
    private static string VersionIn(string properties)
    {
        foreach (var raw in properties.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("version=", StringComparison.Ordinal))
            {
                var version = line["version=".Length..].Trim();
                if (version.Length > 0)
                {
                    return version;
                }
            }
        }

        return UnknownVersion;
    }

    /// <summary>
    /// The coordinate a carrier stamps with, validated: this string is written into every page the
    /// carrier installs and read back by the doctor, so a version carrying the coordinate separator or
    /// a path segment is refused before any page is written.
    /// </summary>
    private static string CoordinateOf(string version)
    {
        if (string.IsNullOrWhiteSpace(version)
            || version.Contains('/', StringComparison.Ordinal)
            || version.Contains('\\', StringComparison.Ordinal)
            || version == "..")
        {
            throw new ArgumentException(
                "a carrier is stamped with its own coordinate, and \"" + Id + "/" + version
                + "\" does not name one — a version must not contain a path segment");
        }

        return Id + "/" + version;
    }
}
