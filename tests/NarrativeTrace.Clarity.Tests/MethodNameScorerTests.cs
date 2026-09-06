// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class MethodNameScorerTests
{
    [Fact]
    public void Domain_verb_method_scores_high()
    {
        var score = MethodNameScorer.Score("placeOrder");
        Assert.True(score >= 0.7);
    }

    [Fact]
    public void Generic_verb_method_scores_lower_than_a_domain_one()
    {
        var generic = MethodNameScorer.Score("doStuff");
        var domain = MethodNameScorer.Score("submitPayment");

        Assert.True(
            generic < domain,
            $"generic={generic} should be below domain={domain}");
    }

    [Fact]
    public void Single_token_scores_lower()
    {
        var score = MethodNameScorer.Score("run");
        Assert.True(score < 0.5);
    }

    [Fact]
    public void Empty_method_name_scores_zero()
    {
        Assert.Equal(0.0, MethodNameScorer.Score(""));
    }

    [Fact]
    public void Well_named_method_scores_above_threshold()
    {
        var score = MethodNameScorer.Score(
            "submitPayment");
        Assert.True(score >= 0.7);
    }
}
