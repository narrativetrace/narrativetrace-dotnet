// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Parity properties of the per-entry rendering API: the batch
/// <c>Render</c> must stay byte-identical to concatenating
/// <c>RenderEntry</c> over one state plus the gaps footer (the live
/// subscriber contract), and a view must be reusable — rendering the same
/// entries twice yields the same output, so no state leaks across render
/// sequences.
/// </summary>
/// <remarks>
/// Entry sequences deliberately include malformed shapes: null span ids,
/// unknown parents, exits without enters, unknown event types.
/// </remarks>
public class TraceTranslationViewProperties
{
    [Property(Arbitrary = [typeof(TranslationArbitraries)])]
    public void Batch_render_equals_concatenated_per_entry_renders_plus_gaps_footer(
        Glossary glossary, List<CanonicalEntry> entries, TargetLocale locale)
    {
        var view = new TraceTranslationView(glossary, _ => "Acme.Billing");
        var state = view.NewRenderState(locale.Tag);
        var incremental = new StringBuilder();
        foreach (var entry in entries)
        {
            incremental.Append(view.RenderEntry(entry, state));
        }

        incremental.Append(view.RenderGapsFooter(state));

        var batch = view.Render(entries, locale.Tag);

        Assert.Equal(incremental.ToString(), batch);
        Assert.Equal(batch, view.Render(entries, locale.Tag));
    }

    /// <summary>A locale tag the shipped scaffolding can answer for.</summary>
    /// <param name="Tag">BCP-47 locale tag.</param>
    public sealed record TargetLocale(string Tag);

    internal static class TranslationArbitraries
    {
        public static Arbitrary<Glossary> Glossaries()
        {
            return GlossaryArbitraries.Glossaries();
        }

        public static Arbitrary<TargetLocale> Locales()
        {
            return Gen.Elements("es", "de", "zh-CN").Select(tag => new TargetLocale(tag))
                .ToArbitrary();
        }

        public static Arbitrary<List<CanonicalEntry>> EntrySequences()
        {
            return (from size in Gen.Choose(0, 8)
                    from entries in Gen.OneOf(Enters(), Exits()).ListOf(size)
                    select entries.ToList()).ToArbitrary();
        }

        private static Gen<CanonicalEntry> Enters()
        {
            return from className in Gen.Elements("PaymentService", "OrderService")
                   from method in Gen.Elements("charge", "placeOrder", "alpha", "beta")
                   from spanId in SpanIds()
                   from parentSpanId in SpanIds()
                   from template in OrNull(Gen.Elements(
                       "Charging {amount} to {customerId}", "Auditing {amount}"))
                   from parameters in Parameters()
                   select Enter(className, method, spanId, parentSpanId, template, parameters);
        }

        private static Gen<CanonicalEntry> Exits()
        {
            return from className in Gen.Elements("PaymentService", "OrderService")
                   from spanId in SpanIds()
                   from outcome in Gen.Elements("success", "failure", "incomplete", "unknown")
                   from returnValue in OrNull(Gen.Elements("\"TXN-1\"", "42"))
                   from exceptionType in OrNull(Gen.Elements("AlphaException", "Exception"))
                   from message in OrNull(Gen.Elements("balance low", "boom"))
                   select Exit(className, spanId, outcome, returnValue, exceptionType, message);
        }

        private static Gen<string?> SpanIds()
        {
            return OrNull(Gen.Elements(
                "0000000000000001", "0000000000000002", "0000000000000003"));
        }

        private static Gen<IReadOnlyList<ParameterCapture>?> Parameters()
        {
            var parameter =
                from name in Gen.Elements("amount", "customerId", "id")
                from value in Gen.Elements("74.97", "\"C-1\"", "[REDACTED]")
                select new ParameterCapture(name, value, false);
            return from size in Gen.Choose(0, 2)
                   from parameters in parameter.ListOf(size)
                   let unique = parameters
                       .GroupBy(p => p.Name, StringComparer.Ordinal)
                       .Select(bucket => bucket.First())
                       .ToArray()
                   select unique.Length == 0 ? null : (IReadOnlyList<ParameterCapture>?)unique;
        }

        /// <summary>Half the samples are null — the malformed shapes are the point.</summary>
        private static Gen<string?> OrNull(Gen<string> values)
        {
            return Gen.Frequency<string?>(
                (1, Gen.Constant((string?)null)),
                (1, values.Select(value => (string?)value)));
        }

        private static CanonicalEntry Enter(
            string className,
            string method,
            string? spanId,
            string? parentSpanId,
            string? template,
            IReadOnlyList<ParameterCapture>? parameters)
        {
            return new CanonicalEntry(
                Timestamp: "2026-08-15T10:00:00.000Z",
                Level: "trace",
                Message: "→ " + className + "." + method,
                Service: "test",
                Environment: null,
                TraceId: "0123456789abcdef0123456789abcdef",
                SpanId: spanId,
                ParentSpanId: parentSpanId,
                CodeNamespace: className,
                CodeFunction: method,
                NtEventType: "method_enter",
                NtParameters: parameters,
                NtNarrationTemplate: template);
        }

        private static CanonicalEntry Exit(
            string className,
            string? spanId,
            string outcome,
            string? returnValue,
            string? exceptionType,
            string? exceptionMessage)
        {
            return new CanonicalEntry(
                Timestamp: "2026-08-15T10:00:00.100Z",
                Level: outcome == "failure" ? "error" : "trace",
                Message: "exit",
                Service: "test",
                Environment: null,
                TraceId: "0123456789abcdef0123456789abcdef",
                SpanId: spanId,
                ParentSpanId: null,
                CodeNamespace: className,
                CodeFunction: "",
                NtEventType: "method_exit",
                NtOutcome: outcome,
                NtReturnValue: returnValue,
                ExceptionType: exceptionType,
                ExceptionMessage: exceptionMessage);
        }
    }
}
