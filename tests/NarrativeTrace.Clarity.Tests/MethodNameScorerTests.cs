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

    // ---- ScoreTokenCount boundary (2 <= count <= 4 is the flat 1.0 plateau) --------------------
    //
    // Every name below starts with the domain verb "submit" (no verb suffix) and pads with
    // filler tokens ("Alpha", "Beta", ...) that are neither generic/vague/typed-generic words
    // nor abbreviations — so every scoring term except token count is held constant, and the
    // final score isolates the plateau's exact shape: flat at 0.97 across 2-4 tokens, then the
    // declining Math.Max(0.0, 0.7 - 0.15 * (count - 4)) formula beyond it. Exact expected values
    // are the real, unmutated Score() output (see commit note) — this is a specification of the
    // formula's shape, not an assertion on any particular mutant's replacement text.

    [Theory]
    [InlineData("submitAlpha", 0.97)]
    [InlineData("submitAlphaBeta", 0.97)]
    [InlineData("submitAlphaBetaGamma", 0.97)]
    public void Token_count_within_the_two_to_four_plateau_scores_flat(string name, double expected)
    {
        Assert.Equal(expected, MethodNameScorer.Score(name), precision: 9);
    }

    [Fact]
    public void Five_tokens_scores_below_the_plateau()
    {
        Assert.Equal(0.9025, MethodNameScorer.Score("submitAlphaBetaGammaDelta"), precision: 9);
    }

    [Fact]
    public void Six_tokens_scores_lower_still_than_five()
    {
        var fiveTokens = MethodNameScorer.Score("submitAlphaBetaGammaDelta");
        var sixTokens = MethodNameScorer.Score("submitAlphaBetaGammaDeltaEpsilon");

        Assert.Equal(0.88, sixTokens, precision: 9);
        Assert.True(sixTokens < fiveTokens, $"six={sixTokens} should be below five={fiveTokens}");
    }

    // ---- ScoreMorphology / HasVerbSuffix ------------------------------------------------------
    //
    // Isolates the four branches of ScoreMorphology (in-dictionary x has-suffix) and the
    // early-return for VerbCategory.Generic, by pairing a first token against the same filler
    // ("Alpha") used above.

    [Fact]
    public void Dictionary_verb_with_a_recognized_suffix_scores_the_morphology_maximum()
    {
        // "activate" is a domain verb ending in the "ate" suffix: in-dictionary AND has-suffix.
        Assert.Equal(1.0, MethodNameScorer.Score("activateAlpha"), precision: 9);
    }

    [Fact]
    public void Unrecognized_verb_with_a_suffix_still_credits_the_suffix()
    {
        // "widgetize" is not in any verb dictionary but ends in "ize": has-suffix without
        // in-dictionary.
        Assert.Equal(0.79, MethodNameScorer.Score("widgetizeAlpha"), precision: 9);
    }

    [Fact]
    public void Unrecognized_verb_without_a_suffix_scores_the_morphology_minimum()
    {
        // "widget" is neither in a verb dictionary nor suffixed: neither in-dictionary nor
        // has-suffix.
        Assert.Equal(0.625, MethodNameScorer.Score("widgetAlpha"), precision: 9);
    }

    [Fact]
    public void Generic_verb_short_circuits_to_the_morphology_minimum_even_without_checking_suffix()
    {
        // "process" is a GENERIC verb (in-dictionary) with no suffix — ScoreMorphology's
        // early-return for VerbCategory.Generic must fire before the suffix check, landing on
        // the same 0.3 a non-dictionary, non-suffixed word gets, not the 0.8 an in-dictionary
        // word without a suffix would otherwise get.
        Assert.Equal(0.625, MethodNameScorer.Score("processAlpha"), precision: 9);
    }

    [Theory]
    [InlineData("ateAlpha")] // "ate" alone is exactly as long as the "ate" suffix — no stem
    [InlineData("fenAlpha")] // "fen" is 3 letters, below HasVerbSuffix's length-4 floor
    public void A_token_no_longer_than_its_matching_suffix_does_not_count_as_suffixed(string name)
    {
        Assert.Equal(0.625, MethodNameScorer.Score(name), precision: 9);
    }

    // ---- ScoreSingleToken's own morphology fallback (independent of HasVerbSuffix) -------------

    [Fact]
    public void Single_token_unknown_verb_recognized_as_a_verb_by_morphology_scores_higher()
    {
        var recognizedAsVerb = MethodNameScorer.Score("widgetize");
        var notRecognizedAsVerb = MethodNameScorer.Score("widget");

        Assert.Equal(0.55, recognizedAsVerb, precision: 9);
        Assert.Equal(0.5, notRecognizedAsVerb, precision: 9);
    }
}
