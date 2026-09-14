// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// <c>narrativetrace-doctor</c>: read-only diagnosis. "Run the CLI, interpret, point at the
/// fix" plus the three steps that don't reduce to a CLI invocation — proving redaction, reading
/// a trace before trusting it, and the flagged approval-flow diff (agent-skills ruling 9: the
/// doctor page is diagnosis, not onboarding — installing and sending output to a logger live in
/// <see cref="AddNarrativeTracingSkill"/> instead).
/// </summary>
public static class NarrativeTraceDoctorSkill
{
    private const string Fixture = "examples/NarrativeTrace.Examples.SixtySeconds";

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "narrativetrace-doctor",
        SkillClass: SkillClass.Guided,
        Description:
            "Diagnoses a NarrativeTrace .NET install without changing anything: runs the doctor " +
            "CLI and explains every failing check and its fix, proves redaction with a real test, " +
            "opens the newest rendered trace before trusting any assertion against it, and — " +
            "flagged unstudied — walks an approval-trace diff. Use when traces aren't appearing, " +
            "output looks wrong, a value that should be redacted shows up in a trace, tests pass " +
            "but the trace looks off, or someone asks \"why isn't NarrativeTrace working\", \"is " +
            "my NarrativeTrace install broken\", \"run narrativetrace doctor\", \"check my " +
            "NarrativeTrace setup\", or \"diagnose narrative trace\".",
        WhenToUse:
            "A NarrativeTrace install already exists in the project and something needs " +
            "diagnosing — no traces are appearing, a check is failing, or a value that should be " +
            "redacted appears in output.",
        Fixture: Fixture,
        Steps:
        [
            new SkillStep(
                Title: "Run the doctor CLI and read its report — every failing check names a fix and a doc link",
                Body: new CommandStep(["dotnet tool run dotnet-narrativetrace doctor --json"]),
                Verify: SkillCommands.DoctorReportWellFormed,
                Failure:
                [
                    new FailureNote(
                        Symptom: "command not found: dotnet-narrativetrace",
                        Cause: "NarrativeTrace.Cli is not installed as a local tool in this project yet",
                        Fix: "run the add-narrative-tracing skill's install step, or " +
                            "`dotnet tool install NarrativeTrace.Cli`"),
                ]),
            new SkillStep(
                Title:
                    "Prove redaction in a test — render a call with a deny-listed parameter name " +
                    "and assert on the output, never just trust the deny-list",
                Body: new CommandStep(["dotnet test"]),
                Verify: SkillCommands.RedactionProofFindingPasses,
                Failure:
                [
                    new FailureNote(
                        Symptom: "trap.redaction-proof still fails after adding a test",
                        Cause: "the test asserts on a parameter name that isn't deny-listed, or the " +
                            "test project isn't discovered by `dotnet test`",
                        Fix: "use a deny-listed name like \"password\" or \"token\", and assert a " +
                            "neighboring, non-sensitive value is still present so an over-broad " +
                            "redaction also fails"),
                ]),
            new SkillStep(
                Title:
                    "Read the newest rendered trace under the output directory before writing any " +
                    "assertion against it",
                Body: new CommandStep(["dotnet test"])),
            new SkillStep(
                Title: "Approval flow: diff the structural trace, not just values",
                Body: new CommandStep(["dotnet tool run dotnet-narrativetrace doctor --json"]),
                Verify: SkillCommands.ApprovalTracesFindingPasses,
                Failure:
                [
                    new FailureNote(
                        Symptom: "a *.received.nt file sits beside its *.approved.nt baseline",
                        Cause: "a traced call's structure changed and approval mode caught it",
                        Fix: "review the diff, promote it once it's reviewed, or delete the " +
                            "*.received.nt file if the change was wrong — never commit it"),
                ],
                Flag: "unstudied — eval cell pending"),
        ],
        Always:
        [
            new ReasonedRule(
                "Run the doctor CLI before hand-diagnosing a NarrativeTrace problem",
                "the checks are tested and stable-ided; guessing re-derives what a check already verified"),
            new ReasonedRule(
                "Read the newest rendered file under the output directory before asserting against it",
                "a stale or wrong-directory trace makes any assertion against it meaningless"),
        ],
        Never:
        [
            new ReasonedRule(
                "Never commit a *.received.nt file",
                "it is an unreviewed diff, not a baseline — committing it hides the question it was raised to ask"),
            new ReasonedRule(
                "Never widen redaction until a neighboring, non-sensitive value is proven still present",
                "an over-broad deny-list silently hides real evidence, the same failure mode as under-redaction"),
        ]);
}
