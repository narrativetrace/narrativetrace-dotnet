// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>Mirrors Java's <c>TranslationSubscriberTest</c>.</summary>
public sealed class TranslationSubscriberTests : IDisposable
{
    private readonly List<string> sink = [];

    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"translation-subscriber-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Renders_a_translated_enter_line_to_the_sink()
    {
        var subscriber = new TranslationSubscriber(ChargeGlossary(), "es", sink.Add);

        subscriber.OnEvent(EnterEvent(
            RootSpanContext(), "PaymentService", "charge",
            new ParameterCapture("amount", "74.97", false)));

        Assert.Equal(["PaymentService.cobrar (charge) (amount: 74.97)"], sink);
    }

    [Fact]
    public void Depth_accumulates_within_one_trace_and_traces_stay_isolated()
    {
        var subscriber = new TranslationSubscriber(ChargeGlossary(), "es", sink.Add);
        var rootA = RootSpanContext();
        var rootB = RootSpanContext();

        subscriber.OnEvent(EnterEvent(rootA, "OrderService", "placeOrder"));
        subscriber.OnEvent(EnterEvent(ChildSpanContext(rootA), "PaymentService", "charge"));
        subscriber.OnEvent(EnterEvent(rootB, "InvoiceService", "print"));

        Assert.Equal(
            [
                "OrderService.placeOrder()",
                "  PaymentService.cobrar (charge) ()",
                "InvoiceService.print()",
            ],
            sink);
    }

    [Fact]
    public void Root_exit_emits_the_gaps_footer_and_drops_the_trace_state()
    {
        var subscriber = new TranslationSubscriber(BillingGlossary(), "es", sink.Add);
        var root = RootSpanContext();
        var child = ChildSpanContext(root);

        subscriber.OnEvent(EnterEvent(root, "OrderService", "audit"));
        subscriber.OnEvent(ExitEvent(root, "\"done\""));
        sink.Clear();
        subscriber.OnEvent(EnterEvent(child, "PaymentService", "charge"));

        Assert.Equal(["PaymentService.charge()"], sink);
    }

    [Fact]
    public void Root_exit_footer_lists_the_accumulated_gaps()
    {
        var subscriber = new TranslationSubscriber(BillingGlossary(), "es", sink.Add);
        var root = RootSpanContext();

        subscriber.OnEvent(EnterEvent(root, "OrderService", "audit"));
        subscriber.OnEvent(ExitEvent(root, "\"done\""));

        Assert.Equal(
            [
                "OrderService.audit()",
                "  -> devuelve \"done\"",
                "---",
                "Vacíos del glosario:",
                "- audit",
            ],
            sink);
    }

    [Fact]
    public void Capacity_eviction_emits_the_evicted_trace_footer()
    {
        var subscriber = new TranslationSubscriber(
            BillingGlossary(), "es", sink.Add, 1, TimeSpan.FromMinutes(5));

        subscriber.OnEvent(EnterEvent(RootSpanContext(), "OrderService", "audit"));
        subscriber.OnEvent(EnterEvent(RootSpanContext(), "InvoiceService", "print"));

        Assert.Equal(
            [
                "OrderService.audit()",
                "---",
                "Vacíos del glosario:",
                "- audit",
                "InvoiceService.print()",
            ],
            sink);
    }

    [Fact]
    public void Concurrency_events_without_trace_identity_are_skipped()
    {
        var subscriber = new TranslationSubscriber(ChargeGlossary(), "es", sink.Add);

        subscriber.OnEvent(new ForkCreatedEvent("group-1", 1));
        subscriber.OnEvent(new MergeEvent("group-1", 2, 3, 4));
        subscriber.OnEvent(new FireAndForgetEvent("group-1", 5));

        Assert.Empty(sink);
    }

    /// <summary>
    /// A grafted subtree carries no per-event narration the translated stream
    /// could render, and the mapper has no canonical entry for it — the
    /// subscriber must stay total rather than throwing into the pipeline.
    /// </summary>
    [Fact]
    public void Graft_events_are_skipped_rather_than_thrown_at_the_pipeline()
    {
        var subscriber = new TranslationSubscriber(ChargeGlossary(), "es", sink.Add);

        subscriber.OnEvent(new GraftEvent(
            null, 1, new TraceNode(
                new MethodSignature("PaymentService", "charge", []), new Incomplete(), [], 0)));

        Assert.Empty(sink);
    }

