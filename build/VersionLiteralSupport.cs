// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// "No version talk anywhere" is about NarrativeTrace's OWN versions (owner ruling 2026-09-24,
/// narrowed by the same owner 2026-09-25). Repository documentation is assumed to describe the code
/// it is committed with, so no README, guide or <c>llms.txt</c> says which version it describes or
/// which one is published. The ONE version literal a public document may carry for THIS project is
/// an <b>install coordinate</b> — a snippet that cannot be pasted is not a quickstart — and that
/// one is machine-written: <see cref="Sync"/> substitutes the build's <c>VersionPrefix</c> into
/// every coordinate form, <see cref="Check"/> fails the build on a coordinate pinned anywhere else.
/// <c>SnippetSync</c> is the only writer, <c>SnippetCheck</c> the reader, exactly as for the
/// embedded code blocks <see cref="SnippetCheckSupport"/> governs.
/// </summary>
/// <remarks>
/// <para>Two rules, and deliberately no third. <see cref="Check"/> rejects, each naming file and line:</para>
/// <list type="bullet">
/// <item>a NarrativeTrace coordinate pinned to a version other than the build's;</item>
/// <item>a <c>*(since X)*</c> marker, a <c>published-version</c> banner or cache comment — the
/// machinery the ruling removed, so a reintroduction fails rather than rots.</item>
/// </list>
/// <para>
/// A version literal that is not one of OURS is not this lint's business, and the third rule that
/// used to make it one — "any other three-part literal", with a reviewed allowlist to buy each hit
/// back — was retired on 2026-09-25. A compatibility table's ".NET 10", an example service's own
/// <c>ServiceVersion</c>, an advisory note's before/after pair: every one of those is a fact about
/// somebody else's release rather than version talk about NarrativeTrace, so every real hit the
/// rule ever produced needed an exemption, and the allowlist holding them was a second copy of
/// other projects' release notes that went stale on each dependency bump.
/// </para>
/// <para>
/// A marker-shaped string inside a fenced block is still a marker: fenced blocks are where the
/// install snippets live, so exempting them would blind this gate on the one surface that matters
/// most; a page that needs to <i>discuss</i> the marker syntax writes a placeholder instead.
/// </para>
/// <para>
/// Scope is the public documents (<see cref="GovernedFiles"/>): the root README and its language
/// mirrors, every <c>*.md</c> and <c>llms.txt</c> under <c>documentation/</c> that survives
/// <c>.publishignore</c>. That scoping is KEPT for the two narrowed rules: the internal design
/// notes that directory also holds are stripped before publication, so a historical NarrativeTrace
/// coordinate one of them cites is engineering history in a page the public never sees, not a
/// public lie — the ruling's subject stays the public documents.
/// <c>documentation/contract.yaml</c> is outside the scan by construction: only <c>*.md</c> and
/// <c>llms.txt</c> are walked.
/// </para>
/// </remarks>
internal static class VersionLiteralSupport
{
    /// <summary><c>0.1.5</c>, <c>0.2.0-preview.1</c>, <c>4.2.17.Final</c> — two or more dotted
    /// numbers, optional qualifier.</summary>
    private const string Version = @"\d+(?:\.\d+)+(?:-[A-Za-z0-9.+]+)?";

    /// <summary><c>&lt;PackageReference Include="NarrativeTrace.Core" Version="0.1.5" /&gt;</c> —
    /// the version attribute of a NarrativeTrace package reference, Include then Version.</summary>
    private static readonly Regex PackageReferenceCoordinate = new(
        $@"(Include=""NarrativeTrace\.[A-Za-z0-9._-]+""\s+Version="")({Version})("")",
        RegexOptions.CultureInvariant);

    /// <summary><c>dotnet add package NarrativeTrace.Proxy --version 0.1.5</c>.</summary>
    private static readonly Regex DotnetAddCoordinate = new(
        $@"(dotnet add package NarrativeTrace\.[A-Za-z0-9._-]+ --version )({Version})",
        RegexOptions.CultureInvariant);

