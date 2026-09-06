// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.DependencyInjection;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

public class NamespaceMatcherTests
{
    [Theory]
    [InlineData("MyApp", true)]            // exact
    [InlineData("MyApp.Impl", true)]       // sub-namespace
    [InlineData("MyApp.Impl.Deep", true)]  // deep sub-namespace
    [InlineData("MyApp2", false)]          // sibling with common prefix
    [InlineData("MyAppImpl", false)]       // no dot boundary
    [InlineData("Other", false)]           // unrelated
    [InlineData("My", false)]              // shorter prefix
    public void Matches_uses_dot_boundary_semantics(
        string ns, bool expected)
    {
        var matcher = new NamespaceMatcher(["MyApp"]);

        Assert.Equal(expected, matcher.Matches(ns));
    }

    [Fact]
    public void Matches_any_of_multiple_base_namespaces()
    {
        var matcher = new NamespaceMatcher(["Alpha", "Beta.Core"]);

        Assert.True(matcher.Matches("Alpha.Svc"));
        Assert.True(matcher.Matches("Beta.Core.Impl"));
        Assert.False(matcher.Matches("Gamma"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_or_null_namespace_never_matches(string? ns)
    {
        var matcher = new NamespaceMatcher(["MyApp"]);

        Assert.False(matcher.Matches(ns));
    }

    [Fact]
    public void Empty_base_list_matches_nothing()
    {
        var matcher = new NamespaceMatcher([]);

        Assert.False(matcher.Matches("MyApp.Svc"));
    }
}
