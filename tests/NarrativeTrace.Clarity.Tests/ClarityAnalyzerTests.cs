// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class ClarityAnalyzerTests
{
    [Fact]
    public void Well_named_code_scores_high()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "placeOrder",
                    [new ParameterCapture("orderId", "42", false)]),
                new Returned(null), [], 0),
        ]);

        var result = ClarityAnalyzer.Analyze(tree);

        Assert.True(
            result.Overall >= 0.6,
            $"Expected >= 0.6 but got {result.Overall}");
    }

    [Fact]
    public void Generic_naming_scores_low()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("DataManager", "handleStuff",
                    [new ParameterCapture("d", "x", false)]),
                new Returned(null), [], 0),
        ]);

        var result = ClarityAnalyzer.Analyze(tree);

        Assert.True(
            result.Overall < 0.6,
            $"Expected < 0.6 but got {result.Overall}");
    }

    [Fact]
    public void Issues_generated_for_generic_verbs()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "run", []),
                new Returned(null), [], 0),
        ]);

        var result = ClarityAnalyzer.Analyze(tree);

        Assert.NotEmpty(result.Issues);
        var issue = Assert.Single(
            result.Issues, i => i.Category == "method-name");
        Assert.Equal("Svc.run", issue.Element);
        Assert.Equal(ClaritySeverity.High, issue.Severity);
    }

    [Fact]
    public void Empty_tree_scores_only_the_neutral_defaults()
    {
        // Java parity: no methods/classes scores 0.0 each, no params 1.0,
        // no structure 1.0, empty cohesion 0.7 — overall 0.47.
        var tree = new TraceTree([]);

        var result = ClarityAnalyzer.Analyze(tree);

        Assert.Equal(0.47, result.Overall, 3);
    }

    [Fact]
    public void Named_property_is_scored_on_the_noun_rubric()
    {
        // "description" is a noun: poor as a verb+noun method name, good as a
        // domain noun. Naming it as a property must move it onto the noun
        // rubric rather than punishing the author for a name they did write.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Expense", "description", []),
                new Returned(null), [], 0),
        ]);

        var asMethod = ClarityAnalyzer.Analyze(tree);
        var asProperty = ClarityAnalyzer.Analyze(
            tree, new HashSet<string>(StringComparer.Ordinal)
            {
                "description",
            });

        Assert.True(
            asProperty.Method > asMethod.Method,
            $"noun rubric {asProperty.Method} should beat "
            + $"verb rubric {asMethod.Method}");
    }

    [Fact]
    public void Analyzer_reports_a_weak_property_under_the_property_category()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Expense", "data", []),
                new Returned(null), [], 0),
        ]);

        var result = ClarityAnalyzer.Analyze(
            tree, new HashSet<string>(StringComparer.Ordinal) { "data" });

        Assert.Contains(result.Issues, i => i.Category == "property-name");
        Assert.DoesNotContain(result.Issues, i => i.Category == "method-name");
    }

    [Fact]
    public void All_dimensions_are_populated()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "placeOrder",
                    [new ParameterCapture("orderId", "42", false)]),
                new Returned(null), [], 0),
        ]);

        var result = ClarityAnalyzer.Analyze(tree);

        Assert.True(result.Method > 0);
        Assert.True(result.Class > 0);
        Assert.True(result.Parameter > 0);
        Assert.True(result.Structural > 0);
        Assert.True(result.Cohesion > 0);
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Analyze must terminate rather than recurse the call
    // stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Analyze_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "run", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "call", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var result = ClarityAnalyzer.Analyze(new TraceTree([a]));

        Assert.True(result.Method > 0);
    }
}
