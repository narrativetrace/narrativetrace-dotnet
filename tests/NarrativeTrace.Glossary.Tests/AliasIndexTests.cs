// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class AliasIndexTests
{
    private static readonly GlossaryTerm OverdraftAccount = new(
        "overdraft account", "billing", TermKind.NounPhrase, TermStatus.Curated,
        null, new Dictionary<string, string>(),
        [new SynonymAlias("account with overdraft")], [],
        new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));

    private static readonly AliasIndex Index = AliasIndex.Of(new Glossary(
        1,
        new Dictionary<string, BoundedContext>
        {
            ["billing"] = new("billing", []),
            ["support"] = new("support", []),
        },
        [OverdraftAccount]));

    [Fact]
    public void Finds_canonical_term_for_alias_within_its_context()
    {
        var key = new TermKey("billing", "account with overdraft");

        Assert.True(Index.IsAlias(key));
        Assert.Same(OverdraftAccount, Index.CanonicalFor(key));
    }

    [Fact]
    public void Alias_matching_is_scoped_to_the_declaring_context()
    {
        var key = new TermKey("support", "account with overdraft");

        Assert.False(Index.IsAlias(key));
        Assert.Null(Index.CanonicalFor(key));
    }

    [Fact]
    public void Canonical_term_itself_is_not_an_alias()
    {
        Assert.False(Index.IsAlias(new TermKey("billing", "overdraft account")));
    }

    [Fact]
    public void Superstring_of_alias_phrase_does_not_match()
    {
        Assert.False(Index.IsAlias(new TermKey("billing", "account with overdraft protection")));
        Assert.False(Index.IsAlias(new TermKey("billing", "account with")));
    }

    [Fact]
    public void Rejects_nulls()
    {
        Assert.Throws<ArgumentNullException>(() => AliasIndex.Of(null!));
        Assert.Throws<ArgumentNullException>(() => Index.IsAlias(null!));
        Assert.Throws<ArgumentNullException>(() => Index.CanonicalFor(null!));
    }
}
