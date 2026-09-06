// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TemplateWarningCollectorTests
{
    [Fact]
    public void Reports_unresolved_placeholder_in_narration()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", [],
                    "Placing order for {custmerId}"),
                new Returned(null), [], 0),
        ]);

        var warnings = TemplateWarningCollector.Collect(tree);

        Assert.Contains(
            "OrderService.PlaceOrder: {custmerId} in narration",
            warnings);
    }

    [Fact]
    public void Reports_unresolved_placeholder_in_error_context()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Pay", "Charge", [], null,
                    "Declined for {custmerId}"),
                new Threw(new InvalidOperationException("boom")), [], 0),
        ]);

        var warnings = TemplateWarningCollector.Collect(tree);

        Assert.Contains(
            "Pay.Charge: {custmerId} in errorContext", warnings);
    }

    [Fact]
    public void Fully_resolved_text_produces_no_warnings()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", [],
                    "Placing order for C-1234"),
                new Returned(null), [], 0),
        ]);

        Assert.Empty(TemplateWarningCollector.Collect(tree));
    }

    [Theory]
    [InlineData("sent {order id}", "{order id}")]
    [InlineData("charged {order-id}", "{order-id}")]
    [InlineData("for {customer.address.city}", "{customer.address.city}")]
    [InlineData("empty {  }", "{  }")]
    public void Flags_tokens_with_spaces_punctuation_and_multi_segment_paths(
        string narration, string expectedToken)
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", [], narration),
                new Returned(null), [], 0),
        ]);

        Assert.Contains(
            $"Svc.Run: {expectedToken} in narration",
            TemplateWarningCollector.Collect(tree));
    }

    [Fact]
    public void Format_returns_empty_string_when_no_warnings()
    {
        Assert.Equal(string.Empty, TemplateWarningCollector.Format([]));
    }

    [Fact]
    public void Format_renders_header_and_indented_bullets()
    {
        var block = TemplateWarningCollector.Format(
            ["Svc.Run: {x} in narration", "Svc.Run: {y} in errorContext"]);

        Assert.Equal(
            "WARNING: Unresolved template placeholder(s) detected:\n"
            + "  - Svc.Run: {x} in narration\n"
            + "  - Svc.Run: {y} in errorContext\n",
            block);
    }

    [Fact]
    public void Scans_nested_children()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save", [], "saving {ordreId}"),
            new Returned(null), [], 0);
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [child], 0),
        ]);

        Assert.Contains(
            "Repo.Save: {ordreId} in narration",
            TemplateWarningCollector.Collect(tree));
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Collect must terminate rather than recurse the call
    // stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Collect_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var warnings = TemplateWarningCollector.Collect(new TraceTree([a]));

        Assert.Empty(warnings);
    }
}
