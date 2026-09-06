// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class VerbDictionaryTests
{
    [Theory]
    [InlineData("accrue", VerbCategory.Domain)]
    [InlineData("authenticate", VerbCategory.Domain)]
    [InlineData("titrate", VerbCategory.Domain)]
    [InlineData("airdrop", VerbCategory.Domain)]
    [InlineData("diagnose", VerbCategory.Domain)]
    [InlineData("defibrillate", VerbCategory.Domain)]
    public void Domain_verbs_classified_correctly(
        string verb, VerbCategory expected)
    {
        Assert.Equal(expected, VerbDictionary.Classify(verb));
    }

    [Theory]
    [InlineData("create", VerbCategory.Standard)]
    [InlineData("find", VerbCategory.Standard)]
    [InlineData("place", VerbCategory.Standard)]
    public void Standard_verbs_classified_correctly(
        string verb, VerbCategory expected)
    {
        Assert.Equal(expected, VerbDictionary.Classify(verb));
    }

    [Theory]
    [InlineData("is", VerbCategory.Boolean)]
    [InlineData("has", VerbCategory.Boolean)]
    [InlineData("contains", VerbCategory.Boolean)]
    public void Boolean_verbs_classified_correctly(
        string verb, VerbCategory expected)
    {
        Assert.Equal(expected, VerbDictionary.Classify(verb));
    }

    [Theory]
    [InlineData("get", VerbCategory.Generic)]
    [InlineData("set", VerbCategory.Generic)]
    [InlineData("handle", VerbCategory.Generic)]
    [InlineData("process", VerbCategory.Generic)]
    public void Generic_verbs_classified_correctly(
        string verb, VerbCategory expected)
    {
        Assert.Equal(expected, VerbDictionary.Classify(verb));
    }

    [Fact]
    public void Unknown_word_returns_unknown()
    {
        Assert.Equal(
            VerbCategory.Unknown,
            VerbDictionary.Classify("xyzzy"));
    }

    [Fact]
    public void Classification_is_case_insensitive()
    {
        Assert.Equal(
            VerbCategory.Domain,
            VerbDictionary.Classify("Accrue"));
    }

    // --- Size-floor drift guards (CLARITY-1/-14): the ported dictionary must not
    // silently shrink below the Java reference volume. ---

    [Fact]
    public void Domain_vocabulary_meets_the_volume_floor()
    {
        Assert.True(
            VerbDictionary.DomainVerbs.Count >= 820,
            $"domain verbs shrank to {VerbDictionary.DomainVerbs.Count}");
    }

    [Fact]
    public void Total_vocabulary_meets_the_volume_floor()
    {
        var total = VerbDictionary.DomainVerbs
            .Concat(VerbDictionary.StandardVerbs)
            .Concat(VerbDictionary.GenericVerbs)
            .Concat(VerbDictionary.BooleanPrefixes)
            .Distinct()
            .Count();

        Assert.True(total >= 1040, $"total unique verbs shrank to {total}");
    }

    [Fact]
    public void Standard_generic_and_boolean_sets_meet_their_floors()
    {
        Assert.True(VerbDictionary.StandardVerbs.Count >= 190);
        Assert.Equal(21, VerbDictionary.GenericVerbs.Count);
        Assert.Equal(17, VerbDictionary.BooleanPrefixes.Count);
    }

    // --- Overlap invariants (CLARITY-14): mirrors Java's static overlap check so a
    // future edit cannot move a generic/boolean verb into the domain set (which
    // would change Classify results). ---

    [Fact]
    public void Generic_verbs_do_not_overlap_domain_verbs()
    {
        Assert.Empty(
            VerbDictionary.GenericVerbs.Intersect(VerbDictionary.DomainVerbs));
    }

    [Fact]
    public void Boolean_prefixes_do_not_overlap_domain_verbs()
    {
        Assert.Empty(
            VerbDictionary.BooleanPrefixes.Intersect(VerbDictionary.DomainVerbs));
    }
}
