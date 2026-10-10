// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// Command and verify literals for the problem-report skill — the <c>dotnet tool run</c> spelling of
/// the <c>feedback</c> verb, because a skill's closed command vocabulary is <c>dotnet</c> and
/// <c>git</c> and a skill may never instruct installing a global tool. Public: the Tier A2 replay
/// keys its executors off these exact constants rather than a second, hand-copied literal.
/// </summary>
/// <remarks>
/// <para>
/// Every command here is what an ADOPTER runs, in their own project. The angle-bracket placeholders
/// are the one thing the agent substitutes: the three sentences, the step and the language tag are the
/// report, and no literal can carry somebody else's report.
/// </para>
/// <para>
/// <b>@llmNote</b> There is deliberately no <c>gh</c> command here. The <c>gh issue create</c> line
/// is the verb's third channel, printed and never run, and it is outside the vocabulary — so the
/// skill never invokes it. No prose names a <c>.md</c> file either: the verb prints the paths it
/// wrote, and the citation lint treats a markdown filename this repository does not hold as an
/// external citation.
/// </para>
/// </remarks>
public static class FeedbackCommands
{
    private const string ReportFlags =
        "--category <category> --step <where it happened> --did <what you did> " +
        "--happened <what happened> --expected <what you expected> --language <language tag>";

    /// <summary>Runs the doctor and leaves its JSON report where the draft reads it.</summary>
    public const string RunDoctorReport = "dotnet tool run dotnet-narrativetrace doctor --report";

    /// <summary>Drafts the report, checks it carries no values, and prints the whole thing.</summary>
    public const string DraftReport = "dotnet tool run dotnet-narrativetrace feedback draft " + ReportFlags;

    /// <summary>Prints the pre-filled issue-form URL for the same report, once the user has said yes.</summary>
    public const string PrintUrl = "dotnet tool run dotnet-narrativetrace feedback url " + ReportFlags;

    /// <summary>The doctor report exists, so the draft can carry it.</summary>
    public const string VerifyDoctorReportWritten =
        "the command left the doctor's JSON report under TestResults/narrativetrace, so the report " +
        "can carry it; a project whose doctor cannot run is reported under the prompt or library " +
        "category instead";

    /// <summary>The draft was written, or a rule refused it and named the field to fix.</summary>
    public const string VerifyDraftWritten =
        "the command exited 0 and wrote the draft and body files it names — a run that named a " +
        "vf.* rule instead wrote neither, and the field it named is what to fix";

    /// <summary>The whole draft is in the reply.</summary>
    public static readonly string VerifyDraftShown = ApprovalGate.VerifyShownWhole(
        "the draft the command printed",
        included: ", attachments included",
        notInstead: " and not a parenthetical standing in for an attachment");

    /// <summary>The reply ends on the question.</summary>
    public static readonly string VerifyQuestionAsked = ApprovalGate.VerifyAskedThenStopped(
        "Do not print the issue URL or run gh before the user says yes — showing the URL is the filing.");

    /// <summary>The URL and the body file are in the reply.</summary>
    public const string VerifyUrlPrinted =
        "the printed URL is in the reply, together with the name of the body file the command " +
        "printed, to paste into the form's last box";
}
