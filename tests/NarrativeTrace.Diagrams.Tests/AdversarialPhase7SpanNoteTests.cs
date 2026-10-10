// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

/// <summary>
/// Adversarial probes of the sequence diagrams' span notes: every call arrow is followed by one note
/// citing the id the structural trace gives the same call, in both grammars.
/// </summary>
public sealed class AdversarialPhase7SpanNoteTests
{
    private static TraceNode Node(
        string cls, string method, ConcurrencyInfo? info, TraceOutcome? outcome = null, params TraceNode[] children) =>
        new(new MethodSignature(cls, method, []), outcome ?? new Returned(null), children, 0, 0, info);

    private static ConcurrencyInfo Info(string group, ConcurrencyKind kind) =>
        new(group, group, 1, null, false, kind);

    /// <summary>A fork (Zed captured before Alpha), a plain call, a launch with a stub, and a root async group.</summary>
    private static TraceTree Mixed()
    {
        var zed = Node("Zed", "Begin", Info("f1", ConcurrencyKind.ForkJoin), null, Node("Deep", "Leaf", null));
        var alpha = Node("Alpha", "Start", Info("f1", ConcurrencyKind.ForkJoin));
        var stub = Node("Notif", "fire-and-forget", Info("f2", ConcurrencyKind.FireAndForget));
        var send = Node("Send", "Mail", Info("f2", ConcurrencyKind.FireAndForget), null, Node("Sub", "Call", null));
        var checkout = Node("Checkout", "Complete", null, null, zed, alpha, Node("Plain", "Step", null), stub, send);
        return new TraceTree([checkout, Node("Y", "Bee", Info("a3", ConcurrencyKind.Async)), Node("X", "Ay", Info("a3", ConcurrencyKind.Async))]);
    }

    /// <summary>The structural trace's id per method name, plus the launch id under "fire-and-forget".</summary>
    private static Dictionary<string, string> StructuralIds(TraceTree tree)
    {
        var text = StructuralTraceRenderer.Render(tree);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(text, @"(?m)^\s*(#[\d.]+) - \w+\.([\w-]+)\("))
        {
            ids[m.Groups[2].Value] = m.Groups[1].Value;
        }

        foreach (Match m in Regex.Matches(text, @"(?m)^\s*(#[\d.]+) ~ fire-and-forget"))
        {
            ids["fire-and-forget"] = m.Groups[1].Value;
        }

        return ids;
    }

    /// <summary>For each call arrow, the method it calls and the id its following note cites (skipping lifeline bars).</summary>
    private static List<(string Method, string Id)> ArrowNotes(string diagram, Regex arrow, Regex note)
    {
        var lines = diagram.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var pairs = new List<(string, string)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var a = arrow.Match(lines[i]);
            if (!a.Success)
            {
                continue;
            }

            var j = i + 1;
            while (j < lines.Count && Regex.IsMatch(lines[j], @"^\s*(de)?activate "))
            {
                j++;
            }

            var n = j < lines.Count ? note.Match(lines[j]) : Match.Empty;
            pairs.Add((a.Groups[1].Value, n.Success ? n.Groups[1].Value : "<no span note>"));
        }

        return pairs;
    }

    private static readonly Regex MermaidArrow = new(@"^\s*\w+->>\w+: (\w[\w-]*)\(", RegexOptions.CultureInvariant);
    private static readonly Regex MermaidNote = new(@"^\s*Note over \w+: (#[\d.]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex PlantArrow = new(@"^\w+ -> \w+ : (\w[\w-]*)\(", RegexOptions.CultureInvariant);
    private static readonly Regex PlantNote = new(@"^\s*hnote over \w+ : (#[\d.]+)$", RegexOptions.CultureInvariant);

    [Fact]
    public void Every_mermaid_call_arrow_is_followed_by_a_note_citing_the_structural_id()
    {
        var tree = Mixed();
        var structural = StructuralIds(tree);

        var pairs = ArrowNotes(MermaidSequenceRenderer.Render(tree), MermaidArrow, MermaidNote);

        Assert.Equal(structural.Count, pairs.Count);
        foreach (var (method, id) in pairs)
        {
            Assert.Equal(structural[method], id);
        }
    }

    [Fact]
    public void Every_plantuml_call_arrow_is_followed_by_a_note_citing_the_structural_id()
    {
        var tree = Mixed();
        var structural = StructuralIds(tree);

        var pairs = ArrowNotes(PlantUmlSequenceRenderer.Render(tree), PlantArrow, PlantNote);

        Assert.Equal(structural.Count, pairs.Count);
        foreach (var (method, id) in pairs)
        {
            Assert.Equal(structural[method], id);
        }
    }

    [Fact]
    public void Mermaid_and_plantuml_cite_the_same_ids_in_the_same_order()
    {
        var tree = Mixed();

        var mermaid = ArrowNotes(MermaidSequenceRenderer.Render(tree), MermaidArrow, MermaidNote);
        var plant = ArrowNotes(PlantUmlSequenceRenderer.Render(tree), PlantArrow, PlantNote);

        Assert.Equal(mermaid, plant);
    }

    [Fact]
    public void An_incomplete_call_has_its_span_note_before_the_in_flight_note()
    {
        var tree = new TraceTree([Node("Svc", "Hang", null, new Incomplete())]);

        var pairs = ArrowNotes(MermaidSequenceRenderer.Render(tree), MermaidArrow, MermaidNote);

        Assert.Equal([("Hang", "#1")], pairs);
    }

    [Fact]
    public void A_thrown_call_still_has_exactly_one_span_note_before_its_throw_arrow()
    {
        var tree = new TraceTree([Node("Svc", "Break", null, new Threw(new InvalidOperationException("boom")))]);

        var pairs = ArrowNotes(PlantUmlSequenceRenderer.Render(tree), PlantArrow, PlantNote);

        Assert.Equal([("Break", "#1")], pairs);
    }

    [Fact]
    public void An_empty_tree_draws_no_call_arrows_and_no_span_notes()
    {
        var mermaid = MermaidSequenceRenderer.Render(new TraceTree([]));

        Assert.DoesNotContain("Note over", mermaid);
    }

    [Theory]
    [InlineData("#1 ")]
    [InlineData("#")]
    [InlineData("#1.")]
    [InlineData("#1;")]
    [InlineData("#1 - A.B()")]
    public void A_span_note_label_accepts_only_a_well_formed_id(string text)
    {
        Assert.Throws<ArgumentException>(() => DiagramLabel.SpanId(text));
    }

    [Fact]
    public void Every_root_and_child_note_of_a_deep_chain_matches_the_structural_id()
    {
        var node = Node("Leaf", "Call", null);
        for (var i = 1; i < 30; i++)
        {
            node = Node("Level" + i, "Step", null, null, node);
        }

        var tree = new TraceTree([node]);
        var pairs = ArrowNotes(MermaidSequenceRenderer.Render(tree), MermaidArrow, MermaidNote);

        Assert.Equal("#1" + string.Concat(Enumerable.Repeat(".1", 29)), pairs[^1].Id);
        Assert.Equal(StructuralIds(tree)["Call"], pairs[^1].Id);
    }
}
