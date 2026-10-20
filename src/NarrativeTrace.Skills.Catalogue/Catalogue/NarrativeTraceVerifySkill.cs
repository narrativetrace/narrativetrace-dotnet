// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// <c>narrativetrace-verify</c>: reads what a change actually did before the agent says it is done,
/// and pins the result as an approval baseline behind the user's yes.
/// </summary>
/// <remarks>
/// <para>The loop runs after the tests are green and before the report: decide whether the change is
/// worth tracing and say so (a pure function is not); write the intent down BEFORE the run, because a
/// trace read against nothing confirms whatever happened; run the smallest real path; read the
/// value-free structural trace against the intent; open values only on the span that looks wrong;
/// fix and re-read; pin (<see cref="BaselinePin"/>); report what the trace showed, citing span ids.</para>
/// <para><b>@llmNote</b> This skill declares NO allowed tools, and that is a safety property: the
/// promotion runs through <c>dotnet</c>, so pre-approving it would pre-approve the promotion in the
/// turn the gate says must stop. <see cref="SkillLints.PromotionNotPreApprovedViolations"/> fails the
/// build if one is ever added.</para>
/// </remarks>
public static class NarrativeTraceVerifySkill
{
    /// <summary>
    /// Values are opened on a shape mismatch, OR on a shape match a symptom still points at — one span,
    /// chosen by the symptom.
    /// </summary>
    public const string OpenValuesWhen =
        "when the structural read named a span that does not match the intent, or the shape matches but a " +
        "symptom points at data — an assertion failed on a value, the output is wrong — then open the one " +
        "span the symptom points at; otherwise go on to the pin";