    /// <summary>Both hard-wrap shapes survive this: the wrap can only land after <c>since</c> or
    /// after the version's comma, and <c>*(since</c> is intact either way.</summary>
    private static readonly Regex SinceMarker = new(@"\*\(since\b", RegexOptions.CultureInvariant);

    /// <summary>The <c>&lt;!-- published-version --&gt;</c> block delimiters and the
    /// <c>&lt;!-- published-version checked … --&gt;</c> cache comment alike.</summary>
    private static readonly Regex BannerMarker = new(
        @"<!--\s*/?\s*published-version", RegexOptions.CultureInvariant);

    // -------------------------------------------------------------------------------------------
    // Sync — the only writer
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Rewrites every NarrativeTrace install coordinate in every governed document to
    /// <paramref name="version"/>, translated mirrors included (a coordinate is language-neutral).
    /// A mirror whose English source this touched has its line-1 blob hash restamped — the hash
    /// only — so a version bump leaves the translation gate green in the same run. Returns one line
    /// per file actually rewritten, sorted; empty when nothing needed it.
    /// </summary>
    public static IReadOnlyList<string> Sync(string repoRoot, string version)
    {
        var root = Path.GetFullPath(repoRoot);
        var changed = new List<string>();
        var rewrittenSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in GovernedFiles(root))
        {
            var original = File.ReadAllText(file);
            var rewritten = RewriteCoordinates(original, version);
            if (rewritten == original)
                continue;
            File.WriteAllText(file, rewritten);
            var relative = Relative(root, file);
            rewrittenSources.Add(relative);
            changed.Add($"{relative}: install coordinate -> {version}");
        }

