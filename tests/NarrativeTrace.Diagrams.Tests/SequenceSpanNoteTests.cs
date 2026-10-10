// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

/// <summary>
/// D8 in the sequence diagrams: every call arrow is followed by a note over its target citing the
/// span's id — the same id the structural trace gives it — so "who called whom, and when" can be
/// pointed at by id. The note carries only a derived id, never trace text.
/// </summary>
public sealed class SequenceSpanNoteTests
{
    private static ConcurrencyInfo Fork(int threadId) =>
        new("fork-1", "pool", threadId, "pool", true, ConcurrencyKind.ForkJoin);

    private static TraceTree Checkout()
    {
        var stock = new TraceNode(
            new MethodSignature("Stock", "Check", []), new Returned("true"), [], 0, 0, Fork(2));
        var discount = new TraceNode(
            new MethodSignature("Discount", "Calculate", []), new Returned("0.15"), [], 0, 0, Fork(1));
        var checkout = new TraceNode(
            new MethodSignature("Checkout", "Complete", []), new Returned(null), [stock, discount], 0);
        var audit = new TraceNode(
            new MethodSignature("Audit", "Record", []), new Returned(null), [], 0);
        return new TraceTree([checkout, audit]);
    }

    [Fact]
    public void Mermaid_notes_every_call_with_its_structural_span_id()
    {
        var diagram = MermaidSequenceRenderer.Render(Checkout()).Replace("\r\n", "\n");

        Assert.Contains("    C->>C: Complete()\n    Note over C: #1\n", diagram);
        Assert.Contains("    Note over S: #1.2\n", diagram);
        Assert.Contains("    Note over D: #1.1\n", diagram);
        Assert.Contains("    Note over A: #2\n", diagram);
    }

    [Fact]
    public void PlantUml_notes_every_call_with_its_structural_span_id()
    {
        var diagram = PlantUmlSequenceRenderer.Render(Checkout()).Replace("\r\n", "\n");

        Assert.Contains("hnote over C : #1\n", diagram);
        Assert.Contains("hnote over S : #1.2\n", diagram);
        Assert.Contains("hnote over D : #1.1\n", diagram);
        Assert.Contains("hnote over A : #2\n", diagram);
    }

    [Fact]
    public void A_span_id_label_refuses_anything_but_a_well_formed_id()
    {
        Assert.Equal("#1.2", DiagramLabel.SpanId("#1.2").Text);
        Assert.Throws<ArgumentException>(() => DiagramLabel.SpanId("#1\nNote over X: injected"));
        Assert.Throws<ArgumentException>(() => DiagramLabel.SpanId("1.2"));
    }
}
