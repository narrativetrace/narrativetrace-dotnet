// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace NarrativeTrace.Build;

/// <summary>The four claim shapes <c>documentation/contract.yaml</c> can make (docs-vs-published-gate §2).</summary>
internal enum ContractKind
{
    EntryPoint,
    ReflectableDefault,
    ProbedDefault,
    ConfigShape,
}

/// <summary>
/// One <c>documentation/contract.yaml</c> entry. <see cref="Expect"/> is the single observed
/// string a probe must produce for the claim to hold — the YAML spells it
/// <c>documented_default</c> (reflectable/probed) or <c>expected_effect</c> (config-shape, the
/// design note's own field name); both land here as one field, mirroring
/// <c>contract-probe</c>'s own <c>ContractEntry</c> (the two cannot share code — see
/// <see cref="ContractLintSupport"/>'s remarks). <see cref="Coordinate"/>/<see cref="Registry"/>
/// apply to <see cref="ContractKind.EntryPoint"/> only.
/// </summary>
internal sealed record ContractEntry(
    string Id, ContractKind Kind, string Page, string Claim, string Since, string Expect,
    string Probe, string? Coordinate = null, string? Registry = null);

internal sealed record ContractDocument(string VersionSource, IReadOnlyList<ContractEntry> Entries);

/// <summary>One page-anchor pointer, split for validation.</summary>
internal sealed record ContractPageRef(string RelativePath, string Anchor);

