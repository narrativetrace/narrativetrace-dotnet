// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NarrativeTrace.Build;

/// <summary>
/// One module's mutant tally, read from a Stryker.NET <c>mutation-report.json</c>. Only the four
/// "real test opportunity" statuses are counted: <c>Ignored</c> (removed by a <c>mutate</c>/block
/// filter — never offered to a test at all) and <c>CompileError</c>/<c>RuntimeError</c> (the
/// mutation itself was invalid, not a test gap) are excluded from every count here, including the
/// score's own denominator — confirmed against a real run of this repo's own
/// <c>stryker-config.valuerefs.json</c> (Stryker's own console summary: Killed 111, Survived 3,
/// Timeout 2 — the tallies here reproduce exactly that, out of 4296 total array entries, most of
/// them <c>Ignored</c>/<c>CompileError</c> noise this record deliberately does not touch).
/// </summary>
public sealed record MutationCounts(int Killed, int Survived, int NoCoverage, int Timeout)
{
    public static readonly MutationCounts Empty = new(0, 0, 0, 0);

    public static MutationCounts operator +(MutationCounts a, MutationCounts b) =>
        new(a.Killed + b.Killed, a.Survived + b.Survived, a.NoCoverage + b.NoCoverage, a.Timeout + b.Timeout);

    /// <summary>The classic mutation-score denominator: every mutant that was a genuine test
    /// opportunity, killed or not.</summary>
    public int Scored => Killed + Survived + NoCoverage + Timeout;

    /// <summary>Killed / Scored, as a percentage — <c>0</c> for a report with no scored mutants
    /// rather than NaN.</summary>
    public double Score => Scored == 0 ? 0.0 : (double)Killed / Scored * 100.0;
}

/// <summary>
/// Reads Stryker.NET's <c>mutation-report.json</c> (the open mutation-testing-elements schema
/// shared with Stryker.js/Infection: <c>files.&lt;path&gt;.mutants[].status</c>) — the counts
/// <c>./build.sh Mutation</c>'s own console output never breaks out per-module. Mirrors Java's
/// buildSrc <c>MutationReportSupport</c> reading Pitest's <c>mutations.xml</c>.
/// </summary>
public static class StrykerReportSupport
{
    /// <summary>
    /// <c>null</c> when <paramref name="reportPath"/> is absent — a module whose Stryker run
    /// produced no report at all (crashed before writing one) is a fact the caller must be able to
    /// tell apart from "ran clean with zero mutants", never silently zero.
    /// </summary>
    public static MutationCounts? Read(string reportPath)
    {
        if (!File.Exists(reportPath))
            return null;

        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        if (!doc.RootElement.TryGetProperty("files", out var files))
            return MutationCounts.Empty;

        var killed = 0;
        var survived = 0;
        var noCoverage = 0;
        var timeout = 0;

        foreach (var file in files.EnumerateObject())
        {
            if (!file.Value.TryGetProperty("mutants", out var mutants))
                continue;
            foreach (var mutant in mutants.EnumerateArray())
            {
                var status = mutant.TryGetProperty("status", out var s) ? s.GetString() : null;
                switch (status)
                {
                    case "Killed": killed++; break;
                    case "Survived": survived++; break;
                    case "NoCoverage": noCoverage++; break;
                    case "Timeout": timeout++; break;
                        // Ignored (removed by a mutate/block filter — never offered to a test) and
                        // CompileError/RuntimeError (the mutation itself was invalid) are real Stryker
                        // statuses that are not a test-coverage gap; deliberately excluded from every
                        // count, matching Stryker's own mutation-score denominator.
                }
            }
        }

        return new MutationCounts(killed, survived, noCoverage, timeout);
    }

    /// <summary>
    /// Sums every module's counts, treating a module with no report (<see cref="Read"/> returned
    /// <c>null</c>) as <b>missing</b>, not zero — <paramref name="missingModules"/> collects which
    /// ones, so a silently-dropped module (the exact defect Java's own <c>verifyAll</c> first run
    /// exposed: 2 of 5 modules missing) is visible in the row's own note rather than hidden inside
    /// a total that happens to still look plausible.
    /// </summary>
    public static MutationCounts ReadAll(
        IReadOnlyDictionary<string, string> reportPathsByModule, out IReadOnlyList<string> missingModules)
    {
        var total = MutationCounts.Empty;
        var missing = new List<string>();
        foreach (var (module, path) in reportPathsByModule)
        {
            var counts = Read(path);
            if (counts is null)
                missing.Add(module);
            else
                total += counts;
        }
        missingModules = missing;
        return total;
    }
}
