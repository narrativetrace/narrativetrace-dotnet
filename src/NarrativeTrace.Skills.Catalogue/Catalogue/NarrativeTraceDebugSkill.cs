// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// <c>narrativetrace-debug</c>: finds the cause of a wrong result by reading what the code did with the
/// values, span by span, fixes it where it diverges, and pins the reproduction behind the user's yes.
/// </summary>
/// <remarks>
/// <para>Starts from a symptom, not a change (Phase 7 D4): reproduce with the smallest input; localize
/// by reading — the first span whose value or branch diverges, named by its id before any code changes;
/// bisect by span, not by file; fix in that span; check the structural trace moved nowhere else; keep
/// the reproduction as the regression test and pin its shape (<see cref="BaselinePin"/>); report the
/// root cause by span id. It renders the same <see cref="TraceReading"/> sections as
/// <see cref="NarrativeTraceVerifySkill"/>, so the two skills read a trace the same way.</para>
/// <para><b>@llmNote</b> Never narrows with <c>[NotTraced]</c>: in this runtime it REDACTS a value (the
/// proxy captures the marker instead of the argument), it does not scope a trace — so the bisect step
/// wraps one more collaborator instead. Declares no allowed tools, for the same reason as verify.</para>
/// </remarks>
public static class NarrativeTraceDebugSkill
{
    private const string Md = "the .md narrative under " + VerifyCommands.OutputDir + "/traces";

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "narrativetrace-debug",
        SkillClass: SkillClass.Guided,
        Description:
            "Finds the cause of a wrong result in a .NET project by reading what the code did with the values, " +
            "not by stepping through it. Use when a symptom is reported — a wrong amount, a wrong id, a call in " +
            "the wrong order, a test that fails with a value nobody expected. Reproduces it with the smallest " +
            "input and NarrativeTrace on, finds the first span where a value diverges and names it by its span " +
            "id (the sequence diagram first when threads are involved), narrows to that span's sub-tree, fixes it " +
            "there and checks the structural trace shows nothing else moved, pins the reproduction as a " +
            "regression test and an approval baseline behind your yes, and reports the root cause by span id. " +
            "Hands a defect in NarrativeTrace itself to narrativetrace-feedback. Say 'debug this with the trace', " +
            "'find where this value goes wrong', or 'why is this result wrong' to invoke it.",
        WhenToUse:
            "Non-obvious triggers: a support ticket quoting a wrong amount or total; a rounding, currency or " +
            "timezone difference between what was expected and what happened; a value that is right going into " +
            "a service and wrong coming out; a flaky ordering between threads.",
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps: [.. Find(), .. Fix(), .. BaselinePin.Steps(Report())],
        Always:
        [
            TraceReading.CiteSpanIds,
            new ReasonedRule(
                "Narrow to one span before reading its values",
                "debugging is where values pay for themselves, but only on the span that diverged — a whole trace " +
                "of values buries the one that matters"),
            BaselinePin.ShowTheWholeReceived,
        ],
        Never:
        [
            new ReasonedRule(
                "Never change code before the diverging span is named",
                "a fix made before the trace says where the value went wrong is a guess, and a guess that turns " +
                "the test green hides the defect it missed"),
            new ReasonedRule(
                "Never make the symptom go away somewhere other than the diverging span",
                "a correction downstream, a caught exception or a changed expectation silences the symptom and " +
                "leaves the defect for the next caller of that span"),
            TraceReading.NeverRedactionOff,
            .. BaselinePin.Never,
        ],
        AllowedTools: [])
    {
        Sections = [TraceReading.Flavours, TraceReading.Shapes],
    };

    private static IEnumerable<SkillStep> Find() =>
        [Reproduce(), SymptomInTheValues(), SequenceDiagramFirst(), Localize(), Bisect(), HandOff()];

    private static IEnumerable<SkillStep> Fix() => [FixInTheSpan(), NothingElseMoved(), KeepTheReproduction()];

    private static SkillStep Reproduce() => new(
        Title: "Reproduce the symptom with tracing on",
        Body: new SnippetStep("tests/NarrativeTrace.Examples.SixtySeconds.Tests/PlaceOrderFlowTests.cs", "csharp"),
        Verify: DebugCommands.RunTheReproduction,
        Failure:
        [
            new FailureNote(
                Symptom: "no .md for the test appears under " + VerifyCommands.OutputDir + "/traces",
                Cause: "the test does not call WriteArtifacts, or the collaborators on the path are not wrapped with " +
                    "the fixture's context",
                Fix: "write the test as the listing does; run narrativetrace-doctor and apply its fix, then run the " +
                    "test again"),
        ],
        Condition: "a test already drives the path with the input from the symptom, run that one; otherwise write " +
            "the smallest one, as below — the reported input, through the real collaborators each wrapped with " +
            "the fixture's context, asserting the value the symptom says should have come out");

    private static SkillStep SymptomInTheValues() => new(
        Title: "Find the symptom in the values",
        Body: new CommandStep([]),
        Verify: "the reported value is in this run's " + Md + ", or the reproducing test fails on it — a symptom " +
            "that does not reproduce is said so, and the loop stops here");

    private static SkillStep SequenceDiagramFirst() => new(
        Title: "Across threads, read the sequence diagram first",
        Body: new CommandStep([]),
        Verify: "the reproduction's .mmd under " + VerifyCommands.OutputDir + "/diagrams was read before any span's " +
            "values, and the reply says which call ran before which across the threads, by the span id in each " +
            "call's note — the span it points to is the one localized next",
        Condition: "only when the path crosses threads — the .nt shows a fork, async or fire-and-forget marker — or " +
            "the symptom is about order (a call that ran before or after another); otherwise go straight to " +
            "localizing");

    private static SkillStep Localize() => new(
        Title: "Localize by reading: name the first span where a value diverges",
        Body: new CommandStep([]),
        Verify: "the reproduction's spans in " + Md + " were read from the root down until the first one whose " +
            "inputs are what the symptom implies but whose result, the value it passes on, or the branch it takes " +
            "is not; the reply names that span by its id (#1.3) and the boundary — the collaborator, the parameter " +
            "or return, the value that arrived and the value that left — before any code is changed: by reading, " +
            "not by stepping through a debugger or adding prints");

    private static SkillStep Bisect() => new(
        Title: "Bisect by span, not by file",
        Body: new CommandStep([DebugCommands.RunTheReproduction]),
        Verify: "only the sub-tree under the diverging id was read on each re-run — the spans whose id begins with " +
            "it (#1.3, #1.3.1, #1.3.2) — and where the work inside that span is not traced, the collaborator it " +
            "calls was wrapped with NarrativeTraceProxy.Create in the reproducing test, as the listing above wraps " +
            "its service, and the run repeated, until the divergence sits in the smallest span that has it; never " +
            "[NotTraced] to narrow — in .NET it redacts a value, it does not scope a trace",
        Condition: "only when the value went into the diverging span right and came out wrong, and what happens in " +
            "between is more than that span's own few lines; otherwise the diverging span is the defect — go on " +
            "to the fix");

    private static SkillStep HandOff() => new(
        Title: "Hand a defect in NarrativeTrace itself to narrativetrace-feedback",
        Body: new CommandStep([]),
        Verify: "narrativetrace-feedback was started with the span id and the value-free .nt — never a value from " +
            "the trace — and the project's code was not changed to work around it; the loop ends with that hand-off",
        Condition: "only when the trace and the code disagree — a call the code makes has no span, a span shows a " +
            "value the code did not pass, one span has two ids in two flavours — or the diverging span is inside " +
            "NarrativeTrace; otherwise go on to the fix");

    private static SkillStep FixInTheSpan() => new(
        Title: "Fix it in the diverging span, re-run the same input, read the same span",
        Body: new CommandStep([DebugCommands.RunTheReproduction]),
        Verify: "the change is in the code of that span — the method the diverging id names, or what it calls — " +
            "and the reproducing test passes; the same span, by the same id, now carries the value the symptom " +
            "implied; a change anywhere else that makes the test pass silences the symptom and leaves the defect, " +
            "so it is undone");

    private static SkillStep NothingElseMoved() => new(
        Title: "Check that nothing else moved",
        Body: new CommandStep([]),
        Verify: "the fixed run's .nt under " + VerifyCommands.OutputDir + "/structural was read whole and compared, " +
            "line by line, with the call lines of the reproduction's .md — a red run writes no .nt (the .nt on disk " +
            "is the last green one), so the shape before the fix is the .md's calls and ids without their values: " +
            "the same calls in the same order under the same ids; a value fix moves no line of a value-free trace, " +
            "and every line that did move is named by its id in the reply and either explained by the fix or undone");

    private static SkillStep KeepTheReproduction() => new(
        Title: "Keep the reproduction as the regression test",
        Body: new CommandStep([]),
        Verify: "the reproducing test stays in the suite with the input from the symptom and asserts the value the " +
            "fixed span now carries — not only that nothing throws — so it fails when the fix is undone; its " +
            "structural trace is what the pin below makes the baseline");

    private static SkillStep Report() => new(
        Title: "Report the root cause as the trace showed it, before the question",
        Body: new CommandStep([]),
        Verify: "the root cause in the user's own terms — the span id where it diverged, the value that arrived and " +
            "the value that left, the branch it took — and what the fix changed in that span; every claim cites the " +
            "span id it rests on, from the .nt or .md read in this session: a claim without an id is not a claim. " +
            "The report is written in full before the pin question, where the gate puts it, and the closing reply " +
            "after the promotion names the span id again in its one-line summary of the cause");
}