    /// <summary>The fix and the re-read happen only on a structural mismatch, or a wrong value step 5 found.</summary>
    public const string FixAndRerunWhen =
        "only when the structural read named a span that does not match the intent, or the value read in " +
        "step 5 was wrong; otherwise go on to the pin";

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "narrativetrace-verify",
        SkillClass: SkillClass.Guided,
        Description:
            "Verifies a change in a .NET project by reading what the code actually did before saying it is " +
            "done. Use after the tests are green and before reporting a change that crosses collaborators, " +
            "branches, retries, runs async, carries state between calls, or touches code not written in this " +
            "session — and skip it, saying why, for a pure function or a one-class edit. Writes the intent " +
            "down first, runs the smallest real path with NarrativeTrace on, reads the value-free structural " +
            "trace against the intent, opens values only on the span that looks wrong, fixes and re-reads, " +
            "then pins the flow as an approval baseline behind your yes and reports what the trace showed, " +
            "citing span ids. Say 'verify this change with the trace', 'check what the code actually did', " +
            "'did the flow do what I meant', or 'pin this flow as a baseline' to invoke it.",
        WhenToUse:
            "Non-obvious triggers: the suite is green but the change touched more call sites than it added; " +
            "a notification, payment or retry path changed; a .received.nt appeared after a test run; you " +
            "are about to write 'tests pass' as the whole report.",
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps: [.. Loop(), .. BaselinePin.Steps(Report())],
        Always:
        [
            TraceReading.CiteSpanIds,
            new ReasonedRule(
                "Use the cheapest flavour that answers the question",
                "the structural trace first and a value on one span only is what keeps the common case near zero tokens"),
            BaselinePin.ShowTheWholeReceived,
        ],
        Never:
        [
            new ReasonedRule(
                "Never report a change as done on green tests alone once this skill decided to trace",
                "the suite checks what someone thought to assert; the trace shows what the code did"),
            new ReasonedRule(
                "Never read a trace against nothing",
                "an intent written after the run bends to whatever happened — that is why it comes first"),
            TraceReading.NeverRedactionOff,
            .. BaselinePin.Never,
        ],
        AllowedTools: [])
    {
        Sections = [TraceReading.Flavours, TraceReading.Shapes],
    };

    private static IEnumerable<SkillStep> Loop() =>
        [Decide(), Intent(), Run(), ReadStructural(), OpenValues(), FixAndReread()];

    private static SkillStep Intent() => new(
        Title: "Write the intent down before running anything",
        Body: new CommandStep([]),
        Verify: "three to six lines in the reply, under the word Intent, written before the first traced run: " +
            "which collaborators the change touches, in which order, under which branch, how many times — " +
            "the oracle the trace is read against, never edited after the run");

    private static SkillStep ReadStructural() => new(
        Title: "Read the structural trace first, against the intent",
        Body: new CommandStep([]),
        Verify: "the .nt of the test just run — under " + VerifyCommands.OutputDir + "/structural in the test " +
            "project — was opened and read whole before any value was looked at, and the reply walks it " +
            "against the intent — calls, order, branch, multiplicity — naming every match and every " +
            "mismatch by its span id (#2.1); the shapes below are the checklist");

    private static SkillStep OpenValues() => new(
        Title: "Open values on the span that looks wrong, and only there",
        Body: new CommandStep([]),
        Verify: "only the flagged span was read in the .md narrative under " + VerifyCommands.OutputDir +
            "/traces, found by the id the .nt gave it — not the whole file; a [REDACTED] value stays redacted",
        Condition: OpenValuesWhen);

    private static SkillStep FixAndReread() => new(
        Title: "Fix, re-run, read again",
        Body: new CommandStep([VerifyCommands.RunThePath]),
        Verify: "the same test ran again after the fix and its new .nt was read whole: the span that was " +
            "wrong now matches the intent, and a fix that changed the shape was read again from the " +
            "structural read",
        Condition: FixAndRerunWhen);

    private static SkillStep Decide() => new(
        Title: "Decide whether to trace, and say so",
        Body: new CommandStep([]),
        Verify: "before anything runs, the reply says which it is: 'tracing: <the reason>' when the change " +
            "crosses two or more collaborators over a boundary, branches, retries, runs async or concurrently, " +
            "carries state between calls, touched more call sites than it added, or includes code not written " +
            "in this session; or 'skipping narrativetrace-verify: <a pure function | a one-class edit with no " +
            "collaborator | a flow one test already walks end to end>' — a skip ends the skill here, and that " +
            "sentence is the report. A whole flow's .nt is dozens of lines: cheap where the path is not " +
            "obvious, waste where it is");

    private static SkillStep Run() => new(
        Title: "Run the smallest real path with tracing on",
        Body: new SnippetStep("tests/NarrativeTrace.Examples.SixtySeconds.Tests/PlaceOrderFlowTests.cs", "csharp"),
        Verify: VerifyCommands.RunThePath,
        Failure:
        [
            new FailureNote(
                Symptom: "no .nt for the test appears under " + VerifyCommands.OutputDir + "/structural",
                Cause: VerifyCommands.NoTestWriteCause + ", the collaborators on the path are not wrapped with " +
                    "the fixture's context, or NARRATIVETRACE_OUTPUT=false turned the files off",
                Fix: "write the test as the listing does; run narrativetrace-doctor and apply its fix, then run the " +
                    "test again"),
        ],
        Condition: "the project already has a test that drives the changed path through its real collaborators " +
            "and writes its artifacts, run that one — again, if it already ran: a trace from a run made before " +
            "the Intent was written does not count; otherwise write the smallest one, as below");

    private static SkillStep Report() => new(
        Title: "Report what the trace showed, before the question",
        Body: new CommandStep([]),
        Verify: "two sentences on what the trace showed on the path this run took, every claim citing the span " +
            "id it rests on — a claim without an id is not a claim, and only ids in the .nt that was read " +
            "count — with the .nt attached or quoted; the report names the branches and inputs this run did " +
            "not exercise, because one trace shows one path, not every execution; 'tests pass' alone is not " +
            "the report. It is written in this reply, after the review copy " +
            "and before the pin question");
}
