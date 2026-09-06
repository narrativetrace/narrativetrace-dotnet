// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

public class PlantUmlSequenceRendererTests
{
    [Fact]
    public void Default_render_omits_lifelines_for_java_parity()
    {
        var child = new TraceNode(
            new MethodSignature("RepoService", "Save", []),
            new Returned("\"ok\""), [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.DoesNotContain("activate", result);
        Assert.DoesNotContain("deactivate", result);
    }

    [Fact]
    public void Opt_in_single_call_with_activate_deactivate()
    {
        var child = new TraceNode(
            new MethodSignature("RepoService", "Save", []),
            new Returned("\"ok\""), [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(
            tree, includeLifelines: true);

        Assert.Contains("activate RS", result);
        Assert.Contains("deactivate RS", result);
    }

    [Fact]
    public void Root_renders_self_call_and_self_return()
    {
        var root = new TraceNode(
            new MethodSignature("OrderService", "calculateTotal", []),
            new Returned("99.0"), [], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("OS -> OS : calculateTotal()", result);
        Assert.Contains("OS --> OS : 99.0", result);
    }

    [Fact]
    public void Opt_in_nested_calls_with_correct_activation()
    {
        var grandchild = new TraceNode(
            new MethodSignature("DbService", "Insert", []),
            new Returned(null), [], 0);
        var child = new TraceNode(
            new MethodSignature("RepoService", "Save", []),
            new Returned(null), [grandchild], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(
            tree, includeLifelines: true);

        Assert.Contains("activate RS", result);
        Assert.Contains("activate DS", result);
        Assert.Contains("deactivate DS", result);
        Assert.Contains("deactivate RS", result);
    }

    [Fact]
    public void Return_value_on_return_arrow()
    {
        var child = new TraceNode(
            new MethodSignature("RepoService", "Get", []),
            new Returned("42"), [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("RS --> OS : 42", result);
    }

    [Fact]
    public void Return_value_newline_cannot_inject_a_diagram_line()
    {
        var child = new TraceNode(
            new MethodSignature("RepoService", "Get", []),
            new Returned("ok\n!include /etc/passwd"), [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("ok !include", result);
        Assert.DoesNotContain("ok\n!include", result);
    }

    [Fact]
    public void Error_path_uses_red_arrow()
    {
        var child = new TraceNode(
            new MethodSignature("RepoService", "Save", []),
            new Threw(new InvalidOperationException("fail")),
            [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("-[#red]->", result);
    }

    [Fact]
    public void Wraps_with_startuml_enduml()
    {
        var tree = new TraceTree([]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.StartsWith("@startuml", result);
        Assert.Contains("@enduml", result);
    }

    [Fact]
    public void Empty_tree_produces_minimal_output()
    {
        var tree = new TraceTree([]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("@startuml", result);
        Assert.Contains("@enduml", result);
    }

    [Fact]
    public void Display_name_with_space_is_quoted()
    {
        var root = new TraceNode(
            new MethodSignature("Order Service", "Run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("as \"Order Service\"", result);
    }

    [Fact]
    public void Incomplete_outcome_renders_hnote_no_return()
    {
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Incomplete(), [], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("hnote over OS : in-flight", result);
        Assert.DoesNotContain(" --> ", result);
        Assert.DoesNotContain("-[#red]->", result);
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Render must degrade with a marker rather than recurse
    // the call stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Render_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var result = PlantUmlSequenceRenderer.Render(new TraceTree([a]));

        Assert.Contains(TreeWalk.CycleMarker, result, StringComparison.Ordinal);
    }
}
