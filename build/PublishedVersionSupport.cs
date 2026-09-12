// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the generated "docs vs published" banner under the <c>llms.txt</c>
/// H1 — family design note <c>docs-vs-published-gate-2026-09-12.md</c> §1.
/// Pure logic (URL construction, registry-JSON parsing, cache freshness,
/// line rendering, marker-block find/replace): no network, no filesystem —
/// <c>Build.cs</c> supplies the HTTP response body and the cache file
/// contents. Mirrors how <see cref="PublicationVerificationSupport"/> and
/// <see cref="TranslationCheckSupport"/> separate pure logic from the
/// process/network calls that use them.
/// </summary>
internal static class PublishedVersionSupport
{
    /// <summary>
    /// The coordinate this port's banner tracks — every <c>NarrativeTrace.*</c>
    /// package ships on the same version, so one representative package
    /// stands in for "the port's published version" the same way the family
    /// design note's <c>contract.yaml</c> example names a single entry point.
    /// </summary>
    public const string TrackedPackageId = "NarrativeTrace.Core";

    /// <summary>How long a successful lookup may be reused before it counts as stale.</summary>
    public static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(1);

    public const string BlockStart = "<!-- published-version -->";
    public const string BlockEnd = "<!-- /published-version -->";

    /// <summary>
    /// The NuGet v3-flatcontainer "index" resource for a package — the same
    /// resource family <see cref="PublicationVerificationSupport.NupkgUrl"/>
    /// and <see cref="PublicationVerificationSupport.NuspecUrl"/> use, one
    /// level up: the full list of published versions rather than one
    /// version's artifacts.
    /// </summary>
    public static string FlatContainerIndexUrl(string baseUrl, string packageId) =>
        $"{baseUrl}/{packageId.ToLowerInvariant()}/index.json";

    /// <summary>
    /// Parses a flat-container index response (<c>{"versions": ["0.1.0", "0.1.1", ...]}</c>)
    /// and returns the highest non-prerelease version, or <see langword="null"/>
    /// when the body is empty, malformed, or carries no stable version at all
    /// — the same "no answer" outcome a failed HTTP call produces, so the
    /// caller never has to tell "the registry said nothing usable" apart
    /// from "the registry could not be reached".
    /// </summary>
    public static string? ParseLatestVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("versions", out var versions)
                || versions.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return versions.EnumerateArray()
                .Select(v => v.GetString())
                .Where(v => !string.IsNullOrWhiteSpace(v) && !v!.Contains('-', StringComparison.Ordinal))
                .OrderBy(v => ParseSortKey(v!))
                .LastOrDefault();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Dotted numeric versions compare component-by-component, missing trailing components read as zero.</summary>
    private static (int, int, int, int) ParseSortKey(string version)
    {
        var parts = version.Split('.');
        int Part(int index) =>
            index < parts.Length && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : 0;
        return (Part(0), Part(1), Part(2), Part(3));
    }

    /// <summary>One successful lookup, cached next to the build output (git-ignored).</summary>
    public sealed record CacheEntry(string Version, DateTimeOffset FetchedAtUtc);