        changed.AddRange(RestampMirrorsOf(root, rewrittenSources));
        changed.Sort(StringComparer.Ordinal);
        return changed;
    }

    private static string RewriteCoordinates(string text, string version)
    {
        var withPackages = PackageReferenceCoordinate.Replace(
            text, m => m.Groups[1].Value + version + m.Groups[3].Value);
        return DotnetAddCoordinate.Replace(withPackages, m => m.Groups[1].Value + version);
    }

    private static IEnumerable<string> RestampMirrorsOf(string root, IReadOnlySet<string> rewrittenSources)
    {
        if (rewrittenSources.Count == 0)
            yield break;
        foreach (var mirror in TranslationCheckSupport.TranslatedFiles(root))
        {
            var restamp = RestampMirror(root, mirror, rewrittenSources);
            if (restamp is not null)
                yield return restamp;
        }
    }

    private static string? RestampMirror(string root, string mirror, IReadOnlySet<string> rewrittenSources)
    {
        var text = File.ReadAllText(mirror);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (TranslationCheckSupport.ParseHeader(lines[0]) is not { } header
            || !rewrittenSources.Contains(header.SourcePath))
            return null;
        var source = Path.GetFullPath(Path.Combine(root, header.SourcePath));
        if (!File.Exists(source))
            return null;
        var fresh = TranslationCheckSupport.GitBlobHash(File.ReadAllBytes(source))[..12];
        if (fresh == header.BlobHashPrefix)
            return null;
        lines[0] = lines[0].Replace($"blob {header.BlobHashPrefix}", $"blob {fresh}");
        File.WriteAllText(mirror, string.Join("\n", lines));
        return $"{Relative(root, mirror)}: blob hash restamped";
    }

    // -------------------------------------------------------------------------------------------
    // Check — the gate
    // -------------------------------------------------------------------------------------------

    /// <summary>Every problem across every governed document, sorted; empty when no page talks
    /// versions.</summary>
    public static IReadOnlyList<string> Check(string repoRoot, string version)
    {
        var root = Path.GetFullPath(repoRoot);
        var problems = new List<string>();
        foreach (var file in GovernedFiles(root))
        {
            var relative = Relative(root, file);
            var lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = i + 1;
                problems.AddRange(MarkerProblems(relative, line, lines[i]));
                problems.AddRange(CoordinateProblems(relative, line, lines[i], version));
            }
        }

        problems.Sort(StringComparer.Ordinal);
        return problems;
    }

    private static IEnumerable<string> MarkerProblems(string relative, int line, string text)
    {
        if (SinceMarker.IsMatch(text))
            yield return $"{relative}:{line}: a *(since …)* marker is version talk — state the "
                + "behaviour in the present tense instead";
        if (BannerMarker.IsMatch(text))
            yield return $"{relative}:{line}: the published-version banner was removed — docs "
                + "describe the code they ship with";
    }

    private static IEnumerable<string> CoordinateProblems(string relative, int line, string text, string version) =>
        CoordinateVersions(text)
            .Where(found => found != version)
            .Select(found =>
                $"{relative}:{line}: NarrativeTrace coordinate pinned to {found} but the build is at "
                + $"{version} — run ./build.sh SnippetSync, never hand-type a coordinate");

    private static IEnumerable<string> CoordinateVersions(string text) =>
        PackageReferenceCoordinate.Matches(text).Select(m => m.Groups[2].Value)
            .Concat(DotnetAddCoordinate.Matches(text).Select(m => m.Groups[2].Value));

    // -------------------------------------------------------------------------------------------
    // Scope
    // -------------------------------------------------------------------------------------------

    /// <summary>The public documents this rule governs: the root README and its language mirrors,
    /// and every <c>*.md</c> or <c>llms.txt</c> under <c>documentation/</c> that survives
    /// <c>.publishignore</c>.</summary>
    public static IReadOnlyList<string> GovernedFiles(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var stripped = PublishIgnorePatterns(root);
        return RootReadmes(root)
            .Concat(DocumentationPages(root))
            .Where(file => !IsStripped(root, file, stripped))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> RootReadmes(string root) =>
        Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly)
            .Where(file => Path.GetFileName(file) == "README.md" || IsReadmeMirror(file));

    private static bool IsReadmeMirror(string file)
    {
        using var reader = new StreamReader(file);
        var firstLine = reader.ReadLine() ?? "";
        return TranslationCheckSupport.ParseHeader(firstLine)?.SourcePath == "README.md";
    }

    private static IEnumerable<string> DocumentationPages(string root)
    {
        var documentation = Path.Combine(root, "documentation");
        if (!Directory.Exists(documentation))
            return Enumerable.Empty<string>();
        return Directory.EnumerateFiles(documentation, "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".md", StringComparison.Ordinal)
                || Path.GetFileName(file) == "llms.txt");
    }

    /// <summary>The <c>.publishignore</c> lines, comments and blanks removed — each a shell glob
    /// (<c>rm -rf $pattern</c> from the repo root) that strips a private path from the snapshot.</summary>
    private static IReadOnlyList<string> PublishIgnorePatterns(string root)
    {
        var file = Path.Combine(root, ".publishignore");
        if (!File.Exists(file))
            return Array.Empty<string>();
        return File.ReadAllLines(file)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();
    }

    private static bool IsStripped(string root, string file, IReadOnlyList<string> patterns)
    {
        var relative = Relative(root, file);
        return patterns.Any(pattern => Matches(relative, pattern));
    }

    /// <summary>Whether a repo-relative POSIX path is removed by one <c>.publishignore</c> glob: a
    /// glob pattern matches segment-wise (<c>*</c> never crosses <c>/</c>); a plain name matches the
    /// path itself or anything beneath it as a directory.</summary>
    private static bool Matches(string relative, string pattern)
    {
        if (pattern.Contains('*') || pattern.Contains('?'))
            return Regex.IsMatch(relative, "^" + GlobToRegex(pattern) + "$");
        return relative == pattern || relative.StartsWith(pattern + "/", StringComparison.Ordinal);
    }

    private static string GlobToRegex(string pattern)
    {
        var builder = new StringBuilder();
        foreach (var ch in pattern)
            builder.Append(ch switch
            {
                '*' => "[^/]*",
                '?' => "[^/]",
                _ => Regex.Escape(ch.ToString()),
            });
        return builder.ToString();
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');
}
