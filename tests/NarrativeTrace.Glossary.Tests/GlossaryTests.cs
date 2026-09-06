// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryTests
{
    private static readonly DateTime FirstSeen = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Terms_are_canonicalized_to_context_then_term_order()
    {
        var glossary = new Glossary(
            1,
            Contexts("billing", "sales"),
            [Term("refund", "sales"), Term("overdraft", "billing"), Term("charge", "billing")]);

        Assert.Equal(
            ["charge", "overdraft", "refund"],
            glossary.Terms.Select(t => t.Term).ToArray());
    }

    [Fact]
    public void Empty_glossary_is_valid()
    {
        var glossary = new Glossary(1, new Dictionary<string, BoundedContext>(), []);

        Assert.Empty(glossary.Contexts);
        Assert.Empty(glossary.Terms);
        Assert.True(glossary.Invariant());
    }

    [Fact]
    public void Schema_version_below_one_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => new Glossary(0, new Dictionary<string, BoundedContext>(), []));
    }

    [Fact]
    public void Duplicate_term_key_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Glossary(
            1, Contexts("billing"), [Term("charge", "billing"), Term("charge", "billing")]));

        Assert.Contains("duplicate term key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_term_in_two_contexts_is_allowed()
    {
        var glossary = new Glossary(
            1,
            Contexts("billing", "sales"),
            [Term("policy", "billing"), Term("policy", "sales")]);

        Assert.Equal(2, glossary.Terms.Count);
    }

    [Fact]
    public void Term_referencing_undeclared_context_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Glossary(1, Contexts("billing"), [Term("refund", "sales")]));

        Assert.Contains("undeclared context 'sales'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Alias_equal_to_a_canonical_term_in_same_context_is_rejected()
    {
        var canonical = Term("overdraft account", "billing");
        var aliased = new GlossaryTerm(
            "charge", "billing", TermKind.Word, TermStatus.Curated,
            null, new Dictionary<string, string>(),
            [new SynonymAlias("overdraft account")], [], FirstSeen);

        var ex = Assert.Throws<ArgumentException>(
            () => new Glossary(1, Contexts("billing"), [canonical, aliased]));

        Assert.Contains("equals a canonical term", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Alias_equal_to_a_term_in_another_context_is_allowed()
    {
        var canonical = Term("overdraft account", "billing");
        var aliased = new GlossaryTerm(
            "charge", "sales", TermKind.Word, TermStatus.Curated,
            null, new Dictionary<string, string>(),
            [new SynonymAlias("overdraft account")], [], FirstSeen);

        var glossary = new Glossary(
            1, Contexts("billing", "sales"), [canonical, aliased]);

        Assert.True(glossary.Invariant());
    }

    [Fact]
    public void Contexts_are_copied_defensively()
    {
        var contexts = new Dictionary<string, BoundedContext>
        {
            ["billing"] = new("billing", []),
        };
        var glossary = new Glossary(1, contexts, []);

        contexts["sales"] = new BoundedContext("sales", []);

        Assert.Single(glossary.Contexts);
    }

    [Fact]
    public void Null_contexts_or_terms_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Glossary(1, null!, []));
        Assert.Throws<ArgumentNullException>(
            () => new Glossary(1, new Dictionary<string, BoundedContext>(), null!));
    }

    private static Dictionary<string, BoundedContext> Contexts(params string[] names)
    {
        return names.ToDictionary(
            name => name, name => new BoundedContext(name, []), StringComparer.Ordinal);
    }

    private static GlossaryTerm Term(string term, string context)
    {
        return new GlossaryTerm(
            term, context, TermKind.NounPhrase, TermStatus.Harvested,
            null, new Dictionary<string, string>(), [], [], FirstSeen);
    }
}
