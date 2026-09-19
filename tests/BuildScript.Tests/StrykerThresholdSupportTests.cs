// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards Stryker.NET's own <c>thresholds.{high,low,break}</c> schema — <c>int?</c>, not
/// <c>double?</c> — after a ratchet wrote a measured score straight into <c>break</c>/<c>low</c>
/// as a float and Stryker refused to even parse the config (nightly 2026-09-19, B-40).
/// </summary>
public sealed class StrykerThresholdSupportTests
{
    [Fact]
    public void A_break_at_or_below_low_at_or_below_high_is_not_a_violation()
    {
        var violations = StrykerThresholdSupport.OrderingViolations(
            [new StrykerThresholdSupport.Thresholds("clarity", High: 85, Low: 76, Break: 76)]);

        Assert.Empty(violations);
    }

    [Fact]
    public void A_break_above_low_is_a_violation()
    {
        var violations = StrykerThresholdSupport.OrderingViolations(
            [new StrykerThresholdSupport.Thresholds("clarity", High: 85, Low: 76, Break: 80)]);

        var violation = Assert.Single(violations);
        Assert.Contains("break", violation);
        Assert.Contains("80", violation);
    }

    [Fact]
    public void A_low_above_high_is_a_violation()
    {
        var violations = StrykerThresholdSupport.OrderingViolations(
            [new StrykerThresholdSupport.Thresholds("clarity", High: 70, Low: 76, Break: 76)]);

        var violation = Assert.Single(violations);
        Assert.Contains("low", violation);
        Assert.Contains("high", violation);
    }

    [Fact]
    public void Multiple_violations_are_all_reported_sorted()
    {
        var violations = StrykerThresholdSupport.OrderingViolations(
        [
            new StrykerThresholdSupport.Thresholds("proxy", High: 90, Low: 78, Break: 80),
            new StrykerThresholdSupport.Thresholds("clarity", High: 85, Low: 76, Break: 77),
        ]);

        Assert.Equal(2, violations.Count);
        Assert.Equal(violations.OrderBy(v => v, StringComparer.Ordinal).ToList(), violations);
    }

    /// <summary>
    /// The real repo, wired end to end: every live <c>stryker-config*.json</c>'s thresholds must
    /// parse as integers (Stryker's own schema) and must never have <c>break</c> above <c>low</c>
    /// or <c>low</c> above <c>high</c>. Reads the real files fresh, so this is the test that fails
    /// the moment a future ratchet writes a measured score in as a float again.
    /// </summary>
    [Fact]
    public void No_real_stryker_config_carries_a_non_integer_or_out_of_order_threshold()
    {
        var root = RepositoryPath.Root();
        var configs = MutationAccounting.ConfigFiles(root);
        Assert.NotEmpty(configs);

        var nonInteger = configs
            .SelectMany(config => StrykerThresholdSupport.NonIntegerThresholds(
                config, MutationAccounting.ModuleName(config)))
            .ToList();
        Assert.Empty(nonInteger);

        var thresholds = configs
            .Select(config => StrykerThresholdSupport.ReadIntegerThresholds(
                config, MutationAccounting.ModuleName(config)))
            .Where(t => t is not null)
            .Select(t => t!.Value)
            .ToList();

        var ordering = StrykerThresholdSupport.OrderingViolations(thresholds);
        Assert.Empty(ordering);
    }
}
