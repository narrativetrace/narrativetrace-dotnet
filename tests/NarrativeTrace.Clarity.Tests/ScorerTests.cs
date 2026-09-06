// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClassNameScorerTests
{
    [Fact]
    public void Well_named_class_scores_high()
    {
        var score = ClassNameScorer.Score("OrderRepository");
        Assert.True(score >= 0.7);
    }

    [Fact]
    public void Generic_class_scores_lower()
    {
        var score = ClassNameScorer.Score("DataManager");
        Assert.True(score < 0.7);
    }

    [Fact]
    public void Empty_class_name_scores_zero()
    {
        Assert.Equal(0.0, ClassNameScorer.Score(""));
    }
}

public class ParameterNameScorerTests
{
    [Fact]
    public void Descriptive_parameter_scores_high()
    {
        var score = ParameterNameScorer.Score("orderId");
        Assert.True(score >= 0.5);
    }

    [Fact]
    public void Single_char_parameter_scores_low()
    {
        var score = ParameterNameScorer.Score("x");
        Assert.True(
            score < 0.6,
            $"Expected < 0.6 but got {score}");
    }

    [Fact]
    public void Empty_parameter_scores_zero()
    {
        Assert.Equal(0.0, ParameterNameScorer.Score(""));
    }

    [Fact]
    public void Ambiguous_abbreviation_lowers_the_parameter_score()
    {
        // "cust" is an ambiguous abbreviation — the revived penalty must
        // score it below the spelled-out name.
        var ambiguous = ParameterNameScorer.Score("custId");
        var clear = ParameterNameScorer.Score("customerId");

        Assert.True(
            ambiguous < clear,
            $"ambiguous={ambiguous} should be below clear={clear}");
    }
}

public class StructuralScorerTests
{
    [Fact]
    public void Few_params_shallow_depth_scores_high()
    {
        var score = StructuralScorer.Score(2, 1);
        Assert.True(score >= 0.9);
    }

    [Fact]
    public void Many_params_deep_nesting_scores_low()
    {
        var score = StructuralScorer.Score(8, 10);
        Assert.True(score <= 0.4);
    }

    [Fact]
    public void Zero_params_scores_perfectly()
    {
        var score = StructuralScorer.Score(0, 0);
        Assert.Equal(1.0, score);
    }
}

public class CohesionScorerTests
{
    [Fact]
    public void Consistent_verbs_score_high()
    {
        var score = CohesionScorer.ScoreClass(
            "OrderRepository",
            ["findById", "findAll", "findByName"]);
        Assert.True(score >= 0.7);
    }

    [Fact]
    public void Mixed_verbs_score_lower()
    {
        var score = CohesionScorer.ScoreClass(
            "OrderRepository",
            ["placeOrder", "getData", "handleRequest"]);
        Assert.True(
            score < 0.8,
            $"Expected < 0.8 but got {score}");
    }

    [Fact]
    public void Aligned_single_method_scores_perfectly()
    {
        var score = CohesionScorer.ScoreClass(
            "OrderRepository", ["findById"]);
        Assert.Equal(1.0, score);
    }
}
