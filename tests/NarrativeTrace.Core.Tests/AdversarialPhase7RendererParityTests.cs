// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Adversarial probes of the four text flavours (structural, indented, Markdown, prose): every
/// flavour must cite the same <see cref="SpanPath"/> id for the same span, including under forks,
/// async groups, fire-and-forget launches, empty trees and deep trees.
/// </summary>
public sealed class AdversarialPhase7RendererParityTests
{
    private static TraceNode Node(
        string cls, string method, ConcurrencyInfo? info, params TraceNode[] children) =>
        new(new MethodSignature(cls, method, []), new Returned(null), children, 0, 0, info);

    private static ConcurrencyInfo Info(string group, ConcurrencyKind kind) =>
        new(group, group, 1, null, false, kind);

    /// <summary>
    /// Checkout with a fork (Zed listed before Alpha), a plain call, a fire-and-forget launch whose
    /// launched work has its own child, and a root-level async group captured Y before X.
    /// </summary>
    private static TraceTree Concurrent()
    {
        var zed = Node("Zed", "Run", Info("f1", ConcurrencyKind.ForkJoin), Node("Deep", "Leaf", null));
        var alpha = Node("Alpha", "Run", Info("f1", ConcurrencyKind.ForkJoin));
        var plain = Node("Plain", "Step", null);
        var stub = Node("Notif", "fire-and-forget", Info("f2", ConcurrencyKind.FireAndForget));
        var send = Node("Send", "Mail", Info("f2", ConcurrencyKind.FireAndForget), Node("Sub", "Call", null));
        var checkout = Node("Checkout", "Complete", null, zed, alpha, plain, stub, send);
        var y = Node("Y", "Bee", Info("a3", ConcurrencyKind.Async));
        var x = Node("X", "Ay", Info("a3", ConcurrencyKind.Async));
        return new TraceTree([checkout, y, x]);
    }

    private static string IdInStructural(string text, string cls, string method) =>
        Regex.Match(text, @"(?m)^\s*(#[\d.]+) - " + Regex.Escape(cls + "." + method) + @"\(").Groups[1].Value;

    private static string IdInIndented(string text, string cls, string method) =>
        Regex.Match(text, @"(?m)" + Regex.Escape(cls + "." + method) + @"\(.*? (#[\d.]+)$").Groups[1].Value;

    private static string IdInMarkdown(string text, string cls, string method) =>
        Regex.Match(text, @"(?m)\*\*" + Regex.Escape(cls + "." + method) + @"\*\*.*? (#[\d.]+)$").Groups[1].Value;

    [Theory]
    [InlineData("Checkout", "Complete", "#1")]
    [InlineData("Alpha", "Run", "#1.1")]
    [InlineData("Zed", "Run", "#1.2")]
    [InlineData("Deep", "Leaf", "#1.2.1")]
    [InlineData("Plain", "Step", "#1.3")]
    [InlineData("Send", "Mail", "#1.4.1")]
    [InlineData("Sub", "Call", "#1.4.1.1")]
    [InlineData("X", "Ay", "#2")]
    public void Every_flavour_cites_the_same_id_for_the_same_span_in_a_fork_fire_and_forget_and_async_tree(
        string cls, string method, string expected)
    {
        var tree = Concurrent();

        Assert.Equal(expected, IdInStructural(StructuralTraceRenderer.Render(tree), cls, method));
        Assert.Equal(expected, IdInIndented(IndentedTextRenderer.Render(tree), cls, method));
        Assert.Equal(expected, IdInMarkdown(MarkdownRenderer.Render(tree), cls, method));
    }

    [Fact]
    public void Structural_prints_the_fire_and_forget_marker_with_the_launch_id_and_hides_the_stub()
    {
        var text = StructuralTraceRenderer.Render(Concurrent());

        Assert.Contains("#1.4 ~ fire-and-forget\n", text);
        Assert.DoesNotContain("Notif.fire-and-forget", text);
    }

    [Fact]
    public void Structural_document_of_an_empty_tree_is_the_empty_string()
    {
        Assert.Equal("", StructuralTraceRenderer.Render(new TraceTree([])));
    }

