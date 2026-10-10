// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// One place that builds a valid, value-free report, so a test that cares about one field does not
/// restate the other seven.
/// </summary>
internal static class FeedbackFixtures
{
    /// <summary>
    /// A doctor report that NAMES the redaction marker, exactly as the real doctor's JSON does — the
    /// generated artifact's own bytes, not a paraphrase. The marker here is what proves the field-
    /// scoped exemption end to end: a paraphrase with no marker would dodge the very defect it exists
    /// to expose (Java milestone 2, defect #1).
    /// </summary>
    internal const string DoctorReportJson =
        "{\"findings\":[{\"id\":\"trap.redaction-proof\",\"status\":\"pass\","
        + "\"message\":\"a test asserts the literal \\\"[REDACTED]\\\" marker\","
        + "\"fix\":\"assert the literal \\\"[REDACTED]\\\" in a redaction test\"}],\"exitCode\":0}";

    internal const string StructuralTrace =
        "scenario: order is placed\n- OrderService.placeOrder(customerId, total) → value\n";

    internal static FeedbackReport.Builder ValidReportBuilder() =>
        FeedbackReport.NewBuilder()
            .Category(FeedbackCategory.Doctor)
            .Install("NarrativeTrace.Core 1.0.0")
            .Step("trap.redaction-proof")
            .Narrative(new ProblemNarrative(
                "ran the doctor twice",
                "the fix did not clear the finding",
                "the finding should clear once a redaction test asserts the marker"))
            .Language("en")
            .Agent(new AgentIdentity("example-agent", "example-model"))
            .Attachments(Attachments.Of(DoctorReportJson, StructuralTrace));

    internal static FeedbackReport ValidReport() => ValidReportBuilder().Build();
}
