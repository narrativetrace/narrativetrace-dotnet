// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Resolves a <see cref="SnippetStep"/> against the real file it names — the whole file, or a
/// named <c>snippet:begin</c>/<c>snippet:end</c> region within it, dedented. Mirrors the
/// documentation build's own snippet convention (<c>SnippetCheckSupport</c>) closely enough to
/// embed the same content — region extraction and dedent are small enough to duplicate rather than
/// reference the build project for, but license-header stripping is shared for real, through
/// <see cref="LicenseHeaderStripper"/>: both sides embedding the SAME publish-time-stamped source
/// file must agree on what a header stamp does to it, or a rendered page drifts the moment a
/// public snapshot is staged (never in this checkout, where no source ever carries the header).
/// </summary>
internal static class SnippetResolver
{
    /// <summary>Reads and resolves the snippet's content from <paramref name="repoRoot"/>.</summary>
    public static string Resolve(SnippetStep snippet, string repoRoot)
    {
        var fullPath = Path.Combine(repoRoot, snippet.Path);
        var content = LicenseHeaderStripper.Strip(File.ReadAllText(fullPath).Replace("\r\n", "\n"));
        return snippet.Region is null ? content.TrimEnd('\n', '\r') : ExtractRegion(content, snippet.Region);
    }

    private static string ExtractRegion(string content, string region)
    {
        var lines = content.Split('\n');
        var begin = Array.FindIndex(lines, l => l.Contains($"snippet:begin {region}", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, l => l.Contains($"snippet:end {region}", StringComparison.Ordinal));
        if (begin < 0 || end < 0 || end <= begin)
        {
            throw new InvalidOperationException($"snippet region '{region}' not found or malformed");
        }

        return Dedent(lines[(begin + 1)..end]);
    }

    private static string Dedent(string[] lines)
    {
        var indent = lines.Where(l => l.Trim().Length > 0).Min(l => l.Length - l.TrimStart().Length);
        return string.Join('\n', lines.Select(l => l.Length >= indent ? l[indent..] : l)).TrimEnd('\n', '\r');
    }
}
