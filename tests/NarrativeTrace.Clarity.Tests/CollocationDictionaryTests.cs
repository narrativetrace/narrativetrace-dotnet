// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Linq;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class CollocationDictionaryTests
{
    // --- Drift guards (CLARITY): the merged lookup must keep every noun and every
    // noun->verb link the Java reference declares. A dropped entry, or a noun in
    // two domains silently overwriting one of them, moves these numbers. ---

    [Fact]
    public void Merged_lookup_keeps_every_noun_and_verb_link_of_the_reference()
    {
        var nouns = CollocationDictionary.Nouns;
        var links = nouns.Sum(
            noun => CollocationDictionary.PreferredVerbs(noun).Count);

        Assert.Equal(201, nouns.Count);
        Assert.Equal(1083, links);
    }

    [Fact]
    public void Every_noun_is_lowercase_and_resolves_to_a_non_empty_verb_set()
    {
        foreach (var noun in CollocationDictionary.Nouns)
        {
            Assert.Equal(noun.ToLowerInvariant(), noun);
            Assert.NotEmpty(CollocationDictionary.PreferredVerbs(noun));
        }
    }

    // --- Exact collocation sets ---

    [Fact]
    public void Account_collocates_with_its_finance_verbs()
    {
        string[] expected =
            ["balance", "close", "credit", "debit", "freeze", "reconcile"];

        Assert.Equal(expected, Sorted("account"));
    }

    [Fact]
    public void Order_collocates_with_its_ecommerce_verbs()
    {
        string[] expected =
            ["backorder", "cancel", "fulfill", "place", "return", "ship"];

        Assert.Equal(expected, Sorted("order"));
    }

    // --- Union across domains (CRITICAL): 14 nouns appear in more than one domain
    // and must keep the verbs of every domain, not just the last one merged. ---

    [Fact]
    public void Token_unions_the_security_and_blockchain_verbs()
    {
        string[] expected =
        [
            "blacklist", "burn", "invalidate", "issue", "lock", "mint",
            "refresh", "revoke", "rotate", "stake", "transfer", "vest",
        ];

        Assert.Equal(expected, Sorted("token"));
    }

    [Fact]
    public void Session_unions_the_telecom_and_security_verbs()
    {
        string[] expected =
        [
            "create", "extend", "handoff", "hijack", "invalidate", "originate",
            "terminate",
        ];

        Assert.Equal(expected, Sorted("session"));
    }

    [Fact]
    public void Field_unions_the_agriculture_and_programming_verbs()
    {
        string[] expected =
        [
            "fertilize", "harvest", "irrigate", "map", "plant", "read",
            "redact", "serialize", "validate", "write",
        ];

        Assert.Equal(expected, Sorted("field"));
    }

    [Theory]
    [InlineData("audience", 5, "exclude", "suppress")]
    [InlineData("case", 9, "adjudicate", "reopen")]
    [InlineData("container", 9, "transload", "orchestrate")]
    [InlineData("contract", 11, "breach", "deploy")]
    [InlineData("event", 8, "fanout", "enrich")]
    [InlineData("field", 10, "irrigate", "redact")]
    [InlineData("message", 9, "unsend", "deadletter")]
    [InlineData("node", 13, "cordon", "traverse")]
    [InlineData("policy", 13, "underwrite", "attest")]
    [InlineData("queue", 7, "matchmake", "purge")]
    [InlineData("schema", 8, "normalize", "infer")]
    [InlineData("session", 7, "handoff", "hijack")]
    [InlineData("tenant", 10, "evict", "offboard")]
    [InlineData("token", 12, "blacklist", "mint")]
    public void Multi_domain_nouns_keep_verbs_from_both_domains(
        string noun,
        int expectedCount,
        string fromFirstDomain,
        string fromSecondDomain)
    {
        var verbs = CollocationDictionary.PreferredVerbs(noun);

        Assert.Equal(expectedCount, verbs.Count);
        Assert.Contains(fromFirstDomain, verbs);
        Assert.Contains(fromSecondDomain, verbs);
    }

    // --- Case handling ---

    [Theory]
    [InlineData("ORDER")]
    [InlineData("Order")]
    [InlineData("oRdEr")]
    public void Lookup_is_case_insensitive(string noun)
    {
        Assert.Equal(Sorted("order"), Sorted(noun));
    }

    [Fact]
    public void Lookup_lowercases_invariantly_under_a_dotted_i_culture()
    {
        using var culture = new CultureScope("tr-TR");
        string[] expected = ["dispute", "issue", "settle", "void"];

        Assert.Equal(expected, Sorted("INVOICE"));
        Assert.True(CollocationDictionary.IsPreferred("ISSUE", "INVOICE"));
    }

    // --- Blank, padded and unknown inputs ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Blank_noun_returns_an_empty_collection(string? noun)
    {
        Assert.Empty(CollocationDictionary.PreferredVerbs(noun));
    }

    [Fact]
    public void Unknown_noun_returns_an_empty_collection()
    {
        Assert.Empty(CollocationDictionary.PreferredVerbs("xyzzy"));
    }

    [Fact]
    public void Padded_noun_is_not_trimmed_before_lookup()
    {
        Assert.Empty(CollocationDictionary.PreferredVerbs(" order "));
    }

    // --- IsPreferred ---

    [Theory]
    [InlineData("place", "order")]
    [InlineData("backorder", "order")]
    [InlineData("PLACE", "ORDER")]
    [InlineData("Debit", "Account")]
    [InlineData("mint", "token")]
    [InlineData("revoke", "token")]
    public void Preferred_collocations_are_recognised(string verb, string noun)
    {
        Assert.True(CollocationDictionary.IsPreferred(verb, noun));
    }

    [Theory]
    [InlineData("place", "account")]
    [InlineData("fulfill", "xyzzy")]
    [InlineData("xyzzy", "order")]
    [InlineData("ship", "")]
    [InlineData("ship", "   ")]
    [InlineData("ship", null)]
    public void Non_collocations_are_rejected(string verb, string? noun)
    {
        Assert.False(CollocationDictionary.IsPreferred(verb, noun));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Blank_verb_is_never_preferred(string? verb)
    {
        Assert.False(CollocationDictionary.IsPreferred(verb, "order"));
    }

    [Fact]
    public void Padded_verb_is_not_trimmed_before_matching()
    {
        Assert.False(CollocationDictionary.IsPreferred(" place ", "order"));
    }

    private static string[] Sorted(string? noun)
    {
        return CollocationDictionary.PreferredVerbs(noun)
            .OrderBy(verb => verb, StringComparer.Ordinal)
            .ToArray();
    }
}
