// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Counts <c>*(since X.Y.Z, unreleased)*</c> markers across a runtime's English docs, for the
/// <c>llms.txt</c> "docs vs published" banner's unreleased-count clause (design note
/// <c>docs-vs-published-gate-2026-09-12.md</c> §1 item 8). Pure logic over file contents already
/// read from disk — no filesystem, no network — mirroring how <see cref="PublishedVersionSupport"/>
/// keeps the banner's own logic pure; <c>Build.cs</c> supplies which files count (documentation/**
/// + README.md + llms.txt/llms-full.md, translated mirrors excluded via
/// <see cref="TranslationCheckSupport.TranslatedFiles"/> — a mirror repeats its English source's
/// markers verbatim, so counting it too would double the true number) and reads their content.
/// </summary>
internal static class UnreleasedMarkerSupport
{
    /// <summary>
    /// The same pattern the publish script's tag-time gate scans a staged snapshot
    /// for (see <c>PublishScriptMarkerTests</c>): tolerant of extra whitespace after "since" and
    /// after the comma — a marker wrapped across a Markdown hard-wrap lands on either seam
    /// ('*(since\nX.Y.Z, unreleased)*' or '*(since X.Y.Z,\nunreleased)*') — so this, the bash
    /// rewrite, and the bash gate all agree on the same marker shape.
    /// </summary>
    private static readonly Regex MarkerPattern = new(
        @"\*\(since\s+\d+\.\d+\.\d+,\s*unreleased\)\*", RegexOptions.CultureInvariant);

    /// <summary>Same shape as <see cref="MarkerPattern"/>, with the version captured — for <see cref="DistinctVersions"/>.</summary>
    private static readonly Regex MarkerWithVersion = new(
        @"\*\(since\s+(\d+\.\d+\.\d+),\s*unreleased\)\*", RegexOptions.CultureInvariant);

    /// <summary>Marker occurrences in one file's content.</summary>
    public static int Count(string content) => MarkerPattern.Count(content);

    /// <summary>Marker occurrences summed across every counted file's content.</summary>
    public static int CountAll(IEnumerable<string> fileContents) => fileContents.Sum(Count);

    /// <summary>
    /// Every distinct version cited by a <c>*(since X.Y.Z, unreleased)*</c> marker across every
    /// counted file's content — for the <c>ContractLint</c> gate (docs-vs-published-gate §2/§5.1
    /// ruling 3), which checks that each one has at least one <c>documentation/contract.yaml</c>
    /// entry recording it.
    /// </summary>
    public static IReadOnlySet<string> DistinctVersions(IEnumerable<string> fileContents) =>
        fileContents
            .SelectMany(content => MarkerWithVersion.Matches(content).Select(match => match.Groups[1].Value))
            .ToHashSet();
}
