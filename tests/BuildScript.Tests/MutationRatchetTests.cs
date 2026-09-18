// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the mutation-score ratchet (owner ruling 2026-09-18, TODO §35): a stryker-config's
/// <c>break</c> is set to the module's measured score, never a lower aspirational target, and it
/// may only move UP from here. <c>mutation-ratchet.json</c> is the one recorded-floor file both a
/// human (raising it after real test work) and this test read — never a second, hand-copied
/// number inside the test itself.
/// </summary>
public sealed class MutationRatchetTests
{
    [Fact]
    public void A_configured_break_at_the_recorded_floor_is_not_a_violation()
    {
        var floors = new Dictionary<string, MutationRatchet.Floor>
        {
            ["clarity"] = new("clarity", 76.36),
        };
        var configured = new Dictionary<string, double> { ["clarity"] = 76.36 };

        Assert.Empty(MutationRatchet.LoweredBreaks(floors, configured));
    }

    [Fact]
    public void A_configured_break_above_the_recorded_floor_is_not_a_violation()
    {
        var floors = new Dictionary<string, MutationRatchet.Floor>
        {
            ["clarity"] = new("clarity", 76.36),
        };
        var configured = new Dictionary<string, double> { ["clarity"] = 80.0 };

        Assert.Empty(MutationRatchet.LoweredBreaks(floors, configured));
    }

    /// <summary>
    /// The "observed red" this ratchet exists for: a config edit that quietly drops the break
    /// below the last recorded score must fail, even though it is still a perfectly valid
    /// Stryker threshold on its own.
    /// </summary>
    [Fact]
    public void A_configured_break_below_the_recorded_floor_is_a_violation()
    {
        var floors = new Dictionary<string, MutationRatchet.Floor>
        {
            ["clarity"] = new("clarity", 76.36),
        };
        var configured = new Dictionary<string, double> { ["clarity"] = 70.0 };

        var violations = MutationRatchet.LoweredBreaks(floors, configured);

        var violation = Assert.Single(violations);
        Assert.Contains("clarity", violation);
        Assert.Contains("70", violation);
        Assert.Contains("76.36", violation);
    }

    [Fact]
    public void A_module_with_no_recorded_floor_yet_is_never_a_violation()
    {
        var floors = new Dictionary<string, MutationRatchet.Floor>();
        var configured = new Dictionary<string, double> { ["brand-new-module"] = 10.0 };

        Assert.Empty(MutationRatchet.LoweredBreaks(floors, configured));
    }

    [Fact]
    public void Multiple_violations_are_all_reported_sorted()
    {
        var floors = new Dictionary<string, MutationRatchet.Floor>
        {
            ["proxy"] = new("proxy", 78.20),
            ["core"] = new("core", 89.95),
        };
        var configured = new Dictionary<string, double> { ["proxy"] = 50.0, ["core"] = 50.0 };

        var violations = MutationRatchet.LoweredBreaks(floors, configured);

        Assert.Equal(2, violations.Count);
        Assert.Equal(violations.OrderBy(v => v, StringComparer.Ordinal).ToList(), violations);
    }

    /// <summary>
    /// The real repo, wired end to end: every module <c>mutation-ratchet.json</c> records must
    /// have a live <c>stryker-config*.json</c> whose <c>break</c> is at or above that recorded
    /// floor. Reads both real files fresh, so this is the test that fails the moment a future
    /// edit lowers a break without also raising (or at least re-recording) the floor.
    /// </summary>
    [Fact]
    public void No_real_stryker_config_break_is_below_its_recorded_floor()
    {
        var root = RepositoryPath.Root();
        var floors = MutationRatchet.ReadFloors(Path.Combine(root, "mutation-ratchet.json"));

        var configuredBreaks = MutationAccounting.ConfigFiles(root)
            .ToDictionary(MutationAccounting.ModuleName, MutationRatchet.ConfiguredBreak);

        Assert.NotEmpty(floors);

        var violations = MutationRatchet.LoweredBreaks(floors, configuredBreaks);

        Assert.Empty(violations);
    }
}
