// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// <c>narrativetrace-feedback</c>: reports a defect in NarrativeTrace itself, with the user's
/// approval and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The shape is the whole point: draft, SHOW the draft in full, ask one question, stop the turn.
/// Filing is public and permanent, so the decision has to be the user's in a turn of their own —
/// not inferred from the turn that asked, and not from a summary of a report they never saw.
/// </para>
/// <para>
/// <b>@llmNote</b> This skill declares NO allowed tools, and that is a safety property rather than
/// an omission. A rendered page's <c>allowed-tools</c> line grants its listed tools for the turn that
/// loads the skill, without prompting — so a skill that declared <c>dotnet</c> would pre-approve its
/// own reporting command. <see cref="SkillLints.PublishingNotPreApprovedViolations"/> fails the build
/// if that is ever added back.
/// </para>
/// <para>The gate's wording is <see cref="ApprovalGate"/>'s, shared with the verify and debug
/// skills' pin step.</para>
/// </remarks>
public static class NarrativeTraceFeedbackSkill
{
    private const string Fixture = "examples/NarrativeTrace.Examples.SixtySeconds";

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "narrativetrace-feedback",
        SkillClass: SkillClass.Guided,
        Description:
            "Reports a defect in NarrativeTrace itself — the library, the doctor, an agent skill, " +
            "or the published install prompt. Use when a doctor finding is wrong or its fix does " +
            "not work, when a skill step cannot be followed or its verify cannot be met, when the " +
            "install prompt is wrong, or when the library misbehaves and the project is " +
            "configured correctly. Drafts the report from this project (the install coordinates, " +
            "the doctor's own JSON report, and at most one structural trace), refuses to write " +
            "one that carries a value from your traces and names the rule that refused it, shows " +
            "you the whole draft, and then asks once whether to file it publicly. Files nothing " +
            "without your answer and sends nothing anywhere. Say \"report this to " +
            "NarrativeTrace\", \"the doctor's fix did not work\", or \"file a bug about this " +
            "skill\" to invoke it.",
        WhenToUse:
            "Non-obvious triggers: a doctor fix that leaves the same finding failing; a skill " +
            "step whose verify cannot be met on a correctly configured project; wording in the " +
            "install prompt that led somewhere wrong.",
        Fixture: Fixture,
        Steps:
        [
            new SkillStep(
                Title: "Gather what the report needs",
                Body: new CommandStep([FeedbackCommands.RunDoctorReport]),
                Verify: FeedbackCommands.VerifyDoctorReportWritten,
                Failure:
                [
                    new FailureNote(
                        Symptom: "command not found: dotnet-narrativetrace",
                        Cause: "NarrativeTrace.Cli is not installed as a local tool in this project yet",
                        Fix: "report under the prompt or library category without a doctor report, or run " +
                            "the add-narrative-tracing skill's install step first"),
                ]),
            new SkillStep(
                Title: "Draft the report and let the gate check it",
                Body: new CommandStep([FeedbackCommands.DraftReport]),
                Verify: FeedbackCommands.VerifyDraftWritten,
                Failure:
                [
                    new FailureNote(
                        Symptom: "the command exits 2 naming a vf.* rule",
                        Cause: "a field carries a value from this project's own run — a rendered call " +
                            "line, an elapsed time, a credential-shaped string, an address",
                        Fix: "rewrite that one field to describe what happened instead of pasting it, " +
                            "and draft again; never work around the rule by moving the text to another field"),
                ]),
            new SkillStep(
                Title: "Show the whole draft, not a summary of it",
                Body: new CommandStep([]),
                Verify: FeedbackCommands.VerifyDraftShown),
            new SkillStep(
                Title: "Ask once whether to file it, then stop the turn",
                Body: new CommandStep([]),
                Verify: FeedbackCommands.VerifyQuestionAsked),
            new SkillStep(
                Title: "Print the way to file it, and nothing else",
                Body: new CommandStep([FeedbackCommands.PrintUrl]),
                Verify: FeedbackCommands.VerifyUrlPrinted),
        ],
        Always:
        [
            ApprovalGate.ShowTheWholeBeforeAsking(
                "draft",
                "filing is public and permanent, and a person can only approve what they have actually read"),
            new ReasonedRule(
                "Ask in the user's own language, and write the report in it",
                "the report may be written in any language, and a question nobody understands is not a question"),
            new ReasonedRule(
                "Tell the user that filing is public, under their own account, before they answer",
                "a public issue shows that their project uses NarrativeTrace, and that is their decision " +
                "to make knowingly"),
        ],
        Never:
        [
            new ReasonedRule(
                "Never attach a rendered trace, a log file or a source file",
                "those carry the values from the user's own run; the structural trace carries the same " +
                "shape of the same call without any of them, and the verb attaches it on its own"),
            ApprovalGate.NeverInTheTurnThatAsked("file"),
            ApprovalGate.NeverEditAfterShowing("draft", "filed", "report is drafted"),
            new ReasonedRule(
                "Never open the URL or run the printed command",
                "submitting is the user's act, in their own browser or their own shell, under their own account"),
            new ReasonedRule(
                "Never route a rule's refusal around the gate",
                "a field that cannot be filed is a field to rewrite, not to move somewhere the rule does not look"),
        ],
        AllowedTools: []);
}