/// <summary>
/// Backs the <c>ContractLint</c> Nuke target (docs-vs-published-gate §2/§5.1) — everything about
/// <c>documentation/contract.yaml</c> a per-commit gate can check WITHOUT the network: the schema
/// parses, every <c>since</c> is a real version string, no two entries make the same claim, every
/// entry's <c>probe</c> file exists, every <c>page#anchor</c> pointer resolves to a heading that
/// actually exists, and every <c>*(since X, unreleased)*</c> marker in the English docs (part (a))
/// is backed by at least one contract entry at that version — the mechanical link between (a) and
/// (c) the gate's design note calls for. This class deliberately never runs a probe, touches the
/// registry, or decides holds/fails/not-applicable-before-since for a real probe result — that
/// decision lives in <see cref="ContractDecisionSupport"/>, exercised nightly by
/// <c>contract-probe/</c> and, standalone, by four fixture tests pinning the docs-vs-published-gate
/// design note's four historical instances, so the decision logic is provable without a release.
/// </summary>
internal static class ContractLintSupport
{
    private static readonly Regex SincePattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant);
    private static readonly Regex HeadingLine = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex HeadingSincePattern = new(@"^#{1,6}\s.*\(since ", RegexOptions.CultureInvariant);

    /// <summary>
    /// The GitHub-flavoured-Markdown heading slug: lowercase, strip anything but
    /// <c>[a-z0-9 _-]</c>, then turn spaces into hyphens. Deliberately does not collapse repeated
    /// separators — a heading with an em dash or a slash between two words legitimately slugs to a
    /// double hyphen, matching the algorithm GitHub's own renderer uses, so an anchor validated
    /// here is also the one a reader's click actually lands on.
    /// </summary>
    public static string Slugify(string heading)
    {
        var lower = heading.ToLowerInvariant();
        var kept = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_')
                kept.Append(ch);
        }

        return kept.ToString().Replace(' ', '-');
    }

    /// <summary>
    /// Every anchor slug the given Markdown file's headings produce, in document order, with
    /// GitHub's own disambiguation for a repeated slug (<c>foo</c>, <c>foo-1</c>, <c>foo-2</c>, …).
    /// </summary>
    public static IReadOnlySet<string> HeadingAnchors(string markdownFile)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(markdownFile))
        {
            var match = HeadingLine.Match(line);
            if (!match.Success)
                continue;

            var basis = Slugify(match.Groups[2].Value);
            var count = seen.GetValueOrDefault(basis);
            seen[basis] = count + 1;
            anchors.Add(count == 0 ? basis : $"{basis}-{count}");
        }

        return anchors;
    }

    /// <summary>
    /// Every heading line, across every Markdown file and <c>llms.txt</c> anywhere under
    /// <c>documentation/</c> (every language — translated mirrors live under
    /// <c>documentation/&lt;lang&gt;/</c> and are in scope too) plus the root README and its own
    /// language mirrors, that still carries an inline <c>(since ...)</c> marker. A heading's
    /// GitHub-rendered anchor slug is exactly the text <see cref="HeadingAnchors"/> computes from
    /// it; a since-marker's own tag rewrite (the release publish script) can later shorten or drop
    /// the parenthetical, and that mutates the slug — any reader link into that anchor breaks the
    /// instant a release settles. Keeping the marker in the section's body, never the heading
    /// itself, is the only shape immune to that (owner ruling 2026-09-16) — ported from the TS
    /// repo's <c>tools/contract-lint.ts</c> <c>headingsWithSinceMarker</c> / Java's
    /// <c>ContractLintSupport.headingsWithSinceMarker</c> / Python's
    /// <c>contract_lint.headings_with_since_marker</c> (read-only references, not shared code).
    /// One entry per hit, <c>"&lt;relative path&gt;:&lt;line&gt;: &lt;reason&gt;"</c>, sorted;
    /// empty when <paramref name="repoRoot"/> has no such heading.
    /// </summary>
    public static IReadOnlyList<string> HeadingsWithSinceMarker(string repoRoot)
    {
        var hits = new List<string>();
        foreach (var file in SinceMarkerHeadingScanScope(repoRoot))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (HeadingSincePattern.IsMatch(line))
                {
                    var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                    hits.Add(
                        $"{relative}:{lineNumber}: since-markers belong in the body: heading "
                            + "anchors must survive the tag rewrite");
                }
            }
        }

        hits.Sort(StringComparer.Ordinal);
        return hits;
    }

    /// <summary>
    /// Every Markdown file and <c>llms.txt</c> anywhere under <c>documentation/</c> (every
    /// language — translated mirrors live under <c>documentation/&lt;lang&gt;/</c> and are in
    /// scope too), plus the root README and its own language mirrors: <c>README.md</c> itself, and
    /// any other root-level <c>*.md</c> file whose line-1 translation header
    /// (<see cref="TranslationCheckSupport.ParseHeader"/>) names <c>README.md</c> as its source —
    /// the same header-driven "is this a translation of X" test <see cref="TranslationCheckSupport"/>
    /// already uses, so this never keeps a second, independent list of root README mirror filenames.
    /// </summary>
    private static IEnumerable<string> SinceMarkerHeadingScanScope(string repoRoot)
    {
        var documentation = Path.Combine(repoRoot, "documentation");
        var docs = Directory.Exists(documentation)
            ? Directory.EnumerateFiles(documentation, "*.*", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(file).Equals("llms.txt", StringComparison.OrdinalIgnoreCase))
            : Enumerable.Empty<string>();
        var readmeMirrors = Directory.Exists(repoRoot)
            ? Directory.EnumerateFiles(repoRoot, "*.md", SearchOption.TopDirectoryOnly)
                .Where(file => Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase)
                    || IsReadmeMirror(file))
            : Enumerable.Empty<string>();
        return docs.Concat(readmeMirrors);
    }

    private static bool IsReadmeMirror(string file)
    {
        using var reader = new StreamReader(file, Encoding.UTF8);
        var firstLine = reader.ReadLine() ?? string.Empty;
        return TranslationCheckSupport.ParseHeader(firstLine)?.SourcePath == "README.md";
    }

    /// <summary>
    /// Splits <c>"documentation/foo.md#some-anchor"</c> into path and anchor; throws on a pointer
    /// with no <c>#anchor</c> half — a contract entry is always about one specific claim, never a
    /// whole page.
    /// </summary>
    public static ContractPageRef ParsePageRef(string page)
    {
        var hashIndex = page.IndexOf('#', StringComparison.Ordinal);
        if (hashIndex <= 0 || hashIndex == page.Length - 1)
            throw new ArgumentException($"\"{page}\" — a contract entry's page must be \"<path>#<anchor>\"");

        return new ContractPageRef(page[..hashIndex], page[(hashIndex + 1)..]);
    }

    /// <summary>
    /// Parses <c>documentation/contract.yaml</c>. Throws (never returns a partial document) on
    /// anything the schema does not allow — a malformed contract must fail loud.
    /// </summary>
    public static ContractDocument Parse(string file)
    {
        var root = ReadRoot(file);
        var versionSource = StringField(root, "version_source")
            ?? throw new ArgumentException($"{file}: missing \"version_source\"");
        if (!root.TryGetValue("entries", out var rawEntries) || rawEntries is not List<object> entryList)
            throw new ArgumentException($"{file}: missing \"entries\" list");

        var entries = entryList.Select(raw => ParseEntry(file, (Dictionary<object, object>)raw)).ToList();
        return new ContractDocument(versionSource, entries);
    }

    private static Dictionary<object, object> ReadRoot(string file)
    {
        using var reader = new StreamReader(file);
        var root = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(reader);
        return root ?? throw new ArgumentException($"{file}: empty document");
    }

    private static ContractEntry ParseEntry(string file, Dictionary<object, object> raw)
    {
        var id = RequiredField(file, raw, "id");
        var kind = ParseKind(file, id, RequiredField(file, raw, "kind"));
        var since = RequiredField(file, raw, "since");
        var expect = StringField(raw, "documented_default") ?? StringField(raw, "expected_effect")
            ?? throw new ArgumentException($"{file}: entry \"{id}\" needs \"documented_default\" or \"expected_effect\"");
        if (kind == ContractKind.EntryPoint)
            RequiredField(file, raw, "coordinate");

        return new ContractEntry(
            id, kind, RequiredField(file, raw, "page"), RequiredField(file, raw, "claim"), since, expect,
            RequiredField(file, raw, "probe"), StringField(raw, "coordinate"), StringField(raw, "registry"));
    }

    private static ContractKind ParseKind(string file, string id, string raw) => raw switch
    {
        "entry-point" => ContractKind.EntryPoint,
        "reflectable-default" => ContractKind.ReflectableDefault,
        "probed-default" => ContractKind.ProbedDefault,
        "config-shape" => ContractKind.ConfigShape,
        _ => throw new ArgumentException(
            $"{file}: entry \"{id}\": unknown kind \"{raw}\" — must be one of entry-point, "
                + "reflectable-default, probed-default, config-shape"),
    };

    private static string? StringField(Dictionary<object, object> raw, string name) =>
        raw.TryGetValue(name, out var value) && value is string { Length: > 0 } s ? s : null;

    private static string RequiredField(string file, Dictionary<object, object> raw, string name) =>
        StringField(raw, name) ?? throw new ArgumentException($"{file}: entry missing \"{name}\"");

    /// <summary>
    /// Every problem the gate reports, empty when the contract is internally consistent.
    /// <paramref name="repoRoot"/> resolves <c>page</c> and <c>probe</c> pointers;
    /// <paramref name="unreleasedMarkerVersions"/> is the distinct set of versions cited by
    /// <c>*(since X.Y.Z, unreleased)*</c> across the English docs (<see cref="UnreleasedMarkerSupport"/>
    /// already walks that file set for part (a) — passed in rather than re-walked here so the two
    /// checks can never quietly disagree on which files count as "the English docs").
    /// </summary>
    public static IReadOnlyList<string> Lint(
        string repoRoot, ContractDocument document, IReadOnlySet<string> unreleasedMarkerVersions)
    {
        var problems = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenClaims = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in document.Entries)
            LintEntry(repoRoot, entry, seenIds, seenClaims, problems);

        LintCoverage(document, unreleasedMarkerVersions, problems);
        problems.AddRange(HeadingsWithSinceMarker(repoRoot));
        problems.Sort(StringComparer.Ordinal);
        return problems;
    }

    private static void LintEntry(
        string repoRoot, ContractEntry entry, HashSet<string> seenIds,
        Dictionary<string, string> seenClaims, List<string> problems)
    {
        if (!seenIds.Add(entry.Id))
            problems.Add($"duplicate entry id \"{entry.Id}\"");
        if (seenClaims.TryGetValue(entry.Claim, out var firstId))
            problems.Add($"\"{entry.Id}\" and \"{firstId}\" make the same claim: \"{entry.Claim}\"");
        else
            seenClaims[entry.Claim] = entry.Id;

        if (!SincePattern.IsMatch(entry.Since))
            problems.Add($"\"{entry.Id}\": since \"{entry.Since}\" is not a real version string (x.y.z)");
        if (entry.Kind == ContractKind.EntryPoint && string.IsNullOrWhiteSpace(entry.Coordinate))
            problems.Add($"\"{entry.Id}\": entry-point requires \"coordinate\"");
        if (!File.Exists(Path.Combine(repoRoot, entry.Probe)))
            problems.Add($"\"{entry.Id}\": probe \"{entry.Probe}\" does not exist");

        LintPageRef(repoRoot, entry, problems);
    }

    private static void LintPageRef(string repoRoot, ContractEntry entry, List<string> problems)
    {
        ContractPageRef pageRef;
        try
        {
            pageRef = ParsePageRef(entry.Page);
        }
        catch (ArgumentException e)
        {
            problems.Add($"\"{entry.Id}\": {e.Message}");
            return;
        }

        var pageFile = Path.Combine(repoRoot, pageRef.RelativePath);
        if (!File.Exists(pageFile))
            problems.Add($"\"{entry.Id}\": page \"{pageRef.RelativePath}\" does not exist");
        else if (!HeadingAnchors(pageFile).Contains(pageRef.Anchor))
            problems.Add($"\"{entry.Id}\": anchor \"#{pageRef.Anchor}\" not found in {pageRef.RelativePath}");
    }

    private static void LintCoverage(
        ContractDocument document, IReadOnlySet<string> unreleasedMarkerVersions, List<string> problems)
    {
        var coveredVersions = new HashSet<string>(document.Entries.Select(e => e.Since), StringComparer.Ordinal);
        foreach (var version in unreleasedMarkerVersions.OrderBy(v => v, StringComparer.Ordinal))
        {
            if (!coveredVersions.Contains(version))
                problems.Add(
                    $"documentation carries \"*(since {version}, unreleased)*\" but no contract.yaml entry "
                        + $"has since: \"{version}\" — add one in the same commit as the feature "
                        + "(docs-vs-published-gate §5.1 ruling 3)");
        }
    }
}

