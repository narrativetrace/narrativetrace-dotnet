// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// <c>add-narrativetrace-clarity</c>: install the clarity gate the way this runtime does it, read its
/// report, rename by its suggestions, and run it again until it is clean.
/// </summary>
/// <remarks>
/// <b>@llmNote</b> The skill pre-approves the whole command vocabulary like the other two setup
/// skills — it is not a publishing skill, and the Java page grants its build tool the same way.
/// "Clean" is the gate exiting zero on a tree whose flagged names were CHANGED, never on one where the
/// flagged code was removed or the threshold moved; the Never list says so because a cheap model
/// reaches for exactly those.
/// </remarks>
public static class AddNarrativeTraceClaritySkill
{
    private const string Fixture = "examples/NarrativeTrace.Examples.Clarity";

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "add-narrativetrace-clarity",
        SkillClass: SkillClass.Guided,
        Description:
            "Adds or verifies NarrativeTrace Clarity in a .NET project. Use when you want a first " +
            "static naming report, per-element explanations, or an explicit clarity quality gate. " +
            "Runs the real clarity-scan and clarity-check verbs, checks fresh nonempty JSON and " +
            "Markdown, applies the report's renames and re-runs until the gate is clean, and " +
            "preserves existing thresholds. Say \"add a clarity report\", \"run the clarity scan\", " +
            "\"explain our clarity scores\", or \"set up a clarity quality gate\" to invoke it; " +
            "route tracing itself being broken to narrativetrace-doctor.",
        WhenToUse:
            "A project has NarrativeTrace or needs a first clarity report, score explanation, or " +
            "optional build enforcement.",
        Fixture: Fixture,
        Steps:
        [
            new SkillStep(
                Title: "Inspect the existing project setup",
                Body: new CommandStep([]),
                Verify:
                    "Read the target project file's target framework, NarrativeTrace references, tool " +
                    "manifest, glossary, and any ClarityMinScore, ClarityMaxHighIssues or ClarityWarnOnly " +
                    "property; preserve them. Run commands in the project's own directory.\n\n" +
                    "Choose the path that matches the request:\n" +
                    "- First naming report: scan, read both reports, and explain them.\n" +
                    "- Names the gate flags: rename by the report's suggestions and re-run until it is clean.\n" +
                    "- Build enforcement: only when the user asks for it."),
            new SkillStep(
                Title: "Make the clarity gate available to this project",
                Body: new CommandStep([ClarityCommands.RestoreTool]),
                Verify: "the restore succeeded and the tool manifest lists NarrativeTrace.Cli",
                Failure:
                [
                    new FailureNote(
                        Symptom: "the restore finds no tool manifest",
                        Cause: "this project has never installed NarrativeTrace.Cli as a local tool",
                        Fix: "run `dotnet new tool-manifest` and `dotnet tool install NarrativeTrace.Cli`, " +
                            "or run the add-narrative-tracing skill's install step first"),
                ]),
            new SkillStep(
                Title: "Build, then run a static scan for a first naming report",
                Body: new CommandStep([ClarityCommands.Build, ClarityCommands.Scan]),
                Verify: ClarityCommands.VerifyScanWritten,
                Failure:
                [
                    new FailureNote(
                        Symptom: "the scan exits 1 naming an assembly that was not found",
                        Cause: "the assembly path does not match this project's target framework or configuration",
                        Fix: "substitute the real path under the project's Release output and run the scan again"),
                ]),
            new SkillStep(
                Title: "Check both report artifacts and read their contents",
                Body: new CommandStep([]),
                Verify:
                    $"{ClarityCommands.JsonReportPath} and the Markdown report beside it in " +
                    $"{ClarityCommands.ReportDirectory}/ are fresh, nonempty, and were read; the JSON has a " +
                    "nonempty scenario array, and the Markdown ranks the issues with their suggestions. " +
                    "These scan reports are distinct from the clarity results a test run aggregates"),
            new SkillStep(
                Title: "Explain scores, notes, and coverage",
                Body: new CommandStep([]),
                Verify:
                    "Base the explanation on the report you read:\n\n" +
                    "- Present a short table of type, observed score, and the report's exact issue. Label your " +
                    "own interpretation or rename advice separately as suggestions.\n" +
                    "- overall (0–1) weights method names 30%, parameter names 25%, class names 20%, " +
                    "structural quality 15%, and cohesion 10%.\n" +
                    "- A static scan reads depth-1 reflection: it assesses names and parameter counts but cannot " +
                    "establish runtime call depth. A captured trace answers that.\n" +
                    "- An empty issues list means no scoring rule flagged an issue; it does not prove every " +
                    "name is unambiguous."),
            new SkillStep(
                Title: "Apply the report's renames, then run the gate again until it is clean",
                Body: new CommandStep([ClarityCommands.Build, ClarityCommands.Scan, ClarityCommands.Check]),
                Verify:
                    "the gate exits 0 on a tree where each flagged name was renamed to the report's suggested " +
                    "shape and every caller, test and reference was updated to match; if a rename would change " +
                    "a published API, stop and ask first",
                Failure:
                [
                    new FailureNote(
                        Symptom: "the gate still exits 1 after a rename",
                        Cause: "the new name is another generic verb or placeholder, or a flagged parameter " +
                            "was left behind",
                        Fix: "read the report again, name the member for what it does in this project's own " +
                            "domain words, and re-run the three commands"),
                ]),
            new SkillStep(
                Title: "Add build enforcement only when requested",
                Body: new CommandStep([ClarityCommands.BuildGate]),
                Verify:
                    "the build runs the check and fails on a HIGH issue; the target shells out to " +
                    "dotnet-narrativetrace on PATH, so it needs the tool installed globally, not only in a " +
                    "local manifest. Existing thresholds stay as they were",
                Failure:
                [
                    new FailureNote(
                        Symptom: "the build cannot start dotnet-narrativetrace",
                        Cause: "the tool is installed in a local manifest only, which never reaches PATH",
                        Fix: "keep the gate as the CLI command in CI, or install the tool globally on the build agent"),
                ]),
            new SkillStep(
                Title: "Hand missing tracing or output setup to the doctor",
                Body: new CommandStep([]),
                Verify:
                    "if the reports stay missing or empty, run narrativetrace-doctor for diagnosis. When the " +
                    "user asked for setup or repair, apply the identified fix and repeat the verification; " +
                    "do not stop after merely naming the doctor"),
        ],
        Always:
        [
            new ReasonedRule(
                "Start from fresh output and inspect both artifacts",
                "a successful command can still leave a stale or empty report"),
            new ReasonedRule(
                "Rename by the report's suggestion and update every caller in the same change",
                "a rename that leaves a caller behind trades a naming issue for a broken build"),
        ],
        Never:
        [
            new ReasonedRule(
                "Do not delete, comment out or exempt a flagged member to make the gate pass",
                "removing the evidence is not clean code; the gate is clean only when the names changed"),
            new ReasonedRule(
                "Do not lower, remove or replace an existing threshold to hide a failure",
                "enforcement is an explicit project choice and a failure is useful evidence"),
            new ReasonedRule(
                "Do not rename a published API without asking first",
                "a public member's name is a promise to other projects, and the gate cannot see them"),
            new ReasonedRule(
                "Report observed scores without promising a score for an untested rename",
                "a rename's score must be measured by another run"),
            new ReasonedRule(
                "Do not harvest a glossary merely to read existing vocabulary",
                "clarity reads a committed glossary; harvesting is a separate opt-in write"),
            new ReasonedRule(
                "Do not call a missing report successful",
                "the gate skips when the results file is absent, so artifact freshness must be checked"),
        ]);
}
