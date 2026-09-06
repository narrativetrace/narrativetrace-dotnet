// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClarityIssueFinderTests
{
    [Fact]
    public void Flags_a_weak_method_name_with_score_derived_severity()
    {
        // A bare generic verb scores 0.10, which lands in the High band.
        var issues = ClarityIssueFinder.Find([Node("OrderService", "run")]);

        var issue = Assert.Single(issues, i => i.Category == "method-name");
        Assert.Equal("OrderService.run", issue.Element);
        Assert.Equal(ClaritySeverity.High, issue.Severity);
        Assert.Contains("domain-specific verb+noun", issue.Suggestion, StringComparison.Ordinal);
    }

    [Fact]
    public void A_weak_property_name_is_flagged_as_a_property_not_a_method()
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "data" };

        var issues = ClarityIssueFinder.Find([Node("Expense", "data")], names);

        var issue = Assert.Single(issues, i => i.Category == "property-name");
        Assert.Equal("Expense.data", issue.Element);
        Assert.Contains(
            "domain concept", issue.Suggestion, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, i => i.Category == "method-name");
    }

    [Fact]
    public void A_well_named_property_raises_no_issue()
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            "description",
        };

        var issues = ClarityIssueFinder.Find(
            [Node("Expense", "description")], names);

        Assert.DoesNotContain(
            issues, i => i.Element == "Expense.description");
    }

    [Fact]
    public void A_property_name_raises_no_collocation_issue()
    {
        // A bare noun has no verb+noun pairing to check, so the collocation
        // rubric does not apply to it at all.
        var names = new HashSet<string>(StringComparer.Ordinal) { "getOrder" };

        var issues = ClarityIssueFinder.Find(
            [Node("Expense", "getOrder")], names);

        Assert.DoesNotContain(issues, i => i.Category == "collocation");
    }

    [Fact]
    public void A_generic_verb_with_a_domain_noun_is_no_longer_a_name_issue()
    {
        // handleRequest scores 0.625 — above the threshold, so the name itself
        // is acceptable even though "handle" is a generic verb. Only the
        // collocation advice remains.
        var issues = ClarityIssueFinder.Find([Node("OrderService", "handleRequest")]);

        Assert.DoesNotContain(issues, i => i.Category == "method-name");
        Assert.Contains(issues, i => i.Category == "collocation");
    }

    [Fact]
    public void Leaves_a_well_named_method_alone()
    {
        var issues = ClarityIssueFinder.Find([Node("OrderService", "placeOrder")]);

        Assert.DoesNotContain(issues, i => i.Category == "method-name");
    }

    [Fact]
    public void Flags_a_weak_class_name_once_however_many_methods_it_owns()
    {
        var issues = ClarityIssueFinder.Find(
        [
            Node("Manager", "placeOrder"),
            Node("Manager", "shipOrder"),
            Node("Manager", "cancelOrder"),
        ]);

        var issue = Assert.Single(issues, i => i.Category == "class-name");
        Assert.Equal("Manager", issue.Element);
        Assert.Equal(1, issue.Occurrences);
    }

    [Fact]
    public void Flags_weak_parameter_names()
    {
        var issues = ClarityIssueFinder.Find(
            [Node("OrderService", "placeOrder", "x")]);

        var issue = Assert.Single(issues, i => i.Category == "param-name");
        Assert.Equal("x", issue.Element);
        Assert.Contains("customerId", issue.Suggestion, StringComparison.Ordinal);
    }

    [Fact]
    public void Flags_a_non_idiomatic_verb_for_a_known_noun()
    {
        var issues = ClarityIssueFinder.Find([Node("OrderService", "makeOrder")]);

        var issue = Assert.Single(issues, i => i.Category == "collocation");
        Assert.Equal("OrderService.makeOrder", issue.Element);
        Assert.Equal(ClaritySeverity.Low, issue.Severity);
        // Preferred verbs for "order", alphabetical, suffixed with the noun.
        Assert.Equal(
            "Consider: backorderOrder, cancelOrder, fulfillOrder, placeOrder, "
            + "returnOrder, shipOrder",
            issue.Suggestion);
    }

    [Fact]
    public void Accepts_an_idiomatic_verb_and_unknown_nouns()
    {
        Assert.DoesNotContain(
            ClarityIssueFinder.Find([Node("OrderService", "placeOrder")]),
            i => i.Category == "collocation");
        Assert.DoesNotContain(
            ClarityIssueFinder.Find([Node("OrderService", "placeWidget")]),
            i => i.Category == "collocation");
        Assert.DoesNotContain(
            ClarityIssueFinder.Find([Node("OrderService", "run")]),
            i => i.Category == "collocation");
    }

    [Fact]
    public void Repeats_of_one_finding_collapse_into_occurrences()
    {
        var issues = ClarityIssueFinder.Find(
        [
            Node("OrderService", "placeOrder", "x"),
            Node("OrderService", "shipOrder", "x"),
            Node("OrderService", "cancelOrder", "x"),
        ]);

        var issue = Assert.Single(issues, i => i.Category == "param-name");
        Assert.Equal(3, issue.Occurrences);
        Assert.Equal(issue.Severity.Weight() * 3.0, issue.ImpactScore);
    }

    [Fact]
    public void Issues_are_ranked_by_impact_score()
    {
        var issues = ClarityIssueFinder.Find(
        [
            Node("OrderService", "placeOrder", "x"),
            Node("OrderService", "shipOrder", "x"),
            Node("OrderService", "cancelOrder", "x"),
            Node("Manager", "run"),
        ]);

        Assert.NotEmpty(issues);
        var scores = issues.Select(i => i.ImpactScore).ToList();
        Assert.Equal(scores.OrderByDescending(s => s).ToList(), scores);
    }

    [Fact]
    public void Walks_only_the_nodes_it_is_given()
    {
        Assert.Empty(ClarityIssueFinder.Find([]));
        Assert.Throws<ArgumentNullException>(() => ClarityIssueFinder.Find(null!));
    }

    [Fact]
    public void Synthetic_names_do_not_crash_the_finder()
    {
        var issues = ClarityIssueFinder.Find([Node("<launcher>", "<fork>", "")]);

        Assert.All(issues, issue => Assert.False(string.IsNullOrWhiteSpace(issue.Element)));
    }

    private static TraceNode Node(
        string className, string methodName, params string[] parameterNames)
    {
        var parameters = parameterNames
            .Select(name => new ParameterCapture(name, "\"v\"", false))
            .ToArray();
        return new TraceNode(
            new MethodSignature(className, methodName, parameters),
            new Returned(null),
            [],
            0);
    }
}
