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

    /// <summary>
    /// The first-trace step's condition (Phase 6, D2). Names no framework: whether the project already
    /// starts itself is read from its own entry point and the doctor's report, never from a list on this
    /// page. The snippet below is a whole console program; dropped into a project that already has an
    /// entry point it would replace the project's own deliverable with a demo.
    /// </summary>
    internal const string EntryPointRunsItself =
        "if the project already has an application entry point — a Main method or top-level program it " +
        "already runs, a web or application framework the doctor reports — do not add a demo program: run " +
        "the application the way it already runs, exercise one real boundary, and read that request's " +
        "trace; the verify below is for the console app, and in an existing application the step is done " +
        "when that request's trace is in the output; otherwise create the smallest console app as follows";

    /// <summary>
    /// The first-trace step's tests premise (B-92). Names no runner: whether the doctor reports a test
    /// integration for the project's runner is read from its report, never from a list on this page.
    /// </summary>
    internal const string FirstTraceHasTests =
        "if the project already has tests — a test project in the solution, a test SDK among its package " +
        "references — and the doctor reports a test integration for that runner, do not add a demo program: " +
        "run one existing test through that integration with output on, which writes that test's narrative " +
        "files, `.md` and `.nt`, under the integration's output directory — the files " +
        "narrativetrace-doctor, narrativetrace-verify, narrativetrace-debug and narrativetrace-explain read; " +
        "print the rendered file so the trace is also on the console; the step is done when that test's " +
        "files exist; a project whose runner has no integration keeps that runner and takes the console " +
        "path below";

    /// <summary>The console branch's closing sentence: what the program does not do, and why (B-92).</summary>
    internal const string FirstTraceWritesNothingWithoutTests =
        "; nothing is written to disk by this program — the application's log is the record once the " +
        "logging provider in the later step is wired; this page never adds a test runner or a test to a " +
        "project that has none";

    private const string FirstTraceCondition =
        FirstTraceHasTests + "; otherwise, " + EntryPointRunsItself + FirstTraceWritesNothingWithoutTests;

    /// <summary>The full skill definition.</summary>
    public static readonly Skill Definition = new(
        CanonicalName: "add-narrative-tracing",
        SkillClass: SkillClass.Guided,
        Description:
            "Adds NarrativeTrace to a .NET project end to end: installs the real toolchain with a " +
            "frozen restore, wraps a service and renders its first trace, sends the trace to a " +
            "logger with one call, finishes by running the doctor CLI to confirm the install, and " +
            "ends by previewing the agent skills install so the next session finds them. " +
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
                Title: "Wire the frameworks this project already uses",
                Body: new CommandStep(["dotnet tool run dotnet-narrativetrace doctor --json"]),
                Verify: SkillCommands.FrameworkFixesApplied,
                Failure:
                [
                    new FailureNote(
                        Symptom: "the same framework finding fails again after its fix was applied",
                        Cause: "the snippet was pasted as printed instead of adapted to this project — " +
                            "its service and namespace names are the fixture's, not this project's",
                        Fix: "keep the printed calls, swap in this project's own services and namespaces, " +
                            "rerun the doctor; if it still fails, report it with the narrativetrace-feedback skill"),
                    new FailureNote(
                        Symptom: "a service under an included namespace is not traced by AddNarrativeTracing",
                        Cause: "generated code is never traced, and the shipped default exclude list leaves " +
                            "framework, mocking and test-runner namespaces alone; an exclude always wins over an include",
                        Fix: "read the doctor's config.exclude finding for the effective list; ExcludeNamespaces(...) " +
                            "or NARRATIVETRACE_EXCLUDE replaces that default as a whole, so repeat any prefix you still want"),
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
                    new FailureNote(
                        Symptom: "a span is named after the interface, not the class that implements it",
                        Cause: "the proxy wraps an interface and names its spans after it; there is no " +
                            "typeless target to name",
                        Fix: "keep the interface name, or pass `new ProxyOptions(ClassName: \"OrderService\")` " +
                            "to the proxy to name the span yourself"),
                    new FailureNote(
                        Symptom: "a parameter renders as arg0, arg1",
                        Cause: "reflection reads parameter names from the interface method, and this one " +
                            "carries none (an interface emitted at run time without parameter names)",
                        Fix: "put `[Traced(\"customerId\", \"productId\")]` on the interface method, one name per " +
                            "parameter"),
                ],
                Condition: FirstTraceCondition),
            new SkillStep(
                Title: "Name the service: set NARRATIVETRACE_SERVICE_NAME, or `new ServiceIdentity(\"order-service\")` " +
                    "in the config the context is built from",
                Body: new CommandStep(["dotnet tool run dotnet-narrativetrace doctor --json"]),
                Verify: "the doctor's config.service-identity finding reports \"passed\": true",
                Failure:
                [
                    new FailureNote(
                        Symptom: "spans show the service as unknown_service:dotnet, on the platform or in a trace backend",
                        Cause: "no service name was set, and NarrativeTrace never guesses one from the assembly",
                        Fix: "set NARRATIVETRACE_SERVICE_NAME (and NARRATIVETRACE_SERVICE_VERSION, " +
                            "NARRATIVETRACE_ENVIRONMENT) in the environment, or pass " +
                            "`new NarrativeTraceConfig(serviceIdentity: new ServiceIdentity(\"order-service\"))` to the " +
                            "context, `options.ServiceIdentity` to AddNarrativeTracing or AddNarrativeTrace, or " +
                            "`NarrativeTrace:ServiceName` in appsettings.json"),
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
            new SkillStep(
                Title: "Install the skills for next time",
                Body: new CommandStep(["dotnet tool run dotnet-narrativetrace init --dry-run"]),
                Verify: SkillCommands.InitPreviewThenNextSessionVerifies,
                Failure:
                [
                    new FailureNote(
                        Symptom: "command not found: dotnet-narrativetrace",
                        Cause: "NarrativeTrace.Cli is not installed as a local tool in this project yet",
                        Fix: "run this skill's own first step, or install it directly with " +
                            "`dotnet tool install NarrativeTrace.Cli`"),
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
            new ReasonedRule(
                "Never apply the installer without showing its diff first",
                "it writes into AGENTS.md and the project's skill directories, and the approval for " +
                "that is a person reading the diff — run it with --dry-run, show the output, and " +
                "let them run it again without the flag"),
        ]);
}
