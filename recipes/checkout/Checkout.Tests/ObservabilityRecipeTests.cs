// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Observability;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/send-traces-to-the-observability-stack-you-already-run.md: ONE run of the checkout
/// writes the log lines it always wrote AND an OpenTelemetry span per traced call, with the parentage
/// and the attributes the page shows — and without the redacted values (rule 8, docs as tests).
/// The pipeline is composed explicitly — narration on the synchronous path, the OpenTelemetry listener
/// beside it — because an in-memory exporter cannot be discovered and a global SDK leaks between tests.
/// The exported spans are written out by <see cref="ExportedSpans"/>, naming and sorting everything it
/// prints, so one run's dump is byte-identical to the next: a raw span dump carries timestamps and
/// OpenTelemetry's own generated ids and could never be embedded.
/// </summary>
public sealed class ObservabilityRecipeTests
{
    private const string Slug = "send-traces-to-the-observability-stack-you-already-run";
    private const string SourceName = "NarrativeTrace";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void One_run_narrates_and_exports()
    {
        var log = new StringWriter { NewLine = "\n" };
        var spans = new List<Activity>();
        var context = ExportingContext(log, spans, TracingLevel.Detail, out var cleanup);
        using var _ = cleanup;

        var orderId = CheckOut(context);

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        AssertOneSpanPerTracedCall(spans);
        AssertNoRedactedValueWasExported(spans);
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "exported-spans.txt", ExportedSpans(spans));
    }

    [Fact]
    public void At_summary_the_same_four_spans_leave_without_parameter_attributes()
    {
        var spans = new List<Activity>();
        var context = ExportingContext(new StringWriter(), spans, TracingLevel.Summary, out var cleanup);
        using var _ = cleanup;

        CheckOut(context);

        Assert.Equal(4, spans.Count);
        Assert.DoesNotContain(spans.SelectMany(span => span.TagObjects), tag => tag.Key.StartsWith("narrative.param.", StringComparison.Ordinal));
    }

    private static SyncNarrativeContext ExportingContext(
        TextWriter log, List<Activity> spans, TracingLevel level, out IDisposable cleanup)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter(spans)))
            .Build();

        // The pipeline this page is about: the narration you already have, and the OpenTelemetry
        // listener beside it on the same synchronous path. What CaptureTrace() reads is untouched.
        var narration = new LoggingTraceEventListener(loggerFactory.CreateLogger("NarrativeTrace"));
        var source = new ActivitySource(SourceName);
        var exporter = new OtelTraceEventListener(source);
        var pipeline = new DualPathPipeline(traceEvent =>
        {
            narration.OnEvent(traceEvent);
            exporter.OnEvent(traceEvent);
        });
        cleanup = new Disposables(tracerProvider, source, loggerFactory);
        return new SyncNarrativeContext(
            new NarrativeTraceConfig(level, initialTraceparent: Traceparent.Parse(DemoTraceparent)), pipeline);
    }

    private static string CheckOut(INarrativeContext context)
    {
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(new InMemoryInventory("SKU-KB", 5), context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);
        return checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");
    }

    /// <summary>Four traced calls, four spans, ended innermost first — and the tree is reconstructed.</summary>
    private static void AssertOneSpanPerTracedCall(List<Activity> exported)
    {
        Assert.Equal(
            ["IInventoryService.Reserve", "IPaymentService.Charge", "IOrderRepository.Save", "IOrderService.PlaceOrder"],
            exported.Select(span => span.DisplayName));
        var root = Named(exported, "IOrderService.PlaceOrder");
        Assert.Equal(root.SpanId, Named(exported, "IPaymentService.Charge").ParentSpanId);
        Assert.DoesNotContain(exported, span => span.SpanId == root.ParentSpanId);
    }

    /// <summary>What the deny-list caught never reaches the exporter either — there is no attribute for it.</summary>
    private static void AssertNoRedactedValueWasExported(List<Activity> exported)
    {
        var tags = exported.SelectMany(span => span.TagObjects).ToList();
        Assert.DoesNotContain(tags, tag => tag.Key is "narrative.param.cardNumber" or "narrative.param.card");
        Assert.DoesNotContain(
            tags, tag => Convert.ToString(tag.Value, CultureInfo.InvariantCulture)!.Contains("4242424242424242", StringComparison.Ordinal));
    }

    private static Activity Named(List<Activity> exported, string name) =>
        exported.Single(span => span.DisplayName == name);

    /// <summary>The dump the page embeds: span name, parent, and every attribute, sorted by key.</summary>
    private static string ExportedSpans(List<Activity> exported)
    {
        var text = new StringBuilder();
        foreach (var span in exported)
        {
            var parent = exported.FirstOrDefault(other => other.SpanId == span.ParentSpanId)?.DisplayName;
            text.Append("span: ").Append(span.DisplayName).Append('\n');
            text.Append("  parent: ").Append(parent ?? "(none — this is the root)").Append('\n');
            foreach (var tag in span.TagObjects.OrderBy(tag => tag.Key, StringComparer.Ordinal))
            {
                text.Append("    ").Append(tag.Key).Append(": ")
                    .Append(Convert.ToString(tag.Value, CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        return text.ToString();
    }

    private sealed class Disposables(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
    }

    private sealed class InMemoryExporter(List<Activity> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                spans.Add(activity);
            }

            return ExportResult.Success;
        }
    }
}