/// <summary>
/// holds: the probe observed exactly what the docs claim. fails: it observed something else.
/// not-applicable-before-since: the claim's <c>since</c> is later than the version actually
/// installed — ruling 1 (docs-vs-published-gate §5.1): exempt only while later than the INSTALLED
/// published version, never the repo's own.
/// </summary>
internal enum ContractVerdict
{
    Holds,
    Fails,
    NotApplicableBeforeSince,
}

internal sealed record ContractOutcome(ContractEntry Entry, ContractVerdict Verdict, string Message);

/// <summary>
/// The decision <c>ContractCheck</c> (nightly, against a real probe) and <c>ContractLint</c>'s own
/// fixture tests (offline, against a fake probe result standing in for one of the four historical
/// instances, docs-vs-published-gate §3) both go through — so "would the gate have fired" is
/// exactly the same code path whether the probe result came from nuget.org or from a test fixture.
/// Deliberately duplicated (not shared) into <c>contract-probe/</c>'s own <c>Versions.cs</c>: that
/// project consumes only nuget.org packages and can never reference this build project, which is
/// not published — mirroring the java runtime's own <c>Versions</c>/<c>ContractDecisionSupport</c> split.
/// </summary>
internal static class ContractDecisionSupport
{
    /// <summary>True while <paramref name="since"/> is NOT strictly later than <paramref name="installedVersion"/>.</summary>
    public static bool IsApplicable(string since, string installedVersion)
    {
        var a = VersionParts(since);
        var b = VersionParts(installedVersion);
        var length = Math.Max(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y)
                return x < y;
        }

