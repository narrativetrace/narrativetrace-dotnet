// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// Per-module wall-clock time budgets for <c>Mutation</c>'s Stryker.NET runs — backs the fix for
/// the 2026-09-17 nightly, where <c>./build.sh Mutation</c> ran with no bound of its own, was
/// killed by the launchd wrapper's outer timeout, and produced zero module reports: a run with no
/// per-module bound loses everything to any single slow module, on any external kill. The
/// <c>Mutation</c> target enforces <see cref="SelectBudgets"/>'s numbers as a wall-clock cap
/// around each module's Stryker subprocess — a module that exceeds its budget is killed, named as
/// a gap, and the loop moves on; every other module still gets its full attempt.
/// </summary>
/// <remarks>
/// <para>
/// <b>No "last successful run" exists to read real per-module durations from.</b> Every
/// <c>dotnet-mutation.log</c> under <c>nightly-quality-logs/2026-09-{02..17}</c> either shows the
/// launchd wrapper's own TIMED OUT status (each night's <c>SUMMARY.md</c>: 120m on 09-02, up to
/// 180m by 09-06/09-10/09-11/09-12, 360m from 09-15 on — raised repeatedly, never enough) or an
/// early crash (rc=255, under two minutes). None ever got past Stryker identifying and building
/// the *first* module (alphabetically <c>clarity</c>) before the log stops advancing — Stryker's
/// live progress reporter does not append further timestamped lines the capture can see, so the
/// visible log always ends at the same "N total mutants will be tested" line regardless of how
/// long the run actually continued afterward. The one number that does exist,
/// <c>reports/verification/2026-09-09.json</c>'s committed <c>VerifyAll</c> row
/// (<c>duration_seconds: 1878.55</c>, three modules excluded via <c>--mutation-exclude</c>),
/// confirms the same shape from the other side: ~31 minutes produced reports for zero of the three
/// smaller modules it ran.
/// </para>
/// <para>
/// <see cref="ProductionWeights"/> therefore weights by scored-mutant counts where real evidence
/// exists — <c>clarity</c>: 3755, stable across six separate nights' logs (Killed+Survived+
/// NoCoverage+Timeout after Stryker's own Ignored/CompileError filtering); <c>valuerefs</c>: 116,
/// from <see cref="StrykerReportSupport"/>'s own doc comment, itself confirmed against a real
/// completed run of that narrow (<c>ValueReferenceIndex.cs</c> only) config — and an estimate for
/// the rest, extrapolated from clarity's real mutants-per-source-line ratio (3755 / 2747 lines
/// &#8776; 1.367) applied to each remaining module's own non-blank, non-comment line count, counted
/// directly from <c>src/</c> (not from any log): core 6725, glossary 3179, proxy 885.
/// </para>
/// </remarks>
public static class MutationBudgetSupport
{
    /// <summary>
    /// Estimated relative mutant-testing cost per module — see the class remarks for how each
    /// number was sourced. The single input to <see cref="SelectBudgets"/> for a real
    /// <c>./build.sh Mutation</c> run.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> ProductionWeights = new Dictionary<string, int>
    {
        ["core"] = 9193, // 6725 non-blank/non-comment lines x clarity's real 1.367 mutants/line
        ["clarity"] = 3755, // real: scored-mutant count, stable across six nights' logs
        ["glossary"] = 4346, // 3179 lines x 1.367
        ["proxy"] = 1210, // 885 lines x 1.367
        ["valuerefs"] = 116, // real: Killed 111 + Survived 3 + NoCoverage 1 + Timeout 2 (StrykerReportSupportTests)
    };

    /// <summary>Total wall-clock ceiling for one <c>Mutation</c> run — comfortably under every
    /// nightly TIMED OUT threshold on record (up to 360 minutes) so a run that hits every module's
    /// budget in full still finishes within the outer wrapper, with margin for Restore/Compile.</summary>
    public static readonly TimeSpan ProductionCeiling = TimeSpan.FromMinutes(300);

    /// <summary>Minimum per-module budget — Stryker's own analysis + solution build phase alone
    /// took 2-5 minutes in a loaded container during this fix's own investigation; a smaller floor
    /// risks killing a module before it starts testing a single mutant.</summary>
    public static readonly TimeSpan ProductionFloor = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Splits <paramref name="totalCeiling"/> across every key of <paramref name="weightByModule"/>
    /// proportional to its weight, then raises any module whose proportional share would fall under
    /// <paramref name="floor"/> up to the floor and re-proportions the remaining budget across the
    /// modules not yet floored — repeating (a module raised to the floor can push another module's
    /// share below the floor too) until every module is settled. Equal weights when
    /// <paramref name="weightByModule"/>'s values are all zero (or the set remaining is weightless)
    /// get an equal split rather than a division by zero. The returned budgets always sum to
    /// <paramref name="totalCeiling"/> when <c>floor * weightByModule.Count &lt;= totalCeiling</c>;
    /// otherwise every module gets exactly <paramref name="floor"/> (a floor promise that cannot
    /// all be honored within the ceiling is still honored per-module, so the sum can exceed the
    /// ceiling — a misconfiguration the caller's own numbers must avoid, not something this method
    /// can silently repair without breaking the floor guarantee).
    /// </summary>
    public static IReadOnlyDictionary<string, TimeSpan> SelectBudgets(
        IReadOnlyDictionary<string, int> weightByModule, TimeSpan totalCeiling, TimeSpan floor)
    {
        var result = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        if (weightByModule.Count == 0)
            return result;

        var remaining = new List<string>(weightByModule.Keys);
        var budgetLeft = totalCeiling;

        while (remaining.Count > 0)
        {
            var weightSum = remaining.Sum(module => (double)weightByModule[module]);
            var toFloor = remaining
                .Where(module => ProportionalShare(module, weightByModule, remaining, weightSum, budgetLeft) < floor)
                .ToList();

            if (toFloor.Count == 0)
            {
                foreach (var module in remaining)
                    result[module] = ProportionalShare(module, weightByModule, remaining, weightSum, budgetLeft);
                remaining.Clear();
                continue;
            }

            foreach (var module in toFloor)
            {
                result[module] = floor;
                var spent = TimeSpan.FromTicks(Math.Min(floor.Ticks, budgetLeft.Ticks));
                budgetLeft -= spent;
                remaining.Remove(module);
            }
        }
        return result;
    }

    private static TimeSpan ProportionalShare(
        string module, IReadOnlyDictionary<string, int> weightByModule, List<string> remaining,
        double weightSum, TimeSpan budgetLeft)
    {
        if (weightSum <= 0)
            return TimeSpan.FromTicks(budgetLeft.Ticks / remaining.Count);
        var fraction = weightByModule[module] / weightSum;
        return TimeSpan.FromTicks((long)(budgetLeft.Ticks * fraction));
    }
}
