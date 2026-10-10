// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// The shared <c>feedback.json</c> replayed through this port's value-free gate. The ACCEPTED half is
/// asserted before the REJECTED half, deliberately: the accepted rows (an install coordinate, a
/// doctor finding id, a hyphenated sentence, a structural-artifact path at 3.96 bits per character)
/// are where a port discovers its rules are unusable, and a gate that refuses them is one an agent
/// learns to route around. Every row's verdict is read text-scoped through
/// <see cref="ValueFreeCheck.RulesRefusing"/>, so it never depends on which field a value arrived in.
/// </summary>
public sealed class FeedbackCorpusReplayTests
{
    public static IEnumerable<object[]> Accepted() =>
        FeedbackCorpus.Cases().Where(c => !c.IsRejected).Select(c => new object[] { c });

    public static IEnumerable<object[]> Rejected() =>
        FeedbackCorpus.Cases().Where(c => c.IsRejected).Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Accepted))]
    public void An_accepted_row_is_refused_by_no_rule(FeedbackCase corpusCase)
    {
        var refusing = ValueFreeCheck.RulesRefusing(corpusCase.Value).Select(r => r.Id).ToList();

        Assert.True(
            refusing.Count == 0,
            $"{corpusCase.Id} ({corpusCase.Description}) must be accepted, but {string.Join(", ", refusing)} refused it");
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void A_rejected_row_is_refused_by_the_rule_it_names(FeedbackCase corpusCase)
    {
        var refusing = ValueFreeCheck.RulesRefusing(corpusCase.Value).Select(r => r.Id).ToList();

        Assert.True(refusing.Count > 0, $"{corpusCase.Id} ({corpusCase.Description}) must be refused by some rule");
        Assert.True(
            corpusCase.Rule is not null && refusing.Contains(corpusCase.Rule),
            $"{corpusCase.Id}: expected {corpusCase.Rule} among the refusing rules, got {string.Join(", ", refusing)}");
    }

    [Fact]
    public void The_corpus_is_the_shape_the_master_declares()
    {
        var cases = FeedbackCorpus.Cases();

        Assert.Equal(77, cases.Count);
        Assert.Equal(28, cases.Count(c => !c.IsRejected));
        Assert.All(cases.Where(c => c.IsRejected), c => Assert.NotNull(c.Rule));
    }
}