        return true;
    }

    private static int[] VersionParts(string version) =>
        version.Split('.').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();

    /// <summary>
    /// <paramref name="observed"/> is <see langword="null"/> when the probe itself could not even
    /// run (registry unreachable, artifact missing) — treated as a failure with its own explaining
    /// message, never silently skipped; only a <c>since</c> later than
    /// <paramref name="installedVersion"/> is ever skipped.
    /// </summary>
    public static ContractOutcome Decide(ContractEntry entry, string installedVersion, string? observed)
    {
        if (!IsApplicable(entry.Since, installedVersion))
        {
            return new ContractOutcome(
                entry, ContractVerdict.NotApplicableBeforeSince,
                $"\"{entry.Id}\": since {entry.Since} is later than installed {installedVersion} — skipped");
        }

        if (observed == entry.Expect)
            return new ContractOutcome(entry, ContractVerdict.Holds, $"\"{entry.Id}\": holds");

        var coordinate = entry.Coordinate ?? entry.Id;
        return new ContractOutcome(
            entry, ContractVerdict.Fails,
            $"documentation/contract.yaml: {entry.Id} documented default \"{entry.Expect}\" "
                + $"(since {entry.Since}) but {coordinate} {installedVersion} (published) reads "
                + $"\"{observed ?? "<no answer>"}\"");
    }
}
