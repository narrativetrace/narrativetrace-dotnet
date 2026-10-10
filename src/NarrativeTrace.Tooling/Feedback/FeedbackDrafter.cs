// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// Turns a gathered <see cref="FeedbackReport"/> into something that may be shown to a person — or
/// into a refusal that names what is in the way.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: three things in one place, in this order, because the order is the safety property.
/// Normalise what can be normalised (a home path becomes <c>~</c>); run every <see cref="ValueFreeRule"/>
/// over every field that would be filed; render the two texts ONLY if nothing refused. A caller cannot
/// reach a URL or a body file past a violation, because <see cref="FeedbackDraft.Refused"/> has
/// neither.
/// </para>
/// <para>
/// <b>@sideEffects</b> None. This returns text; writing it to disk is the entry point's job, and that
/// separation is what lets every test here be hermetic.
/// </para>
/// </remarks>
public static class FeedbackDrafter
{
    /// <summary>
    /// Normalise, gate, and render.
    /// </summary>
    /// <exception cref="ArgumentNullException">when <paramref name="report"/> is null.</exception>
    public static FeedbackDraft Draft(FeedbackReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var normalised = Normalise(report);
        var violations = ValueFreeCheck.Violations(normalised.Fields());
        if (violations.Count > 0)
        {
            return new FeedbackDraft.Refused(violations);
        }

        var body = FeedbackRender.Body(normalised);
        return new FeedbackDraft.Drafted(normalised, FeedbackRender.Draft(body), body);
    }

    /// <summary>Every text field with its home directories rewritten to <c>~</c>.</summary>
    private static FeedbackReport Normalise(FeedbackReport report)
    {
        var narrative = report.Narrative;
        var attachments = report.Attachments;
        return report.ToBuilder()
            .Install(HomePaths.ToTilde(report.Install))
            .Step(HomePaths.ToTilde(report.Step))
            .Narrative(new ProblemNarrative(
                HomePaths.ToTilde(narrative.Did),
                HomePaths.ToTilde(narrative.Happened),
                HomePaths.ToTilde(narrative.Expected)))
            .Attachments(new Attachments(
                HomePaths.ToTilde(attachments.DoctorReport),
                HomePaths.ToTilde(attachments.DoctorUnavailable),
                HomePaths.ToTilde(attachments.StructuralTrace)))
            .Build();
    }
}
