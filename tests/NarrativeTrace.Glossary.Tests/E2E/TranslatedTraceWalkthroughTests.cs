// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests.E2E;

/// <summary>
/// Walkthrough 3 of the glossary plan, exercised from outside the production
/// assembly: a failed charge translated to Spanish, with values and the
/// runtime exception message byte-identical and original identifiers
/// greppable beside their glosses.
/// </summary>
public class TranslatedTraceWalkthroughTests
{
    [Fact]
    public void Walkthrough_three_translates_the_failed_charge_for_support()
    {
        var glossary = new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
            },
            [
                Term("charge", TermKind.VerbPhrase, "cobrar"),
                Term("customer", TermKind.Word, "cliente"),
                Term("amount", TermKind.Word, "importe"),
                Term("insufficient fund", TermKind.NounPhrase, "fondos insuficientes"),
            ]);
        var entries = new[]
        {
            Entry("method_enter", "PaymentService", "charge",
                [
                    new ParameterCapture("customerId", "\"C-BROKE\"", false),
                    new ParameterCapture("amount", "74.97", false),
                ],
                null, null, null),
            Entry("method_exit", "PaymentService", "", null, "failure",
                "InsufficientFundsException", "balance 12.50 below required 74.97"),
        };

        var text = new TraceTranslationView(glossary, _ => "Acme.Billing")
            .Render(entries, "es");

        Assert.Equal(
            "PaymentService.cobrar (charge) (cliente: \"C-BROKE\", importe: 74.97)\n"
            + "  !! fondos insuficientes [InsufficientFundsException]:"
            + " balance 12.50 below required 74.97\n",
            text);
    }

    private static GlossaryTerm Term(string term, TermKind kind, string spanish)
    {
        return new GlossaryTerm(
            term,
            "billing",
            kind,
            TermStatus.Curated,
            null,
            new Dictionary<string, string> { ["es"] = spanish },
            [],
            [],
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
    }

    private static CanonicalEntry Entry(
        string eventType,
        string className,
        string method,
        IReadOnlyList<ParameterCapture>? parameters,
        string? outcome,
        string? exceptionType,
        string? exceptionMessage)
    {
        return new CanonicalEntry(
            Timestamp: "2026-08-14T10:00:00.000Z",
            Level: exceptionType is not null ? "error" : "trace",
            Message: "message is never parsed",
            Service: "test",
            Environment: null,
            TraceId: "0123456789abcdef0123456789abcdef",
            SpanId: "0000000000000001",
            ParentSpanId: null,
            CodeNamespace: className,
            CodeFunction: method,
            NtEventType: eventType,
            NtOutcome: outcome,
            NtParameters: parameters,
            ExceptionType: exceptionType,
            ExceptionMessage: exceptionMessage);
    }
}
