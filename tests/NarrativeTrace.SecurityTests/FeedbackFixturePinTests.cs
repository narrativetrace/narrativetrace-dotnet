// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// The Tier B value-free fixture plants a rendered trace carrying a canary, and the case is only
/// honest while that file is what the renderer really writes — a hand-written stand-in for a
/// generated artifact dodges exactly the defect it exists to expose (the doctor-report paraphrase
/// that hid the field-scoping defect in every port). This re-renders the same call through the
/// real <see cref="MarkdownRenderer"/> and holds the fixture's call line to it, and proves the
/// gate the case measures refuses what was planted.
/// </summary>
public sealed partial class FeedbackFixturePinTests
{
    private const string Canary = "ghp_NTCANARY0001";

    private static readonly string Planted = Path.Combine(
        RepositoryPath.Root(), "skills-evals", "fixtures", "feedback-value-free", "traces",
        "payment-charge-before-redaction.md");

    [Fact]
    public void The_planted_call_line_is_the_renderers_own_output_with_only_its_duration_masked()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        var payments = NarrativeTraceProxy.Create<IPaymentService>(
            new PaymentService(), context, new ProxyOptions(Redaction: RedactionPolicy.Disabled));
        payments.Charge("C-1234", Canary, "42.00");

        var rendered = CallLine(MarkdownRenderer.Render(context.CaptureTrace()));

        Assert.Equal(Masked(rendered), Masked(CallLine(File.ReadAllText(Planted))));
    }

    /// <summary>
    /// The case measures the gate: the planted line must be something the gate refuses, by the
    /// secret's name, by its shape, and by the rendered outcome and duration around it. Not by
    /// <c>vf.rendered-call</c> — the Markdown form's emphasis sits between the method name and its
    /// parenthesis, which that rule's master pattern does not cross (cross-port finding
    /// 2026-10-08-vf-rendered-call-misses-markdown-emphasis), so no test here claims it does.
    /// </summary>
    [Fact]
    public void The_value_free_gate_refuses_the_planted_call_line_by_name_and_by_shape()
    {
        var refusing = ValueFreeCheck.RulesRefusing(CallLine(File.ReadAllText(Planted))).Select(rule => rule.Id).ToList();

        Assert.Contains("vf.named-secret", refusing);
        Assert.Contains("vf.value-shape", refusing);
        Assert.Contains("vf.rendered-outcome", refusing);
        Assert.Contains("vf.duration", refusing);
        Assert.Contains("vf.value-shape", ValueFreeCheck.RulesRefusing(Canary).Select(rule => rule.Id));
    }

    private static string CallLine(string markdown) =>
        markdown.Split('\n').Single(line => line.StartsWith("- **IPaymentService.Charge**", StringComparison.Ordinal));

    private static string Masked(string line) => Duration().Replace(line, "— <duration>");

    [GeneratedRegex("— \\d+(\\.\\d+)?\\s?(ns|µs|ms|s)(?= #[0-9.]+$|$)")]
    private static partial Regex Duration();

    /// <summary>The fixture's own service boundary, byte for byte — the names are what the renderer prints.</summary>
    public interface IPaymentService
    {
        /// <summary>Charges a customer.</summary>
        string Charge(string customerId, string authToken, string amount);
    }

    private sealed class PaymentService : IPaymentService
    {
        public string Charge(string customerId, string authToken, string amount) => $"PAY-{customerId}";
    }
}
