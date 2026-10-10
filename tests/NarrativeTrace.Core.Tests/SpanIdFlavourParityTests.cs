// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// D8: every flavour prints the same span id for the same span — the
/// structural <c>.nt</c> as the leading token, indented text and Markdown as a
/// trailing <c>#id</c>, prose as <c>(#id)</c> — so a report, a review and an
/// approval diff can all point at one span.
/// </summary>
public sealed class SpanIdFlavourParityTests
{
    private static ConcurrencyInfo Fork(int threadId) =>
        new("fork-1", "pool", threadId, "pool", true, ConcurrencyKind.ForkJoin);

    private static ConcurrencyInfo Background(string label) =>
        new("fanf-1", label, 9, "pool-9", true, ConcurrencyKind.FireAndForget);

    /// <summary>
    /// A root with a fork captured out of signature order, a plain call, a
    /// fire-and-forget launch with launched work, and a second root.
    /// </summary>
    internal static TraceTree Checkout()
    {
        var stock = new TraceNode(
            new MethodSignature("StockService", "Check", []),
            new Returned("true"), [], 0, 0, Fork(22));
        var discount = new TraceNode(
            new MethodSignature("DiscountEngine", "Calculate", []),
            new Returned("0.15"), [], 0, 0, Fork(21));
        var charge = new TraceNode(
            new MethodSignature("PaymentGateway", "Charge", [new ParameterCapture("amount", "1", false)]),
            new Returned(null), [], 0);
        var launcher = new TraceNode(
            new MethodSignature("NotificationService", "fire-and-forget", []),
            new Incomplete(), [], 0, 0, Background("fire-and-forget"));
        var send = new TraceNode(
            new MethodSignature("NotificationService", "Send", []),
            new Returned(null), [], 0, 0, Background("Send"));
        var checkout = new TraceNode(
            new MethodSignature("CheckoutService", "Complete", []),
            new Returned(null), [stock, discount, charge, launcher, send], 0);
        var audit = new TraceNode(
            new MethodSignature("AuditLog", "Record", []), new Returned(null), [], 0);
        return new TraceTree([checkout, audit]);
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

    private static void AssertSomeLineEndsWith(string text, string ending) =>
        Assert.Contains(Lines(text), line => line.EndsWith(ending, StringComparison.Ordinal));

    [Fact]
    public void Indented_text_ends_every_entry_line_with_the_structural_id()
    {
        var text = IndentedTextRenderer.Render(Checkout());

        AssertSomeLineEndsWith(text, "CheckoutService.Complete() #1");
        AssertSomeLineEndsWith(text, "DiscountEngine.Calculate() → 0.15 #1.1");
        AssertSomeLineEndsWith(text, "StockService.Check() → true #1.2");
        AssertSomeLineEndsWith(text, "PaymentGateway.Charge(amount: 1) #1.3");
        AssertSomeLineEndsWith(text, "fire-and-forget [thread: 9] #1.4");
        AssertSomeLineEndsWith(text, "NotificationService.Send() #1.4.1");
        AssertSomeLineEndsWith(text, "AuditLog.Record() #2");
    }

    [Fact]
    public void Markdown_ends_every_entry_line_with_the_structural_id()
    {
        var text = MarkdownRenderer.Render(Checkout(), new MarkdownOptions { IncludeFrontmatter = false });

        AssertSomeLineEndsWith(text, "- **CheckoutService.Complete**() #1");
        AssertSomeLineEndsWith(text, "↦ **DiscountEngine.Calculate**() → `0.15` [thread: 21] #1.1");
        AssertSomeLineEndsWith(text, "↦ **StockService.Check**() → `true` [thread: 22] #1.2");
        AssertSomeLineEndsWith(text, "- **PaymentGateway.Charge**(amount: `1`) #1.3");
        AssertSomeLineEndsWith(text, "fire-and-forget [thread: 9] #1.4");
        AssertSomeLineEndsWith(text, "↦ **NotificationService.Send**() [thread: 9] #1.4.1");
        AssertSomeLineEndsWith(text, "- **AuditLog.Record**() #2");
    }

    [Fact]
    public void Markdown_puts_the_id_on_a_thrown_leaf_entry_line_and_never_on_a_closing_line()
    {
        var thrown = new TraceNode(
            new MethodSignature("Gateway", "Charge", []),
            new Threw(new InvalidOperationException("declined")), [], 0);
        var parent = new TraceNode(
            new MethodSignature("Checkout", "Pay", []),
            new Threw(new InvalidOperationException("declined")), [thrown], 0);

        var text = MarkdownRenderer.Render(new TraceTree([parent]), new MarkdownOptions { IncludeFrontmatter = false });

        AssertSomeLineEndsWith(text, "- **Checkout.Pay**() #1");
        AssertSomeLineEndsWith(text, "InvalidOperationException: declined #1.1");
        Assert.Contains(Lines(text), l => l.TrimStart().StartsWith("- ❌", StringComparison.Ordinal) && !l.Contains('#', StringComparison.Ordinal));
    }

    [Fact]
    public void Prose_names_every_span_by_its_structural_id_in_parentheses()
    {
        var text = ProseRenderer.Render(Checkout());

        AssertSomeLineEndsWith(text, "The checkout service complete (#1)");
        AssertSomeLineEndsWith(text, "The discount engine calculate (#1.1) and returns 0.15");
        AssertSomeLineEndsWith(text, "The stock service check (#1.2) and returns true");
        AssertSomeLineEndsWith(text, "The payment gateway charge with amount: `1` (#1.3)");
        AssertSomeLineEndsWith(text, "In the background (#1.4):");
        AssertSomeLineEndsWith(text, "The notification service send (#1.4.1)");
        AssertSomeLineEndsWith(text, "The audit log record (#2)");
    }

    [Fact]
    public void Prose_lists_fork_members_in_the_order_their_ids_run()
    {
        var lines = Lines(ProseRenderer.Render(Checkout()));
        var discount = Array.FindIndex(lines, l => l.Contains("(#1.1)", StringComparison.Ordinal));
        var stock = Array.FindIndex(lines, l => l.Contains("(#1.2)", StringComparison.Ordinal));

        Assert.True(discount >= 0 && discount < stock);
    }
}