    [Fact]
    public void Rejects_invalid_construction_arguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TranslationSubscriber(null!, "es", sink.Add));
        Assert.Throws<ArgumentException>(
            () => new TranslationSubscriber(ChargeGlossary(), " ", sink.Add));
        Assert.Throws<ArgumentNullException>(
            () => new TranslationSubscriber(ChargeGlossary(), "es", (Action<string>)null!));
        Assert.Throws<ArgumentNullException>(
            () => new TranslationSubscriber(ChargeGlossary(), "es", sink.Add).OnEvent(null!));
    }

    [Fact]
    public void File_variant_writes_each_trace_to_its_own_markdown_file()
    {
        var subscriber = new TranslationSubscriber(BillingGlossary(), "es", root);
        var rootA = RootSpanContext();
        var rootB = RootSpanContext();

        subscriber.OnEvent(EnterEvent(rootA, "OrderService", "audit"));
        subscriber.OnEvent(EnterEvent(rootB, "InvoiceService", "print"));
        subscriber.OnEvent(ExitEvent(rootA, "\"done\""));

        Assert.Equal(
            "OrderService.audit()\n  -> devuelve \"done\"\n---\nVacíos del glosario:\n- audit\n",
            File.ReadAllText(Path.Combine(root, rootA.TraceId.Value + ".md")));
        Assert.Equal(
            "InvoiceService.print()\n",
            File.ReadAllText(Path.Combine(root, rootB.TraceId.Value + ".md")));
    }

    [Fact]
    public void File_variant_routes_eviction_footers_to_the_evicted_trace_file()
    {
        var subscriber = new TranslationSubscriber(
            BillingGlossary(), "es", root, 1, TimeSpan.FromMinutes(5));
        var rootA = RootSpanContext();
        var rootB = RootSpanContext();

        subscriber.OnEvent(EnterEvent(rootA, "OrderService", "audit"));
        subscriber.OnEvent(EnterEvent(rootB, "InvoiceService", "print"));

        Assert.Equal(
            "OrderService.audit()\n---\nVacíos del glosario:\n- audit\n",
            File.ReadAllText(Path.Combine(root, rootA.TraceId.Value + ".md")));
        Assert.Equal(
            "InvoiceService.print()\n",
            File.ReadAllText(Path.Combine(root, rootB.TraceId.Value + ".md")));
    }

    [Fact]
    public void File_variant_drops_lines_it_cannot_write_and_keeps_serving_other_traces()
    {
        using var diagnostics = new StringWriter();
        var subscriber = new TranslationSubscriber(
            ChargeGlossary(), "es", root, TranslationSubscriber.DefaultCapacity,
            TranslationSubscriber.DefaultTtl, diagnostics);
        var blocked = RootSpanContext();
        var healthy = RootSpanContext();
        Directory.CreateDirectory(Path.Combine(root, blocked.TraceId.Value + ".md"));

        subscriber.OnEvent(EnterEvent(blocked, "PaymentService", "charge"));
        subscriber.OnEvent(EnterEvent(blocked, "PaymentService", "charge"));
        subscriber.OnEvent(EnterEvent(healthy, "PaymentService", "charge"));

        Assert.Equal(
            "PaymentService.cobrar (charge) ()\n",
            File.ReadAllText(Path.Combine(root, healthy.TraceId.Value + ".md")));
    }

    [Fact]
    public void File_variant_reports_a_write_failure_only_once()
    {
        using var diagnostics = new StringWriter();
        var subscriber = new TranslationSubscriber(
            ChargeGlossary(), "es", root, TranslationSubscriber.DefaultCapacity,
            TranslationSubscriber.DefaultTtl, diagnostics);
        var blocked = RootSpanContext();
        Directory.CreateDirectory(Path.Combine(root, blocked.TraceId.Value + ".md"));

        subscriber.OnEvent(EnterEvent(blocked, "PaymentService", "charge"));
        subscriber.OnEvent(EnterEvent(blocked, "PaymentService", "charge"));

        var reported = diagnostics.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(reported);
        Assert.Contains("dropping translated trace lines", reported[0], StringComparison.Ordinal);
    }

    [Fact]
    public void File_variant_fails_fast_on_an_uncreatable_output_directory()
    {
        Directory.CreateDirectory(root);
        var occupied = Path.Combine(root, "occupied");
        File.WriteAllText(occupied, "not a directory");

        var thrown = Assert.Throws<ArgumentException>(
            () => new TranslationSubscriber(ChargeGlossary(), "es", occupied));
        Assert.Contains("occupied", thrown.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(
            () => new TranslationSubscriber(ChargeGlossary(), "es", (string)null!));
    }

    [Fact]
    public async Task Receives_events_live_from_a_buffered_event_consumer()
    {
        var lines = new List<string>();
        using var consumer = new BufferedEventConsumer(16);
        var subscriber = new TranslationSubscriber(
            ChargeGlossary(), "es", line => { lock (lines) { lines.Add(line); } });
        consumer.Subscribe(subscriber.OnEvent);

        consumer.Publish(EnterEvent(RootSpanContext(), "PaymentService", "charge"));
        await consumer.WhenCountReached(1).WaitAsync(TimeSpan.FromSeconds(2));
        consumer.Flush();

        lock (lines)
        {
            Assert.Equal(["PaymentService.cobrar (charge) ()"], lines);
        }
    }

    // ── Fixtures ──────────────────────────────────────────────────────────

    private static GlossaryTerm Term(
        string term, TermKind kind, Dictionary<string, string> translations)
    {
        return new GlossaryTerm(
            term, "billing", kind, TermStatus.Curated, null, translations, [], [],
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Glossary BillingGlossary(params GlossaryTerm[] terms)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
            },
            terms);
    }

    private static Glossary ChargeGlossary()
    {
        return BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));
    }

    private static SpanContext RootSpanContext()
    {
        return new SpanContext(
            SpanIdGenerator.GenerateTraceId(), SpanIdGenerator.GenerateSpanId(), null);
    }

    private static SpanContext ChildSpanContext(SpanContext parent)
    {
        return new SpanContext(
            parent.TraceId, SpanIdGenerator.GenerateSpanId(), parent.SpanId);
    }

    private static EnterEvent EnterEvent(
        SpanContext spanContext,
        string className,
        string methodName,
        params ParameterCapture[] parameters)
    {
        return new EnterEvent(
            spanContext, 0, new MethodSignature(className, methodName, parameters));
    }

    private static ExitEvent ExitEvent(SpanContext spanContext, string renderedValue)
    {
        return new ExitEvent(spanContext, 0, new Returned(renderedValue));
    }
}
