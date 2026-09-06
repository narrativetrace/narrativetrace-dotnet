// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

public class MermaidSequenceRendererTests
{
    [Fact]
    public void Single_call_produces_participant_and_message()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save",
                [new ParameterCapture("id", "1", false)]),
            new Returned("\"ok\""), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("sequenceDiagram", result);
        Assert.Contains("participant", result);
        Assert.Contains("Svc", result);
        Assert.Contains("Repo", result);
        Assert.Contains("Save(id: 1)", result);
    }

    [Fact]
    public void Root_renders_self_call_and_self_return()
    {
        var root = new TraceNode(
            new MethodSignature("OrderService", "calculateTotal", []),
            new Returned("99.0"), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("OS->>OS: calculateTotal()", result);
        Assert.Contains("OS-->>OS: 99.0", result);
    }

    [Fact]
    public void Nested_calls_have_correct_arrow_directions()
    {
        var child = new TraceNode(
            new MethodSignature("RepoService", "Save", []),
            new Returned("\"ok\""), [], 0);
        var root = new TraceNode(
            new MethodSignature("OrderService", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("OS->>RS", result);
        Assert.Contains("RS-->>OS", result);
    }

    [Fact]
    public void Return_value_on_return_arrow()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Get", []),
            new Returned("42"), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("42", result);
    }

    [Fact]
    public void Return_value_newline_cannot_inject_a_diagram_line()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Get", []),
            new Returned("ok\nclick Repo href \"evil\""), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("ok click Repo", result);
        Assert.DoesNotContain("ok\nclick", result);
    }

    [Fact]
    public void Error_path_uses_cross_notation()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Threw(new InvalidOperationException("fail")),
            [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("-x", result);
        Assert.DoesNotContain("--x", result);
    }

    [Fact]
    public void Multiple_participants_deduped()
    {
        var child1 = new TraceNode(
            new MethodSignature("Repo", "Load", []),
            new Returned(null), [], 0);
        var child2 = new TraceNode(
            new MethodSignature("Repo", "Save", []),
            new Returned(null), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child1, child2], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        var count = result.Split("participant")
            .Length - 1;
        Assert.Equal(2, count);
    }

    [Fact]
    public void Empty_tree_produces_minimal_output()
    {
        var tree = new TraceTree([]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("sequenceDiagram", result);
    }

    [Fact]
    public void Display_name_with_space_is_quoted()
    {
        var root = new TraceNode(
            new MethodSignature("Order Service", "Run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("as \"Order Service\"", result);
    }

    [Fact]
    public void Qualified_display_name_with_dot_is_quoted()
    {
        var root = new TraceNode(
            new MethodSignature("com.example.OrderService", "Run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("as \"com.example.OrderService\"", result);
    }

    [Theory]
    [InlineData("Order-Service")]
    [InlineData("Order:Service")]
    public void Display_name_with_special_char_is_quoted(string name)
    {
        var root = new TraceNode(
            new MethodSignature(name, "Run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("as \"" + name + "\"", result);
    }

    [Fact]
    public void Incomplete_outcome_renders_in_flight_note_no_return()
    {
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Incomplete(), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("Note over S: in-flight", result);
        Assert.DoesNotContain("-->>", result);
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

        var result = MermaidSequenceRenderer.Render(new TraceTree([a]));

        Assert.Contains(TreeWalk.CycleMarker, result, StringComparison.Ordinal);
    }
}
