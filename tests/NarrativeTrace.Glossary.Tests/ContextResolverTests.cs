// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class ContextResolverTests
{
    [Fact]
    public void Resolves_namespace_to_declared_context()
    {
        var resolver = Resolver(
            new BoundedContext("billing", ["Acme.Billing"]),
            new BoundedContext("support", ["Acme.Support"]));

        Assert.Equal("billing", resolver.Resolve("Acme.Billing"));
        Assert.Equal("billing", resolver.Resolve("Acme.Billing.Overdraft"));
        Assert.Equal("support", resolver.Resolve("Acme.Support"));
    }

    [Fact]
    public void Falls_back_to_unassigned_when_no_prefix_matches()
    {
        var resolver = Resolver(new BoundedContext("billing", ["Acme.Billing"]));

        Assert.Equal(ContextResolver.Unassigned, resolver.Resolve("Other.Shop"));
        Assert.Equal(ContextResolver.Unassigned, resolver.Resolve(""));
    }

    [Fact]
    public void Rejects_sibling_namespace_sharing_prefix_without_delimiter()
    {
        var resolver = Resolver(new BoundedContext("billing", ["Acme.Billing"]));

        Assert.Equal(ContextResolver.Unassigned, resolver.Resolve("Acme.Billingx"));
        Assert.Equal(ContextResolver.Unassigned, resolver.Resolve("Acme.Billingx.Core"));
        Assert.Equal(ContextResolver.Unassigned, resolver.Resolve("Acme.Billin"));
    }

    [Fact]
    public void Longest_matching_prefix_wins_for_nested_contexts()
    {
        var resolver = Resolver(
            new BoundedContext("billing", ["Acme.Billing"]),
            new BoundedContext("collections", ["Acme.Billing.Collections"]));

        Assert.Equal("collections", resolver.Resolve("Acme.Billing.Collections.Dunning"));
        Assert.Equal("billing", resolver.Resolve("Acme.Billing.Invoice"));
    }

    [Fact]
    public void Equal_length_prefix_tie_resolves_to_alphabetically_first_context()
    {
        var resolver = Resolver(
            new BoundedContext("zebra", ["Acme.Billing"]),
            new BoundedContext("alpha", ["Acme.Billing"]));

        Assert.Equal("alpha", resolver.Resolve("Acme.Billing.Core"));
    }

    [Fact]
    public void Rejects_null_namespace()
    {
        Assert.Throws<ArgumentNullException>(() => Resolver().Resolve(null!));
    }

    [Fact]
    public void Rejects_null_glossary()
    {
        Assert.Throws<ArgumentNullException>(() => new ContextResolver(null!));
    }

    private static ContextResolver Resolver(params BoundedContext[] contexts)
    {
        return new ContextResolver(new Glossary(
            1,
            contexts.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal),
            []));
    }
}
