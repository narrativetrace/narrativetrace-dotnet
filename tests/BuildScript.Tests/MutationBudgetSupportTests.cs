// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="MutationBudgetSupport.SelectBudgets"/> — the water-filling allocation that
/// turns per-module weights into wall-clock budgets, always summing to the ceiling (floor
/// permitting) so <c>Mutation</c> can bound every module's subprocess instead of running
/// unbounded. See the class remarks for why no real "last successful run" duration exists to
/// weight by instead.
/// </summary>
public sealed class MutationBudgetSupportTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(100);
    private static readonly TimeSpan Floor = TimeSpan.FromMinutes(10);

    [Fact]
    public void Empty_weights_produce_no_budgets()
    {
        var budgets = MutationBudgetSupport.SelectBudgets(
            new Dictionary<string, int>(), Ceiling, Floor);

        Assert.Empty(budgets);
    }

    [Fact]
    public void Equal_weights_split_the_ceiling_equally()
    {
        var weights = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1, ["d"] = 1 };

        var budgets = MutationBudgetSupport.SelectBudgets(weights, Ceiling, Floor);

        Assert.Equal(TimeSpan.FromMinutes(25), budgets["a"]);
        Assert.Equal(TimeSpan.FromMinutes(25), budgets["b"]);
        Assert.Equal(TimeSpan.FromMinutes(25), budgets["c"]);
        Assert.Equal(TimeSpan.FromMinutes(25), budgets["d"]);
    }

    [Fact]
    public void Zero_weight_modules_split_the_ceiling_equally_instead_of_dividing_by_zero()
    {
        var weights = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };

        var budgets = MutationBudgetSupport.SelectBudgets(weights, Ceiling, Floor);

        Assert.Equal(TimeSpan.FromMinutes(50), budgets["a"]);
        Assert.Equal(TimeSpan.FromMinutes(50), budgets["b"]);
    }

    [Fact]
    public void Proportional_shares_match_each_modules_fraction_of_total_weight()
    {
        // 1:3 weight ratio, both comfortably above the floor.
        var weights = new Dictionary<string, int> { ["small"] = 25, ["big"] = 75 };

        var budgets = MutationBudgetSupport.SelectBudgets(weights, Ceiling, Floor);

        Assert.Equal(TimeSpan.FromMinutes(25), budgets["small"]);
        Assert.Equal(TimeSpan.FromMinutes(75), budgets["big"]);
    }

    [Fact]
    public void A_module_under_the_floor_is_raised_to_it_and_the_rest_absorb_the_difference()
    {
        // "tiny" would get 1% of 100min = 1min unweighted, well under the 10min floor.
        var weights = new Dictionary<string, int> { ["tiny"] = 1, ["huge"] = 99 };

        var budgets = MutationBudgetSupport.SelectBudgets(weights, Ceiling, Floor);

        Assert.Equal(TimeSpan.FromMinutes(10), budgets["tiny"]);
        Assert.Equal(TimeSpan.FromMinutes(90), budgets["huge"]);
    }

    [Fact]
    public void Flooring_one_module_can_push_a_second_below_the_floor_only_on_a_later_pass()
    {
        // a=1 floors immediately (round-1 share 100*1/102 ~ 0.98min). b=11's round-1 share is
        // 100*11/102 ~ 10.78min — just ABOVE the 10min floor, so a single-pass allocation would
        // leave it alone. But flooring a takes 10min out of the pool (far more than its ~0.98min
        // natural share), shrinking what is left for b and c — b's round-2 share against that
        // smaller pool (90min, weight-sum 101) is 90*11/101 ~ 9.80min, now under the floor too.
        // This is exactly the cascade the iteration must keep re-checking for.
        var weights = new Dictionary<string, int> { ["a"] = 1, ["b"] = 11, ["c"] = 90 };

        var budgets = MutationBudgetSupport.SelectBudgets(weights, Ceiling, Floor);

        Assert.Equal(TimeSpan.FromMinutes(10), budgets["a"]);
        Assert.Equal(TimeSpan.FromMinutes(10), budgets["b"]);
        Assert.Equal(TimeSpan.FromMinutes(80), budgets["c"]);
        var total = budgets.Values.Aggregate(TimeSpan.Zero, (sum, span) => sum + span);
        Assert.Equal(Ceiling, total);
    }

    [Fact]
    public void Every_module_gets_at_least_the_floor_even_when_floors_alone_exceed_the_ceiling()
    {
        // 6 modules x 10min floor = 60min > a 50min ceiling: the floor promise still wins per
        // module, so the total legitimately exceeds the ceiling — documented, not silently capped.
        var weights = Enumerable.Range(1, 6).ToDictionary(i => $"m{i}", i => 1);
        var smallCeiling = TimeSpan.FromMinutes(50);

        var budgets = MutationBudgetSupport.SelectBudgets(weights, smallCeiling, Floor);

        Assert.All(budgets.Values, budget => Assert.Equal(Floor, budget));
        var total = budgets.Values.Aggregate(TimeSpan.Zero, (sum, span) => sum + span);
        Assert.Equal(TimeSpan.FromMinutes(60), total);
    }

    [Fact]
    public void Budgets_sum_to_the_ceiling_when_the_floor_fits()
    {
        var budgets = MutationBudgetSupport.SelectBudgets(
            MutationBudgetSupport.ProductionWeights,
            MutationBudgetSupport.ProductionCeiling,
            MutationBudgetSupport.ProductionFloor);

        var total = budgets.Values.Aggregate(TimeSpan.Zero, (sum, span) => sum + span);
        Assert.True(
            Math.Abs((total - MutationBudgetSupport.ProductionCeiling).TotalSeconds) < 1.0,
            $"expected ~{MutationBudgetSupport.ProductionCeiling}, got {total}");
    }

    [Fact]
    public void Every_production_module_has_a_budget_and_none_is_under_the_floor()
    {
        var budgets = MutationBudgetSupport.SelectBudgets(
            MutationBudgetSupport.ProductionWeights,
            MutationBudgetSupport.ProductionCeiling,
            MutationBudgetSupport.ProductionFloor);

        Assert.Equal(MutationBudgetSupport.ProductionWeights.Count, budgets.Count);
        Assert.All(budgets.Values, budget => Assert.True(budget >= MutationBudgetSupport.ProductionFloor));
    }
}
