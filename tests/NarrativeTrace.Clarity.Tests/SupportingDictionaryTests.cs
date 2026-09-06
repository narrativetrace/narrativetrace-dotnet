// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class GenericTokenDetectorTests
{
    [Theory]
    [InlineData("customer", TokenTier.NotGeneric)]
    [InlineData("data", TokenTier.Vague)]
    [InlineData("foo", TokenTier.Meaningless)]
    [InlineData("x", TokenTier.Meaningless)]
    [InlineData("id", TokenTier.TypedGeneric)]
    [InlineData("manager", TokenTier.NotGeneric)]
    public void Classifies_tokens_by_tier(
        string token, TokenTier expected)
    {
        Assert.Equal(
            expected, GenericTokenDetector.Classify(token));
    }

    [Theory]
    [InlineData(TokenTier.NotGeneric, 1.0)]
    [InlineData(TokenTier.TypedGeneric, 0.5)]
    [InlineData(TokenTier.Vague, 0.2)]
    [InlineData(TokenTier.Meaningless, 0.0)]
    public void Scores_tiers_correctly(
        TokenTier tier, double expected)
    {
        Assert.Equal(expected, GenericTokenDetector.Score(tier));
    }
}

public class AbbreviationDictionaryTests
{
    [Fact]
    public void Universal_abbreviation_detected()
    {
        Assert.Equal(
            AbbreviationTier.Universal,
            AbbreviationDictionary.Classify("id"));
    }

    [Fact]
    public void Well_known_abbreviation_detected()
    {
        Assert.Equal(
            AbbreviationTier.WellKnown,
            AbbreviationDictionary.Classify("msg"));
    }

    [Fact]
    public void Ambiguous_abbreviation_detected()
    {
        Assert.Equal(
            AbbreviationTier.Ambiguous,
            AbbreviationDictionary.Classify("cust"));
    }

    [Theory]
    [InlineData("svc")]
    [InlineData("txn")]
    [InlineData("repo")]
    [InlineData("elem")]
    [InlineData("stmt")]
    public void Newly_ported_well_known_abbreviations_detected(string token)
    {
        Assert.Equal(
            AbbreviationTier.WellKnown,
            AbbreviationDictionary.Classify(token));
    }

    [Fact]
    public void Unknown_word_returns_null()
    {
        Assert.Null(
            AbbreviationDictionary.Classify("order"));
    }

    [Fact]
    public void Abbreviation_volume_meets_the_floor()
    {
        Assert.True(
            AbbreviationDictionary.Count >= 185,
            $"abbreviations shrank to {AbbreviationDictionary.Count}");
    }
}

public class MorphologyAnalyzerTests
{
    [Fact]
    public void Verb_detected()
    {
        Assert.Equal(
            PartOfSpeech.Verb,
            MorphologyAnalyzer.Analyze("place"));
    }

    [Fact]
    public void Adjective_detected()
    {
        Assert.Equal(
            PartOfSpeech.Adjective,
            MorphologyAnalyzer.Analyze("active"));
    }

    [Fact]
    public void Noun_detected()
    {
        Assert.Equal(
            PartOfSpeech.Noun,
            MorphologyAnalyzer.Analyze("location"));
    }
}

public class RoleSuffixDictionaryTests
{
    [Fact]
    public void Known_suffix_returns_verbs()
    {
        var verbs = RoleSuffixDictionary.ExpectedVerbs(
            "repository");

        Assert.NotNull(verbs);
        Assert.Contains("find", verbs!);
    }

    [Fact]
    public void Unknown_suffix_returns_null()
    {
        Assert.Null(
            RoleSuffixDictionary.ExpectedVerbs("widget"));
    }
}
