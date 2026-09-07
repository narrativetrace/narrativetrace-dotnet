// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Globalization;

namespace NarrativeTrace.StressTests;

/// <summary>
/// The repetition count every stress test loops its race for. Bounded and
/// seeded by default so the whole suite stays inside the gate's seconds
/// budget; overridden by the <c>Stress</c> build target for the long
/// randomized sweep.
/// </summary>
/// <remarks>
/// Mirrors the split jcstress itself documents
/// (<c>documentation/concurrency-testing.md</c> in the Java runtime): a
/// short, bounded mode runs on every commit, a long unbounded sweep runs on
/// the scheduled/manual job — the same shape this runtime already uses for
/// mutation testing and fuzzing.
/// </remarks>
internal static class StressIterations
{
    private const string EnvironmentVariable = "NARRATIVETRACE_STRESS_ITERATIONS";

    /// <summary>
    /// Default repetition count in the gate's short mode: enough for a
    /// logic bug (miscounted loss, a broken invariant) to show up on the
    /// first or second trial, and enough to give a genuine timing race a
    /// real chance without pushing total suite time past a few seconds.
    /// </summary>
    internal const int DefaultShortCount = 300;

    /// <summary>The repetition count resolved for this process.</summary>
    internal static int Count { get; } = Resolve();

    private static int Resolve()
    {
        var raw = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return int.TryParse(
            raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0
            ? parsed
            : DefaultShortCount;
    }
}
