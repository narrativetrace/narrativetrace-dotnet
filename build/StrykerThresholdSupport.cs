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
/// Stryker.NET's own <c>thresholds.{high,low,break}</c> schema is <c>int?</c>, not
/// <c>double?</c> (nightly 2026-09-19, B-40: a ratchet wrote a measured score straight into
/// <c>break</c>/<c>low</c> as a float — <c>76.36</c> — and Stryker refused to even parse the
/// config: "The JSON value could not be converted to System.Nullable`1[System.Int32]"). This is
/// the config lint that catches a non-integer threshold, or a <c>break</c>/<c>low</c>/<c>high</c>
/// out of order, before Stryker ever runs — on every real <c>stryker-config*.json</c> the ratchet
/// touches.
/// </summary>
public static class StrykerThresholdSupport
{
    /// <param name="Module">The module name (<see cref="MutationAccounting.ModuleName"/>).</param>
    public readonly record struct Thresholds(string Module, int High, int Low, int Break);

    /// <summary>
    /// Every ordering violation among already-integer thresholds: <c>break</c> must never exceed
    /// <c>low</c>, and <c>low</c> must never exceed <c>high</c> — Stryker's own accepted range.
    /// Pure so the comparison logic is unit-tested directly, never re-derived only against the
    /// real repo files.
    /// </summary>
    public static IReadOnlyList<string> OrderingViolations(IEnumerable<Thresholds> thresholds) =>
        thresholds
            .SelectMany(t => new[]
            {
                t.Break > t.Low
                    ? $"{t.Module}: thresholds.break ({t.Break}) is above thresholds.low ({t.Low})"
                    : null,
                t.Low > t.High
                    ? $"{t.Module}: thresholds.low ({t.Low}) is above thresholds.high ({t.High})"
                    : null,
            })
            .OfType<string>()
            .OrderBy(message => message, System.StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Every <c>thresholds.{high,low,break}</c> entry in <paramref name="configPath"/> that is not
    /// a whole number — Stryker deserializes each into <c>int?</c> and refuses to even parse the
    /// file otherwise, never a partial run with a silently rounded value.
    /// </summary>
    public static IReadOnlyList<string> NonIntegerThresholds(string configPath, string module)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var thresholds = document.RootElement.GetProperty("stryker-config").GetProperty("thresholds");

        return new[] { "high", "low", "break" }
            .Where(name => !thresholds.GetProperty(name).TryGetInt32(out _))
            .Select(name => $"{module}: thresholds.{name} "
                + $"({thresholds.GetProperty(name).GetRawText()}) is not an integer — Stryker's "
                + "threshold schema is int?, not double?")
            .ToList();
    }

    /// <summary>
    /// <paramref name="configPath"/>'s three thresholds, read as integers — <c>null</c> when any
    /// of them is not a whole number (see <see cref="NonIntegerThresholds"/> for that failure
    /// reported on its own terms, never a thrown parse exception this caller would have to catch).
    /// </summary>
    public static Thresholds? ReadIntegerThresholds(string configPath, string module)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var thresholds = document.RootElement.GetProperty("stryker-config").GetProperty("thresholds");

        if (!thresholds.GetProperty("high").TryGetInt32(out var high)
            || !thresholds.GetProperty("low").TryGetInt32(out var low)
            || !thresholds.GetProperty("break").TryGetInt32(out var brk))
            return null;

        return new Thresholds(module, high, low, brk);
    }
}