    [Fact]
    public void Indented_text_of_an_empty_tree_is_the_empty_string()
    {
        Assert.Equal("", IndentedTextRenderer.Render(new TraceTree([])));
    }

    [Fact]
    public void Prose_of_an_empty_tree_is_the_empty_string()
    {
        Assert.Equal("", ProseRenderer.Render(new TraceTree([])));
    }

    [Fact]
    public void Deep_chain_numbers_its_leaf_with_one_position_per_level_in_every_flavour()
    {
        const int depth = 40;
        var node = Node("Leaf", "Call", null);
        for (var i = 1; i < depth; i++)
        {
            node = Node("Level" + i, "Call", null, node);
        }

        var tree = new TraceTree([node]);
        var expected = "#1" + string.Concat(Enumerable.Repeat(".1", depth - 1));

        Assert.Equal(expected, IdInStructural(StructuralTraceRenderer.Render(tree), "Leaf", "Call"));
        Assert.Equal(expected, IdInIndented(IndentedTextRenderer.Render(tree), "Leaf", "Call"));
        Assert.Equal(expected, IdInMarkdown(MarkdownRenderer.Render(tree), "Leaf", "Call"));
    }

    [Fact]
    public void Indented_text_prints_a_root_level_fire_and_forget_launch_with_the_launch_marker()
    {
        var stub = Node("Notif", "fire-and-forget", Info("f2", ConcurrencyKind.FireAndForget));
        var send = Node("Send", "Mail", Info("f2", ConcurrencyKind.FireAndForget));
        var tree = new TraceTree([stub, send]);

        var text = IndentedTextRenderer.Render(tree);

        Assert.Contains("⤳ fire-and-forget", text);
    }

    [Fact]
    public void Launched_work_with_no_launcher_stub_is_numbered_under_one_launch_position()
    {
        var w1 = Node("W", "One", Info("g", ConcurrencyKind.FireAndForget));
        var w2 = Node("W", "Two", Info("g", ConcurrencyKind.FireAndForget));
        var tree = new TraceTree([Node("Svc", "Root", null, w1, w2)]);

        var text = StructuralTraceRenderer.Render(tree);

        Assert.Contains("  #1.1 ~ fire-and-forget\n", text);
        Assert.Contains("    #1.1.1 - W.One()", text);
        Assert.Contains("    #1.1.2 - W.Two()", text);
    }

    [Fact]
    public void Launcher_named_fire_and_forget_that_has_children_is_printed_as_a_call_not_dropped()
    {
        var launcher = Node("Svc", "fire-and-forget", Info("g", ConcurrencyKind.FireAndForget), Node("Child", "Do", null));
        var tree = new TraceTree([launcher]);

        var text = StructuralTraceRenderer.Render(tree);

        Assert.Contains("#1.1 - Svc.fire-and-forget()", text);
        Assert.Contains("#1.1.1 - Child.Do()", text);
    }

    [Fact]
    public void Indented_text_of_a_launch_with_only_its_stub_says_the_result_was_not_captured()
    {
        var stub = Node("Notif", "fire-and-forget", Info("f2", ConcurrencyKind.FireAndForget));
        var tree = new TraceTree([Node("Checkout", "Complete", null, stub)]);

        Assert.Contains("[launched, result not captured]", IndentedTextRenderer.Render(tree));
    }

    [Fact]
    public void Prose_cites_ids_in_print_order_matching_the_structural_order_for_an_ordered_tree()
    {
        var tree = new TraceTree(
        [
            Node("A", "Root", null, Node("B", "First", null), Node("C", "Second", null, Node("D", "Third", null))),
            Node("E", "Other", null),
        ]);

        var prose = Regex.Matches(ProseRenderer.Render(tree), @"\((#[\d.]+)\)")
            .Select(m => m.Groups[1].Value).ToArray();
        var structural = Regex.Matches(StructuralTraceRenderer.Render(tree), @"(?m)^\s*(#[\d.]+) - ")
            .Select(m => m.Groups[1].Value).ToArray();

        Assert.Equal(structural, prose);
    }
}
