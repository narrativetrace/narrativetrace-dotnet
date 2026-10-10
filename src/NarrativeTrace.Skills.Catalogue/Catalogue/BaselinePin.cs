// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// The pin — a structural trace promoted to a committed <c>.approved.nt</c> behind the user's yes — as
/// the steps every skill that pins renders: approval mode on, run and show, ask and stop, promote.
/// </summary>
/// <remarks>
/// <para>INTENT: the verify skill pins a flow it checked and the debug skill pins the regression it
/// fixed; it is the same act on the same artifact behind the same gate (<see cref="ApprovalGate"/>),
/// so it is written once.</para>
/// <para><b>@llmNote</b> The approval run is the whole suite, never the one test the skill ran:
/// approval mode compares every test that writes its artifacts, so a one-test run leaves every other
/// such test without a baseline and the suite red (found by Java's trials, cross-port item 1).</para>
/// </remarks>
public static class BaselinePin
{
    private const string Listings = "tests/NarrativeTrace.Examples.SixtySeconds.Tests/";

    /// <summary>When approval mode is off — the two switch steps apply only then.</summary>
    public const string WhenApprovalModeIsOff =
        "if approval mode is off — the test project references no run settings that set " +
        "NARRATIVETRACE_APPROVAL, or the doctor's config.approval-mode finding fails; switching it on is part " +
        "of the pin, not a change beyond the task, so do it now without asking first — the one question comes " +
        "after the review copy is shown; when it is already on, go straight to the run";

    /// <summary>The pin's "always" rule: the whole review copy is shown before the question.</summary>
    public static readonly ReasonedRule ShowTheWholeReceived = ApprovalGate.ShowTheWholeBeforeAsking(
        ".received.nt",
        "the baseline becomes the contract every later change is held to, and a person can only approve " +
        "what they have actually read");

    /// <summary>The pin's "never" rules, in the order the pages list them.</summary>
    public static readonly IReadOnlyList<ReasonedRule> Never =
    [
        ApprovalGate.NeverInTheTurnThatAsked("promote a baseline"),
        ApprovalGate.NeverEditAfterShowing(".received.nt", "promoted", "run is rendered"),
        new ReasonedRule("Never commit a .received.nt", "it is the review copy; the committed contract is the .approved.nt"),
    ];

    /// <summary>
    /// The pin's steps, in the order the reply is written: the switch, the run and the review copy,
    /// <paramref name="report"/>, the question, and — in the user's next turn — the promotion. The
    /// skill's report sits between the review copy and the question because that is where the gate
    /// puts it; a page that listed it after the promotion was read as "report last" (Tier B, batch 2).
    /// </summary>
    public static IReadOnlyList<SkillStep> Steps(SkillStep report) =>
        [ApprovalModeOn(), ReferenceTheRunSettings(), RunAndShow(), report, AskThenStop(), Promote()];

    private static SkillStep ApprovalModeOn() => new(
        Title: "Turn approval mode on",
        Body: new SnippetStep(Listings + "approval.runsettings", "xml"),
        Verify: "the test project's directory carries this file as approval.runsettings",
        Condition: WhenApprovalModeIsOff);

    private static SkillStep AskThenStop() => new(
        Title: "Ask once whether to pin it, then stop the turn",
        Body: new CommandStep([]),
        Verify: ApprovalGate.VerifyAskedThenStopped(
            "Do not run the approve verb before the user says yes — promoting is the pinning. And " +
            "everything else — the report, every caveat, what you will do on a yes — goes before the " +
            "question; the question is the reply's last line, with no sentence after it: end the reply with " +
            "exactly the line " + PinQuestion + " (in the user's language)."));

    /// <summary>The literal last line of the asking reply — a cheap model given the line writes the line.</summary>
    public const string PinQuestion = "\"Shall I pin this as the approved baseline?\"";

    private static SkillStep Promote() => new(
        Title: "Promote what was shown, and nothing else",
        Body: new CommandStep([ClarityCommands.RestoreTool, VerifyCommands.Approve]),
        Verify: "each .approved.nt now holds exactly the text that was shown and no .received.nt is left " +
            "beside it — git status --short lists the new or changed .approved.nt files under narratives/ " +
            "and nothing else of the pin's; those are what get committed — and dotnet test passes: the " +
            "suite is green again after the promotion");

    private static SkillStep ReferenceTheRunSettings() => new(
        Title: "Reference the run settings from the test project",
        Body: new SnippetStep(Listings + "NarrativeTrace.Examples.SixtySeconds.Tests.csproj", "xml", Region: "approval-mode"),
        Verify: "the test project's .csproj carries the PropertyGroup above, and .gitignore carries the line " +
            "**/*.received.nt so a review copy is never committed",
        Failure:
        [
            new FailureNote(
                Symptom: "the suite still passes and no .received.nt appears under narratives/",
                Cause: "the run settings are not referenced, or no test calls WriteArtifacts, so nothing is compared",
                Fix: "check the doctor's config.approval-mode finding and apply its fix; write the flow test as the " +
                    "listing above does, then run the suite again"),
        ],
        Condition: WhenApprovalModeIsOff);

    private static SkillStep RunAndShow() => new(
        Title: "Run the suite in approval mode and show every .received.nt",
        Body: new CommandStep([VerifyCommands.RunTheSuite]),
        Verify: "this runs in the same turn as the structural read, before the report — the pin does not wait " +
            "for a yes, only the promotion does; approval mode compares every test that writes its artifacts, " +
            "not only the one this skill ran, " +
            "so the run that writes the review copies is the whole suite; the first run of a test with no " +
            "baseline fails on purpose — that failure is what writes its review copy, as " +
            VerifyCommands.ApprovedDir + ".received.nt beside the test project — and " +
            ApprovalGate.VerifyShownWhole("each .received.nt that run wrote") +
            " — it holds names and shape and no value, which is why it is safe to commit once approved; " +
            "where a .approved.nt already existed, the reply also names what the delta changed, by span id, " +
            "in the program's own words, and whether it was meant");
}
