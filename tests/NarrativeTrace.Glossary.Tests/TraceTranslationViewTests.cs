// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>Mirrors Java's <c>TraceTranslationViewTest</c>.</summary>
public class TraceTranslationViewTests
{
    [Fact]
    public void Renders_the_enter_line_with_glossed_identifiers_and_verbatim_values()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }),
            Term("customer", TermKind.Word, new() { ["es"] = "cliente" }),
            Term("amount", TermKind.Word, new() { ["es"] = "importe" }));
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null,
                new ParameterCapture("customerId", "\"C-BROKE\"", false),
                new ParameterCapture("amount", "74.97", false)),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains(
            "PaymentService.cobrar (charge) (cliente: \"C-BROKE\", importe: 74.97)",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Untranslated_identifiers_render_as_is_and_feed_the_gaps_footer()
    {
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null,
                new ParameterCapture("customerId", "\"C-1\"", false)),
        };

        var text = BillingView(BillingGlossary()).Render(entries, "es");

        Assert.Contains("PaymentService.charge(customerId: \"C-1\")", text, StringComparison.Ordinal);
        Assert.Contains("Vacíos del glosario", text, StringComparison.Ordinal);
        Assert.Contains("- charge", text, StringComparison.Ordinal);
        Assert.Contains("- customer", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_exit_translates_the_exception_phrase_and_keeps_the_message_verbatim()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }),
            Term("insufficient fund", TermKind.NounPhrase,
                new() { ["es"] = "fondos insuficientes" }));
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null),
            Exit("PaymentService", "0000000000000001", "failure", null,
                "InsufficientFundsException", "balance 12.50 below required 74.97"),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains(
            "  !! fondos insuficientes [InsufficientFundsException]:"
            + " balance 12.50 below required 74.97",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Success_exit_renders_the_returns_label_and_the_verbatim_value()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null),
            Exit("PaymentService", "0000000000000001", "success", "\"TXN-1\"", null, null),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains("  -> devuelve \"TXN-1\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_calls_indent_by_parent_span_chain()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));
        var entries = new[]
        {
            Enter("OrderService", "placeOrder", "0000000000000001", null),
            Enter("PaymentService", "charge", "0000000000000002", "0000000000000001"),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains("\n  PaymentService.cobrar (charge) ()", text, StringComparison.Ordinal);
        Assert.Contains("OrderService.placeOrder(", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Narration_template_renders_its_locale_variant_filled_with_verbatim_values()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }),
            Term("Charging {amount} to {customerId}", TermKind.Template,
                new() { ["es"] = "Cobrando {amount} a {customerId}" }));
        var entries = new[]
        {
            EnterWithTemplate("PaymentService", "charge", "0000000000000001",
                "Charging {amount} to {customerId}",
                new ParameterCapture("amount", "74.97", false),
                new ParameterCapture("customerId", "\"C-1\"", false)),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains("\n  Cobrando 74.97 a \"C-1\"\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Narration_template_without_a_locale_variant_becomes_a_gap_and_renders_no_line()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));
        var entries = new[]
        {
            EnterWithTemplate("PaymentService", "charge", "0000000000000001",
                "Charging {amount} to {customerId}",
                new ParameterCapture("amount", "74.97", false)),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.DoesNotContain("Cobrando", text, StringComparison.Ordinal);
        Assert.Contains("- Charging {amount} to {customerId}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Incomplete_exit_renders_the_incomplete_label()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null),
            Exit("PaymentService", "0000000000000001", "incomplete", null, null, null),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains("  .. incompleto", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Redacted_parameter_values_pass_through_verbatim()
    {
        var glossary = BillingGlossary(
            Term("password", TermKind.Word, new() { ["es"] = "contraseña" }));
        var entries = new[]
        {
            Enter("AuthService", "login", "0000000000000001", null,
                new ParameterCapture("password", "[REDACTED]", true)),
        };

        var text = BillingView(glossary).Render(entries, "es");

        Assert.Contains("contraseña: [REDACTED]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Untranslated_exception_phrase_falls_back_to_the_original_type_and_becomes_a_gap()
    {
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null),
            Exit("PaymentService", "0000000000000001", "failure", null,
                "InsufficientFundsException", "balance low"),
        };

        var text = BillingView(BillingGlossary()).Render(entries, "es");

        Assert.Contains("  !! InsufficientFundsException: balance low", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[InsufficientFundsException]", text, StringComparison.Ordinal);
        Assert.Contains("- insufficient fund", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Suffix_only_exception_type_renders_as_is()
    {
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null),
            Exit("PaymentService", "0000000000000001", "failure", null, "Exception", "boom"),
        };

        var text = BillingView(BillingGlossary()).Render(entries, "es");

        Assert.Contains("  !! Exception: boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_without_an_exception_type_still_renders_the_message()
    {
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null),
            Exit("PaymentService", "0000000000000001", "failure", null, null, "boom"),
        };

        var text = BillingView(BillingGlossary()).Render(entries, "es");

        Assert.Contains("  !! : boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Bare_id_parameter_keeps_its_name()
    {
        var entries = new[]
        {
            Enter("PaymentService", "charge", "0000000000000001", null,
                new ParameterCapture("id", "\"1\"", false)),
        };

        var text = BillingView(BillingGlossary()).Render(entries, "es");

        Assert.Contains("(id: \"1\")", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>code.function</c> is a schema-required string with no shape
    /// constraint, so a name that normalizes to nothing is a valid canonical
    /// entry. A renderer that a live subscriber drives has to stay total over
    /// its own wire format — this one renders such a name verbatim rather
    /// than throwing into the pipeline's fan-out.
    /// </summary>
    [Theory]
    [InlineData("$$$")]
    [InlineData("__")]
    [InlineData("123")]
    public void Unreadable_function_name_renders_verbatim_rather_than_throwing(string function)
    {
        var entries = new[] { Enter("PaymentService", function, "0000000000000001", null) };

        var text = BillingView(BillingGlossary()).Render(entries, "es");

        Assert.Contains($"PaymentService.{function}()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_null_glossary_or_namespace_resolver()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TraceTranslationView(null!, _ => null));
        Assert.Throws<ArgumentNullException>(
            () => new TraceTranslationView(BillingGlossary(), null!));
    }

    [Fact]
    public void Rejects_null_entries_and_a_blank_locale()
    {
        var view = BillingView(BillingGlossary());

        Assert.Throws<ArgumentNullException>(() => view.Render(null!, "es"));
        Assert.Throws<ArgumentException>(() => view.Render([], " "));
    }

    [Fact]
    public void Single_context_glossary_translates_even_without_namespace_information()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));
        var view = new TraceTranslationView(glossary, _ => null);

        var text = view.Render(
            [Enter("PaymentService", "charge", "0000000000000001", null)], "es");

        Assert.Contains("PaymentService.cobrar (charge)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_context_glossary_stays_strict_without_namespace_information()
    {
        var view = new TraceTranslationView(
            BillingAndInsuranceGlossary(
                Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" })),
            _ => null);

        var text = view.Render(
            [Enter("PaymentService", "charge", "0000000000000001", null)], "es");

        Assert.Contains("PaymentService.charge(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cobrar", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_entry_renders_a_single_enter_against_mutable_state()
    {
        var view = BillingView(BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" })));
        var state = view.NewRenderState("es");

        var text = view.RenderEntry(
            Enter("PaymentService", "charge", "0000000000000001", null), state);

        Assert.Equal("PaymentService.cobrar (charge) ()\n", text);
    }

    [Fact]
    public void Render_entry_accumulates_depth_across_calls_on_the_same_state()
    {
        var view = BillingView(BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" })));
        var state = view.NewRenderState("es");

        view.RenderEntry(Enter("OrderService", "placeOrder", "0000000000000001", null), state);
        var child = view.RenderEntry(
            Enter("PaymentService", "charge", "0000000000000002", "0000000000000001"), state);
        var exit = view.RenderEntry(
            Exit("PaymentService", "0000000000000002", "success", "\"TXN-1\"", null, null), state);

        Assert.Equal("  PaymentService.cobrar (charge) ()\n", child);
        Assert.Equal("    -> devuelve \"TXN-1\"\n", exit);
    }

    [Fact]
    public void Gap_registry_accumulates_across_entries_and_renders_sorted_once()
    {
        var view = BillingView(BillingGlossary());
        var state = view.NewRenderState("es");

        var enterText = view.RenderEntry(
            Enter("PaymentService", "charge", "0000000000000001", null,
                new ParameterCapture("customerId", "\"C-1\"", false)),
            state);
        view.RenderEntry(Enter("OrderService", "audit", "0000000000000002", null), state);

        Assert.DoesNotContain("Vacíos del glosario", enterText, StringComparison.Ordinal);
        Assert.Equal(
            "\n---\nVacíos del glosario:\n- audit\n- charge\n- customer\n",
            view.RenderGapsFooter(state));
    }

    [Fact]
    public void Gaps_footer_is_empty_when_everything_translated()
    {
        var view = BillingView(BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" })));
        var state = view.NewRenderState("es");

        view.RenderEntry(Enter("PaymentService", "charge", "0000000000000001", null), state);

        Assert.Equal(string.Empty, view.RenderGapsFooter(state));
    }

    [Fact]
    public void Late_exit_whose_enter_was_never_seen_renders_at_depth_zero()
    {
        var view = BillingView(BillingGlossary());
        var state = view.NewRenderState("es");

        var text = view.RenderEntry(
            Exit("PaymentService", "00000000000000ff", "success", "\"TXN-9\"", null, null), state);

        Assert.Equal("  -> devuelve \"TXN-9\"\n", text);
    }

    [Fact]
    public void Late_enter_with_an_unknown_parent_renders_at_depth_zero()
    {
        var view = BillingView(BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" })));
        var state = view.NewRenderState("es");

        var text = view.RenderEntry(
            Enter("PaymentService", "charge", "0000000000000002", "00000000000000ff"), state);

        Assert.Equal("PaymentService.cobrar (charge) ()\n", text);
    }

    [Fact]
    public void Rejects_null_arguments_on_the_per_entry_api()
    {
        var view = BillingView(BillingGlossary());
        var state = view.NewRenderState("es");
        var entry = Enter("PaymentService", "charge", "0000000000000001", null);

        Assert.Throws<ArgumentNullException>(() => view.RenderEntry(null!, state));
        Assert.Throws<ArgumentNullException>(() => view.RenderEntry(entry, null!));
        Assert.Throws<ArgumentNullException>(() => view.RenderGapsFooter(null!));
        Assert.Throws<ArgumentException>(() => view.NewRenderState(" "));
    }

    [Fact]
    public void Glossary_guard_fires_before_the_namespace_resolver_guard()
    {
        var thrown = Assert.Throws<ArgumentNullException>(
            () => new TraceTranslationView(null!, null!));

        Assert.Equal("glossary", thrown.ParamName);
    }

    [Fact]
    public void Declared_unassigned_context_does_not_block_the_single_context_fallback()
    {
        var glossary = new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
                [ContextResolver.Unassigned] = new(ContextResolver.Unassigned, []),
            },
            [Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" })]);
        var view = new TraceTranslationView(glossary, _ => null);

        var text = view.Render(
            [Enter("PaymentService", "charge", "0000000000000001", null)], "es");

        Assert.Contains("PaymentService.cobrar (charge)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_context_glossary_translates_through_the_resolved_namespace()
    {
        var view = new TraceTranslationView(
            BillingAndInsuranceGlossary(
                InsuranceTerm("settle", TermKind.VerbPhrase, new() { ["es"] = "liquidar" })),
            _ => "Acme.Insurance");

        var text = view.Render(
            [Enter("ClaimService", "settle", "0000000000000001", null)], "es");

        Assert.Contains("ClaimService.liquidar (settle)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Captured_entry_namespace_overrides_the_namespace_resolver()
    {
        var view = new TraceTranslationView(
            BillingAndInsuranceGlossary(
                InsuranceTerm("settle", TermKind.VerbPhrase, new() { ["es"] = "liquidar" })),
            _ => "Acme.Billing");
        var entry = Enter("ClaimService", "settle", "0000000000000001", null)
            with
        { NtPackage = "Acme.Insurance" };

        var text = view.Render([entry], "es");

        Assert.Contains("ClaimService.liquidar (settle)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Captured_exit_namespace_overrides_the_resolver_for_exception_phrases()
    {
        var view = new TraceTranslationView(
            BillingAndInsuranceGlossary(
                InsuranceTerm("claim rejected", TermKind.NounPhrase,
                    new() { ["es"] = "siniestro rechazado" })),
            _ => "Acme.Billing");
        var failure = Exit(
                "ClaimService", "0000000000000001", "failure", null,
                "ClaimRejectedException", "boom")
            with
        { NtPackage = "Acme.Insurance" };

        var text = view.Render([failure], "es");

        Assert.Contains(
            "!! siniestro rechazado [ClaimRejectedException]: boom", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Fully_translated_trace_has_no_gaps_footer()
    {
        var glossary = BillingGlossary(
            Term("charge", TermKind.VerbPhrase, new() { ["es"] = "cobrar" }));

        var text = BillingView(glossary).Render(
            [Enter("PaymentService", "charge", "0000000000000001", null)], "es");

        Assert.DoesNotContain("Vacíos del glosario", text, StringComparison.Ordinal);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────

    internal static GlossaryTerm Term(
        string term, TermKind kind, Dictionary<string, string> translations)
    {
        return InContext(term, "billing", kind, translations);
    }

    internal static GlossaryTerm InsuranceTerm(
        string term, TermKind kind, Dictionary<string, string> translations)
    {
        return InContext(term, "insurance", kind, translations);
    }

    private static GlossaryTerm InContext(
        string term, string context, TermKind kind, Dictionary<string, string> translations)
    {
        return new GlossaryTerm(
            term, context, kind, TermStatus.Curated, null, translations, [], [],
            new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
    }

    internal static Glossary BillingGlossary(params GlossaryTerm[] terms)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
            },
            terms);
    }

    internal static Glossary BillingAndInsuranceGlossary(params GlossaryTerm[] terms)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
                ["insurance"] = new("insurance", ["Acme.Insurance"]),
            },
            terms);
    }

    internal static TraceTranslationView BillingView(Glossary glossary)
    {
        return new TraceTranslationView(glossary, _ => "Acme.Billing");
    }

    internal static CanonicalEntry Enter(
        string className,
        string method,
        string spanId,
        string? parentSpanId,
        params ParameterCapture[] parameters)
    {
        return new CanonicalEntry(
            Timestamp: "2026-08-14T10:00:00.000Z",
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
            NtParameters: parameters.Length == 0 ? null : parameters);
    }

    internal static CanonicalEntry EnterWithTemplate(
        string className,
        string method,
        string spanId,
        string template,
        params ParameterCapture[] parameters)
    {
        return Enter(className, method, spanId, null, parameters)
            with
        { NtNarrationTemplate = template };
    }

    internal static CanonicalEntry Exit(
        string className,
        string spanId,
        string outcome,
        string? returnValue,
        string? exceptionType,
        string? exceptionMessage)
    {
        return new CanonicalEntry(
            Timestamp: "2026-08-14T10:00:00.100Z",
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
