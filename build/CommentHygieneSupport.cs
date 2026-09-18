// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>CommentHygieneCheck</c> target: a per-commit lint against audit-ledger history and
/// port-framing left behind in code comments (<c>//</c>, <c>///</c>, <c>/* */</c>) under
/// <c>src/**/*.cs</c> — product code only, <c>tests/</c> is out of scope by construction. A code
/// comment carries the constraint a reader of this runtime alone must respect, not the ledger entry
/// that produced it, and not this runtime's place in a multi-language family — both belong in the
/// commit that made the change, never in a comment that survives it.
/// </summary>
/// <remarks>
/// Structurally the C# mirror of the TS reference implementation
/// (<c>../narrative-trace-ts/tools/comment-hygiene.ts</c>, read-only, not shared code) and the
/// Python port's <c>scripts/comment_hygiene.py</c>: walk the source tree, collect hits, excuse
/// exactly the files a JSON allowlist (file → reason) names, and flag an allowlist entry whose file
/// no longer has any hit as stale — the allowlist is meant to shrink as each project's own wave
/// lands, never to accumulate dead entries a later wave forgot to remove.
/// </remarks>
internal static class CommentHygieneSupport
{
    /// <summary>
    /// An audit-ledger citation surviving in a comment: an owner-ruling reference, a
    /// "ruled 20YY-..." phrase, a bare "(20YY-MM-DD)" audit date, or shipped-release wording
    /// ", unreleased)".
    /// </summary>
    public static readonly Regex HistoryPattern = new(
        @"owner ruling|ruled 20\d\d|\(20\d\d-\d\d-\d\d\)|, unreleased\)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Language framing this runtime as secondary to a Java implementation, inside a comment a
    /// reader of this runtime alone would see. Framing this runtime as one of a family ("same as
    /// every NarrativeTrace runtime") is fine; naming Java specifically as the one true source is
    /// not.
    /// </summary>
    public static readonly Regex PortFramingPattern = new(
        @"golden source|the Java (repo|runtime|implementation)|mirrors Java|as in Java|"
        + @"the JVM edition|matching the JVM", RegexOptions.CultureInvariant);

    private static readonly string[] ExcludedDirectories = ["bin", "obj"];

    /// <summary>One line matching <see cref="HistoryPattern"/> or <see cref="PortFramingPattern"/>.
    /// <see cref="File"/> is repo-relative, POSIX-separated; <see cref="Line"/> is 1-indexed;
    /// <see cref="Text"/> is the matched line, trimmed; <see cref="Reason"/> is
    /// <c>"history"</c> or <c>"port-framing"</c>.</summary>
    public sealed record CommentHygieneHit(string File, int Line, string Text, string Reason);

    /// <param name="Violations">Hits outside the allowlist — a red gate: <c>Verify</c> must fail on
    /// these.</param>
    /// <param name="StaleAllowlistEntries">Allowlist entries naming a file with zero current hits,
    /// sorted.</param>
    public sealed record LintResult(
        IReadOnlyList<CommentHygieneHit> Violations, IReadOnlyList<string> StaleAllowlistEntries);

    /// <summary>Every <c>.cs</c> file under <c>&lt;repoRoot&gt;/src/**</c>, sorted — never
    /// generated <c>bin</c>/<c>obj</c> output, never <c>tests/</c> (a sibling of <c>src</c>, never
    /// nested inside it).</summary>
    public static IReadOnlyList<string> SourceFiles(string repoRoot)
    {
        var src = Path.Combine(repoRoot, "src");
        if (!Directory.Exists(src))
            return [];
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar)
                .Any(segment => ExcludedDirectories.Contains(segment, StringComparer.Ordinal)))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    private static string? ReasonFor(string text)
    {
        if (HistoryPattern.IsMatch(text))
            return "history";
        return PortFramingPattern.IsMatch(text) ? "port-framing" : null;
    }

    /// <summary>Every <see cref="HistoryPattern"/>/<see cref="PortFramingPattern"/> hit across
    /// <see cref="SourceFiles"/>, repo-relative and sorted by file then line — allowlist filtering
    /// is the caller's job (see <see cref="Lint"/>).</summary>
    public static IReadOnlyList<CommentHygieneHit> FindHits(string repoRoot)
    {
        var hits = new List<CommentHygieneHit>();
        foreach (var file in SourceFiles(repoRoot))
        {
            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (ReasonFor(lines[i]) is { } reason)
                    hits.Add(new CommentHygieneHit(relative, i + 1, lines[i].Trim(), reason));
            }
        }

        return hits;
    }

    /// <summary>Lints <c>src/**/*.cs</c> for <see cref="HistoryPattern"/>/
    /// <see cref="PortFramingPattern"/> hits, excusing exactly the files named in
    /// <paramref name="allowlist"/> (repo-relative, POSIX-separated paths, each mapped to a human
    /// reason — enforced by the caller, never read here).</summary>
    public static LintResult Lint(string repoRoot, IReadOnlyDictionary<string, string> allowlist)
    {
        var hits = FindHits(repoRoot);
        var hitFiles = hits.Select(hit => hit.File).ToHashSet(StringComparer.Ordinal);
        var violations = hits.Where(hit => !allowlist.ContainsKey(hit.File)).ToList();
        var stale = allowlist.Keys
            .Where(file => !hitFiles.Contains(file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
        return new LintResult(violations, stale);
    }

    /// <summary>Reads the JSON allowlist (file → reason) at <paramref name="path"/>; a missing file
    /// is an empty allowlist — the first comment-hygiene wave lands before the file exists.</summary>
    public static IReadOnlyDictionary<string, string> LoadAllowlist(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.Ordinal);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
        return parsed ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
