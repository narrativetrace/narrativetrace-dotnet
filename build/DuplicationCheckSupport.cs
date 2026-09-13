// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;

namespace NarrativeTrace.Build;

/// <summary>The committed <c>config/duplication/baseline.properties</c> — main tree only; tests never gate.</summary>
public sealed record DuplicationBaseline(double MainPercent, int MainLargestCluster, string Recorded, string Commit);

/// <summary>One <c>config/duplication/exemptions.txt</c> entry: a deliberate pair, with the reason it exists.</summary>
public sealed record DuplicationExemption(string GlobA, string GlobB, string Reason);

public sealed record DuplicationCheckResult(bool Passed, string Message);

/// <summary>
/// INTENT: Backs the root <c>DuplicationCheck</c> Nuke target — a ratchet against a committed
/// baseline, not a fixed percentage (see documentation/duplication.md for why: the "right" number
/// depends on the token floor and on how much test scaffolding legitimately repeats, so a fixed
/// threshold is either loose enough to never fire or tight enough to block unrelated work). Mirrors
/// the Java runtime's buildSrc <c>DuplicationCheckSupport</c> field-for-field (same
/// <c>baseline.properties</c>/<c>exemptions.txt</c> keys and shape) so the two ratchets read as one
/// family-wide convention, not two.
///
/// Only the main tree gates; test-tree duplication is reported by <c>DuplicationReport</c> and never
/// reaches this class.
/// </summary>
public static class DuplicationCheckSupport
{
    /// <summary>Percentage-point slack absorbing token-count noise between runs (owner ruling 2026-09-12).</summary>
    public const double PercentTolerance = 0.3;

    public static DuplicationBaseline ReadBaseline(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new ArgumentException(
                $"{filePath}: no duplication baseline — run DuplicationReport and commit one");
        }

        var properties = ReadProperties(filePath);
        string Required(string key) =>
            properties.TryGetValue(key, out var value)
                ? value
                : throw new ArgumentException($"{filePath}: missing '{key}'");

        return new DuplicationBaseline(
            MainPercent: double.Parse(Required("main.percent"), CultureInfo.InvariantCulture),
            MainLargestCluster: int.Parse(Required("main.largestCluster"), CultureInfo.InvariantCulture),
            Recorded: properties.GetValueOrDefault("recorded", string.Empty),
            Commit: properties.GetValueOrDefault("commit", string.Empty));
    }

    /// <summary>A minimal <c>key=value</c> reader — blank lines and <c>#</c>-comment lines are skipped,
    /// matching the subset of Java's <c>java.util.Properties</c> syntax this repository's baseline files use.</summary>
    private static Dictionary<string, string> ReadProperties(string filePath)
    {
        var result = new Dictionary<string, string>();
        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var separator = line.IndexOf('=');
            if (separator < 0)
                continue;
            result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return result;
    }

    /// <summary>
    /// Parses <c>exemptions.txt</c>: blank-line-separated entries, each a <c># reason</c> line (one
    /// or more, concatenated) immediately followed by one <c>globA :: globB</c> pair line.
    /// Default-deny: a pair line with no reason above it is a malformed file, not a silent pass.
    /// </summary>
    public static IReadOnlyList<DuplicationExemption> ReadExemptions(string filePath)
    {
        if (!File.Exists(filePath))
            return Array.Empty<DuplicationExemption>();

        var exemptions = new List<DuplicationExemption>();
        string? pendingReason = null;
        var lines = File.ReadAllLines(filePath);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                pendingReason = null;
            }
            else if (line.StartsWith('#'))
            {
                var text = line.TrimStart('#').Trim();
                pendingReason = pendingReason is null ? text : $"{pendingReason} {text}";
            }
            else
            {
                var reason = pendingReason ?? throw new ArgumentException(
                    $"{filePath}:{index + 1}: exemption pair has no '# reason' line above it: {line}");
                var parts = line.Split("::").Select(p => p.Trim()).ToArray();
                if (parts.Length != 2 || parts.Any(string.IsNullOrEmpty))
                {
                    throw new ArgumentException(
                        $"{filePath}:{index + 1}: expected 'globA :: globB', got: {line}");
                }
                exemptions.Add(new DuplicationExemption(parts[0], parts[1], reason));
                pendingReason = null;
            }
        }
        return exemptions;
    }

    /// <summary>A cluster is exempt when every occurrence's path matches one of a pair's two globs.</summary>
    public static bool IsExempt(DuplicationCluster cluster, IReadOnlyList<DuplicationExemption> exemptions) =>
        exemptions.Any(exemption => cluster.Occurrences.All(occurrence =>
            MatchesGlob(exemption.GlobA, occurrence.File) || MatchesGlob(exemption.GlobB, occurrence.File)));

    /// <summary>
    /// Matches a repo-root-relative path against one glob, via <c>Microsoft.Extensions.FileSystemGlobbing</c>
    /// (already on this build's graph through Nuke.Common's own transitive reference — no new
    /// dependency). [path] is matched as a virtual file name; nothing here touches disk.
    /// </summary>
    private static bool MatchesGlob(string glob, string path)
    {
        var matcher = new Matcher();
        matcher.AddInclude(glob);
        return matcher.Match(path).HasMatches;
    }

    /// <summary>
    /// The ratchet: fails when main's percentage rose past <see cref="PercentTolerance"/> over the
    /// baseline, or when a non-exempt cluster is bigger than the baseline's recorded largest —
    /// either one, on its own, is new duplication the baseline never accounted for.
    /// </summary>
    public static DuplicationCheckResult Decide(
        DuplicationTreeResult main, DuplicationBaseline baseline, IReadOnlyList<DuplicationExemption> exemptions)
    {
        var percentFailed = main.Percent - baseline.MainPercent > PercentTolerance;
        var offending = main.Clusters
            .Where(c => c.Tokens > baseline.MainLargestCluster && !IsExempt(c, exemptions))
            .ToList();

        if (!percentFailed && offending.Count == 0)
        {
            // The largest-cluster ratchet is anchored on non-exempt clusters: a data table exempted
            // by path must not set the bar a real copy elsewhere is measured against.
            var largest = main.Clusters.Where(c => !IsExempt(c, exemptions)).Select(c => c.Tokens).DefaultIfEmpty(0).Max();
            return new DuplicationCheckResult(
                true,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "DuplicationCheck: main {0:F1}% within baseline {1:F1}% (+/-{2:F1}), largest non-exempt cluster {3} tokens (baseline {4})",
                    main.Percent, baseline.MainPercent, PercentTolerance, largest, baseline.MainLargestCluster));
        }

        var problems = new List<string>();
        if (percentFailed)
        {
            problems.Add(string.Format(
                CultureInfo.InvariantCulture,
                "main duplication rose to {0:F1}% (baseline {1:F1}% + {2:F1} tolerance)",
                main.Percent, baseline.MainPercent, PercentTolerance));
        }
        foreach (var cluster in offending)
        {
            var locations = string.Join(" ↔ ", cluster.Occurrences.Select(o => $"{o.File}:{o.StartLine}"));
            problems.Add($"new cluster {cluster.Tokens} tokens (baseline largest {baseline.MainLargestCluster}): {locations}");
        }

        var message = "DuplicationCheck failed:\n  " + string.Join("\n  ", problems) + "\n"
            + "Lower the baseline (config/duplication/baseline.properties) with the commit that removes "
            + "the duplication, or add a reasoned 'globA :: globB' pair to config/duplication/exemptions.txt "
            + "if it is deliberate — see documentation/duplication.md.";
        return new DuplicationCheckResult(false, message);
    }
}
