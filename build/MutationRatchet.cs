// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NarrativeTrace.Build;

/// <summary>
/// The single recorded-score floor behind every <c>stryker-config*.json</c>'s <c>break</c>
/// (owner ruling 2026-09-18, TODO §35): once a module's honest mutation score is measured, its
/// break becomes THAT score — never the aspirational target — and the floor can only move up.
/// <c>mutation-ratchet.json</c> at the repo root is the one file both a human (raising the floor
/// after real test work) and <see cref="MutationRatchetTests"/> (asserting nobody lowered it)
/// read; the test never hardcodes a module's floor score itself, so the check is derived from
/// that file, not remembered a second time in C#.
/// </summary>
internal static class MutationRatchet
{
    /// <param name="Module">The module name (<see cref="MutationAccounting.ModuleName"/>).</param>
    /// <param name="Score">The recorded mutation score floor — the last measured, honest score.</param>
    public readonly record struct Floor(string Module, double Score);

    /// <summary>Every module's recorded floor, read fresh out of <c>mutation-ratchet.json</c>.</summary>
    public static IReadOnlyDictionary<string, Floor> ReadFloors(string ratchetPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ratchetPath));
        var floors = new Dictionary<string, Floor>();

        foreach (var module in document.RootElement.EnumerateObject())
        {
            var score = module.Value.GetProperty("score").GetDouble();
            floors[module.Name] = new Floor(module.Name, score);
        }

        return floors;
    }

    /// <summary>The <c>thresholds.break</c> value a real stryker-config file currently carries.</summary>
    public static double ConfiguredBreak(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        return document.RootElement
            .GetProperty("stryker-config")
            .GetProperty("thresholds")
            .GetProperty("break")
            .GetDouble();
    }

    /// <summary>
    /// Every module whose CONFIGURED break has dropped below its RECORDED floor — a module with
    /// no recorded floor yet is not a violation (nothing to compare against), matching
    /// <see cref="MutationAccounting"/>'s own "absence is not yet a verdict" shape.
    /// </summary>
    public static IReadOnlyList<string> LoweredBreaks(
        IReadOnlyDictionary<string, Floor> floors, IReadOnlyDictionary<string, double> configuredBreaks)
    {
        return floors.Values
            .Where(floor => configuredBreaks.TryGetValue(floor.Module, out var configured)
                && configured < floor.Score)
            .Select(floor => $"{floor.Module}: configured break "
                + $"{configuredBreaks[floor.Module].ToString(CultureInfo.InvariantCulture)} is below "
                + $"the recorded floor {floor.Score.ToString(CultureInfo.InvariantCulture)} "
                + "— mutation-ratchet.json only ever moves up")
            .OrderBy(message => message, System.StringComparer.Ordinal)
            .ToList();
    }
}
