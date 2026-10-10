// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The two texts a cleared report becomes: the body that gets filed, and the draft a person reads
/// before deciding to file it.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: one renderer, so the draft cannot say something the body does not. The draft CONTAINS the
/// body verbatim (<see cref="FeedbackDraft.Drafted"/> refuses to exist otherwise) because approving a
/// draft has to mean approving what is filed, byte for byte, and a summary is not that.
/// </para>
/// <para>
/// <b>@llmNote</b> Fences are four backticks, not three. An attached doctor report or structural trace
/// is somebody else's text, and a three-backtick fence around text that happens to contain three
/// backticks ends the block early and spills the rest into the issue as prose.
/// </para>
/// </remarks>
public static class FeedbackRender
{
    /// <summary>The note the draft closes with — what filing publicly actually means (design D5).</summary>
    public const string PrivacyNote =
        "Filing on GitHub is public, under your own account, and shows that this project uses"
        + " NarrativeTrace. Nothing is sent anywhere until you choose to file it.";

    private const string Fence = "````";

    /// <summary>The report in Markdown: what gets filed, with nothing in it about the process.</summary>
    public static string Body(FeedbackReport report)
    {
        var builder = new StringBuilder();
        AppendFacts(builder, report);
        AppendNarrative(builder, report.Narrative);
        AppendAttachments(builder, report.Attachments);
        return builder.ToString();
    }

    /// <summary>The body, framed by what a person needs in order to decide: the two choices and the note.</summary>
    public static string Draft(string body) =>
        "# NarrativeTrace problem report (draft — nothing has been filed)\n\n"
        + body
        + "\n---\n\n"
        + PrivacyNote
        + "\n";

    private static void AppendFacts(StringBuilder builder, FeedbackReport report)
    {
        var agent = report.Agent.Describe();
        builder.Append("- runtime: ").Append(report.Runtime)
            .Append("\n- category: ").Append(report.Category.Id)
            .Append("\n- install: ").Append(report.Install)
            .Append("\n- step: ").Append(report.Step)
            .Append("\n- language: ").Append(report.Language)
            .Append("\n- agent: ").Append(agent.Length == 0 ? "not reported" : agent)
            .Append("\n\n");
    }

    private static void AppendNarrative(StringBuilder builder, ProblemNarrative narrative)
    {
        builder.Append("## What I did\n\n").Append(narrative.Did)
            .Append("\n\n## What happened\n\n").Append(narrative.Happened)
            .Append("\n\n## What I expected\n\n").Append(narrative.Expected)
            .Append("\n\n");
    }

    private static void AppendAttachments(StringBuilder builder, Attachments attachments)
    {
        builder.Append("## Doctor report\n\n");
        if (attachments.HasDoctorReport())
        {
            builder.Append(Fence).Append("json\n").Append(attachments.DoctorReport.Trim())
                .Append('\n').Append(Fence).Append("\n\n");
        }
        else
        {
            builder.Append("No doctor report: ").Append(attachments.DoctorUnavailable).Append("\n\n");
        }

        builder.Append("## Structural trace\n\n");
        if (attachments.HasStructuralTrace())
        {
            builder.Append(Fence).Append('\n').Append(attachments.StructuralTrace.Trim())
                .Append('\n').Append(Fence).Append('\n');
        }
        else
        {
            builder.Append("No structural trace was attached.\n");
        }
    }
}