    /// <summary>Deserializes a cache file's content; <see langword="null"/> for missing/corrupt content — never throws.</summary>
    public static CacheEntry? ParseCache(string? fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CacheEntry>(fileContent);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeCache(CacheEntry entry) => JsonSerializer.Serialize(entry);

    public static bool IsFresh(CacheEntry entry, DateTimeOffset now) =>
        now - entry.FetchedAtUtc <= CacheMaxAge;

    /// <summary>
    /// The visible banner line — exactly the three forms the design note
    /// specifies. <paramref name="publishedVersion"/> is <see langword="null"/>
    /// for the offline case: no cache hit and no network, never a guess and
    /// never a stale cached value presented as current.
    /// </summary>
    public static string BuildLine(string repoVersion, string? publishedVersion) =>
        publishedVersion switch
        {
            null => $"*(These docs describe {repoVersion}; published: unknown offline.)*",
            var p when p == repoVersion => $"*(Docs and published both at {repoVersion}.)*",
            var p => $"*(These docs describe {repoVersion}; published is {p}.)*",
        };

    private static readonly Regex EqualLinePattern = new(
        @"^\*\(Docs and published both at (?<repo>[^;)]+)\.\)\*$", RegexOptions.CultureInvariant);

    private static readonly Regex DifferLinePattern = new(
        @"^\*\(These docs describe (?<repo>[^;]+); published is (?<published>[^;)]+)\.\)\*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex OfflineLinePattern = new(
        @"^\*\(These docs describe (?<repo>[^;]+); published: unknown offline\.\)\*$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The exact inverse of <see cref="BuildLine"/>: recognizes any of the three canonical forms
    /// and extracts the version(s) each carries. Returns <see langword="false"/> for anything
    /// else — a hand-edited line, an old wording, garbage — so a caller can tell "wrong content"
    /// (this method returns values, caller compares them) from "not a banner line at all".
    /// </summary>
    public static bool TryParseLine(string line, out string describedVersion, out string? publishedVersion)
    {
        var equal = EqualLinePattern.Match(line);
        if (equal.Success)
        {
            describedVersion = equal.Groups["repo"].Value;
            publishedVersion = describedVersion;
            return true;
        }

        var differ = DifferLinePattern.Match(line);
        if (differ.Success)
        {
            describedVersion = differ.Groups["repo"].Value;
            publishedVersion = differ.Groups["published"].Value;
            return true;
        }

        var offline = OfflineLinePattern.Match(line);
        if (offline.Success)
        {
            describedVersion = offline.Groups["repo"].Value;
            publishedVersion = null;
            return true;
        }

        describedVersion = "";
        publishedVersion = null;
        return false;
    }

    /// <summary>
    /// The comment line disclosing where the published-side figure came
    /// from: a fresh registry hit, a cache hit (with its age, so a cached
    /// value is never presented as silently fresh), or no data at all.
    /// </summary>
    public static string BuildProvenanceComment(DateTimeOffset checkedAtUtc, TimeSpan? cacheAge) =>
        cacheAge switch
        {
            null => $"<!-- published-version checked {Iso(checkedAtUtc)} (live) -->",
            var age => $"<!-- published-version checked {Iso(checkedAtUtc)} "
                + $"(cached, {FormatAge(age.Value)} old) -->",
        };

    private static string Iso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string FormatAge(TimeSpan age) =>
        age.TotalHours >= 1
            ? $"{age.TotalHours:F1}h"
            : $"{Math.Max(1, (int)age.TotalMinutes)}m";

    /// <summary>
    /// The full marker block, ready to insert or compare: the provenance
    /// comment trails the banner on the SAME line (not a line of its own) —
    /// the shape the other runtimes use, so a diff of this file always shows
    /// one changed line, and <see cref="ExtractLine"/> strips it back off
    /// before any equality check, so the clock never flakes a comparison.
    /// </summary>
    public static string BuildBlock(string line, string provenanceComment) =>
        $"{BlockStart}\n{line} {provenanceComment}\n{BlockEnd}";

    private static readonly Regex ExistingBlockPattern = new(
        Regex.Escape(BlockStart) + @"\n(?<body>.*?)\n" + Regex.Escape(BlockEnd),
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>The block's body (line + comment, no markers) as it stands in a file today, or <see langword="null"/> if absent.</summary>
    public static string? ExtractBlockBody(string fileText)
    {
        var match = ExistingBlockPattern.Match(fileText);
        return match.Success ? match.Groups["body"].Value : null;
    }

    /// <summary>
    /// The visible banner, with any trailing provenance comment (
    /// <c>&lt;!-- published-version checked ... --&gt;</c>) stripped off —
    /// stripped before it ever reaches an equality check, so a stamp's
    /// timestamp or cache age never flakes a comparison.
    /// </summary>
    public static string? ExtractLine(string fileText)
    {
        var body = ExtractBlockBody(fileText);
        if (body is null)
        {
            return null;
        }

        var commentStart = body.IndexOf(" <!--", StringComparison.Ordinal);
        return commentStart < 0 ? body : body[..commentStart];
    }

    private static readonly Regex H1Pattern = new(@"^#[^\n]*\n", RegexOptions.CultureInvariant);

    /// <summary>
    /// Inserts or replaces the marker block immediately under the file's
    /// first H1 line. Replaces an existing block in place (byte-identical
    /// elsewhere); inserts a new one (with a blank line on each side) right
    /// after the H1 when none exists yet.
    /// </summary>
    public static string UpsertBlock(string fileText, string line, string provenanceComment)
    {
        var block = BuildBlock(line, provenanceComment);
        if (ExistingBlockPattern.IsMatch(fileText))
        {
            return ExistingBlockPattern.Replace(fileText, _ => block, 1);
        }

        var h1 = H1Pattern.Match(fileText);
        if (!h1.Success)
        {
            throw new InvalidOperationException("published-version banner: file has no H1 to insert the block under");
        }

        var insertAt = h1.Index + h1.Length;
        return fileText[..insertAt] + "\n" + block + "\n" + fileText[insertAt..];
    }
}
