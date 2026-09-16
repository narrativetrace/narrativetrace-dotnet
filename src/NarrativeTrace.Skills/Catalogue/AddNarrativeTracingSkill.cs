// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// <c>add-narrative-tracing</c>: setup, end to end. Install → first trace → send it to a
/// logger → hand off to <see cref="NarrativeTraceDoctorSkill"/> (agent-skills ruling 9: these
/// three steps are onboarding, not diagnosis, so they live here rather than on the doctor page).
/// </summary>
public static class AddNarrativeTracingSkill
{
    private const string Fixture = "examples/NarrativeTrace.Examples.SixtySeconds";

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "add-narrative-tracing",
        SkillClass: SkillClass.Guided,
        Description:
            "Adds NarrativeTrace to a .NET project end to end: installs the real toolchain with a " +
            "frozen restore, wraps a service and renders its first trace, sends the trace to a " +
            "logger with one call, and finishes by running the doctor CLI to confirm the install. " +
            "Use when asked to \"add narrative tracing\", \"set up NarrativeTrace\", \"wire up " +
            "tracing for this service\", \"instrument this .NET app with NarrativeTrace\", or " +
            "\"get a trace out of this code\".",
        WhenToUse: "A .NET project has no NarrativeTrace install yet and needs one wired up from scratch.",
        Fixture: Fixture,
        Steps:
        [
            new SkillStep(
                Title: "Install with the real toolchain — a frozen restore, never an ad hoc add",
                Body: new CommandStep(
                [
                    "dotnet tool install NarrativeTrace.Cli",
                    "dotnet add package NarrativeTrace.Proxy",
                    "dotnet restore --locked-mode",
                ]),
                Verify: SkillCommands.ToolchainChecksHold,
                Failure:
                [
                    new FailureNote(
                        Symptom: "`dotnet restore --locked-mode` fails with no lock file found",
                        Cause: "the project has no committed packages.lock.json yet",
                        Fix: "run `dotnet restore` once without --locked-mode to generate the lock " +
                            "file, commit it, then use --locked-mode from then on"),
                ]),
            new SkillStep(
                Title: "First trace: wrap the service, call it, render the tree, run it",
                Body: new SnippetStep(
                    Path: $"{Fixture}/Program.cs", Language: "csharp"),
                Verify:
                    "`dotnet run` prints a \"trace: <name> (<id>)\" line followed by the call tree",
                Failure:
                [
                    new FailureNote(
                        Symptom: "no trace line appears",
                        Cause: "the wrapped target was called directly instead of the proxy " +
                            "returned by NarrativeTraceProxy.Create<T>",
                        Fix: "call the proxy instance everywhere the service is used, never the " +
                            "wrapped target"),
                ]),
            new SkillStep(
                Title: "Send it to your logger — one call, same captured tree",
                Body: new SnippetStep(
                    Path: $"{Fixture}.WithLogger/Program.cs", Language: "csharp"),
                Verify: "the console logger prints one Information-level record per trace node",
                Failure: []),
            new SkillStep(
                Title: "Run the doctor and resolve every finding it reports",
                Body: new CommandStep(["dotnet tool run dotnet-narrativetrace doctor --json"]),
                Verify: SkillCommands.ExitCodeIsZero,
                Failure:
                [
                    new FailureNote(
                        Symptom: "the doctor CLI exits 1",
                        Cause: "at least one finding failed",
                        Fix: "read its \"fix\" field and doc URL, resolve it, then rerun doctor"),
                ]),
        ],
        Always:
        [
            new ReasonedRule(
                "Install with the project's own frozen restore, never a bare `dotnet add package` " +
                "left unlocked",
                "an unlocked restore can silently resolve a different version tomorrow than it did today"),
            new ReasonedRule(
                "Finish by running the doctor CLI",
                "it is the same tested check the narrativetrace-doctor skill uses — resolving its " +
                "findings here means starting from a clean baseline"),
        ],
        Never:
        [
            new ReasonedRule(
                "Never seed a fixed traceparent outside an example or test",
                "the constant exists so this page's output is reproducible — a real run adopts a " +
                "real inbound header or generates its own trace id"),
            new ReasonedRule(
                "Never call the wrapped target directly instead of the proxy",
                "only calls made through the proxy returned by NarrativeTraceProxy.Create<T> are captured"),
        ]);
}
