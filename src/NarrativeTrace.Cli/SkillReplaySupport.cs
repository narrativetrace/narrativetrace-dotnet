// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;

namespace NarrativeTrace.Cli;

/// <summary>One step's replay outcome. <see cref="Ran"/> is false only when the step carried
/// nothing mechanical to replay (a judgmental step, or a narrative-only <c>verify</c>) — that is
/// reported <c>ok: true</c>, never a failure.</summary>
internal readonly record struct StepReplayResult(string Title, bool Ran, bool Ok, string? Detail);

/// <summary>
/// Tier A2 (skill-harness design §4.2): mechanically replays a skill's own step data against the
/// sixty-seconds fixture, no LLM. Green means the instructions are literally executable today
/// against this commit's code — the generic engine below is deliberately data-only (an injected
/// <c>runCommand</c>/<c>tryVerify</c> pair), unit-testable with fakes exactly like the golden
/// TypeScript source's <c>replaySkill</c> (<c>packages/skills/src/replay.ts</c>); the concrete,
/// process-spawning registry lives in <see cref="SkillReplayRegistry"/> so the decision logic
/// above it never itself needs a real process or a real fixture to test.
/// </summary>
internal static class SkillReplayEngine
{
    public static StepReplayResult ReplayStep(
        SkillStep step, string cwd, Func<string, string, bool> runCommand, Func<string, string, bool?> tryVerify)
    {
        var commands = step.Body is CommandStep commandStep ? commandStep.Commands : [];
        var ran = false;
        var ok = true;
        foreach (var command in commands)
        {
            ran = true;
            ok &= runCommand(command, cwd);
        }

        if (step.Verify is not null)
        {
            var verifyOk = tryVerify(step.Verify, cwd);
            if (verifyOk is not null)
            {
                ran = true;
                ok &= verifyOk.Value;
            }
        }

        return new StepReplayResult(step.Title, ran, ok, null);
    }

    public static IReadOnlyList<StepReplayResult> Replay(
        Skill skill, string cwd, Func<string, string, bool> runCommand, Func<string, string, bool?> tryVerify)
    {
        return skill.Steps.Select(step => ReplayStep(step, cwd, runCommand, tryVerify)).ToList();
    }
}

/// <summary>
/// The concrete, closed registry <see cref="SkillReplayEngine"/> is driven by in production: every
/// literal command string either catalogue skill's <see cref="CommandStep"/> can name must be
/// registered here, or replay fails loudly naming the offending string — the same "no drift goes
/// unnoticed" contract <see cref="SkillLints.VocabularyViolations"/> already enforces for the
/// vocabulary itself (skill-harness design principle 7).
///
/// <para>
/// A literal replay of every command as written is not always the safe or even the sensible thing
/// to do against a shared, committed fixture: <c>dotnet tool install NarrativeTrace.Cli</c> and
/// <c>dotnet add package NarrativeTrace.Proxy</c> both target the PUBLISHED package (this repo's
/// own <c>examples/NarrativeTrace.Examples.SixtySeconds</c> deliberately uses a <c>ProjectReference</c>
/// instead — see that project's own comment), so running them for real here would either hit the
/// network for a package this repo already builds from source, or permanently rewrite the
/// checked-in fixture's <c>.csproj</c>. Both replay instead as an in-repo structural check standing
/// for the same claim: the tool this repo ships really does install as <c>dotnet-narrativetrace</c>
/// (<see cref="CliPacksAsDotnetNarrativetraceTool"/>), and the fixture already carries the
/// dependency the command would add (<see cref="FixtureReferencesProxy"/>).
/// </para>
/// </summary>
internal static class SkillReplayRegistry
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    private delegate bool CommandExecutor(string repoRoot, string fixtureDir);

    private static readonly Dictionary<string, CommandExecutor> KnownCommands =
        new Dictionary<string, CommandExecutor>(StringComparer.Ordinal)
        {
            ["dotnet tool install NarrativeTrace.Cli"] =
                (repoRoot, _) => CliPacksAsDotnetNarrativetraceTool(repoRoot),
            ["dotnet add package NarrativeTrace.Proxy"] =
                (_, fixtureDir) => FixtureReferencesProxy(fixtureDir),
            // Amendment (mirrors the TypeScript reference's own npm→pnpm amendment note for its
            // fixture): plain `dotnet restore`, never `--locked-mode` — the fixture has no
            // committed packages.lock.json (it restores three in-repo ProjectReferences, not
            // packages, so there is nothing to lock), and --locked-mode's whole point is failing
            // without one. This only changes what THIS replay executes against ITS OWN fixture; a
            // real consumer's own project (with its own lock file) is unaffected.
            ["dotnet restore --locked-mode"] =
                (_, fixtureDir) => RunProcess("dotnet", "restore", fixtureDir).ExitCode == 0,
            ["dotnet tool run dotnet-narrativetrace doctor --json"] =
                (repoRoot, fixtureDir) => RunDoctorJson(repoRoot, fixtureDir).IsWellFormed,
            // The fixture itself is a console example, not a test host — its own real, committed
            // test project (tests/NarrativeTrace.Examples.SixtySeconds.Tests) is the safe,
            // fixture-scoped equivalent of "run the tests a real consumer would already have":
            // it exercises Program.cs verbatim, plus the "Send it to your logger" postscript's own
            // sibling project (examples/NarrativeTrace.Examples.SixtySeconds.WithLogger/Program.cs,
            // invoked via reflection on its own compiled entry point — see that test project's own
            // remarks).
            ["dotnet test"] = (repoRoot, _) => RunSixtySecondsTests(repoRoot).ExitCode == 0,
        };

    private delegate bool? VerifyChecker(string repoRoot, string fixtureDir);

    private static readonly Dictionary<string, VerifyChecker> KnownVerifies =
        new Dictionary<string, VerifyChecker>(StringComparer.Ordinal)
        {
            [SkillCommands.DoctorReportWellFormed] =
                (repoRoot, fixtureDir) => DoctorReportWellFormed(repoRoot, fixtureDir),
            [SkillCommands.ToolchainChecksHold] =
                (repoRoot, fixtureDir) => ToolchainChecksHold(repoRoot, fixtureDir),
            [SkillCommands.ApprovalTracesFindingPasses] =
                (repoRoot, fixtureDir) => FindingPasses(repoRoot, fixtureDir, "trap.approval-traces"),
            ["`dotnet run` prints a \"trace: <name> (<id>)\" line followed by the call tree"] =
                (_, fixtureDir) => DotnetRunPrintsTraceLine(fixtureDir),
            // Deliberately NOT registered — SkillCommands.RedactionProofFindingPasses and
            // SkillCommands.ExitCodeIsZero both assert an OUTCOME (a specific finding passing, or
            // the doctor CLI's own exit code) that depends on the consuming project having already
            // added a redaction test — something the pristine sixty-seconds fixture, by design,
            // has not done (it is a minimal quickstart, not a "doctor-clean" reference project).
            // Confirmed against a real run: pointed at the fixture directory, doctor reports
            // trap.redaction-proof failed and exits 1, exactly as doctor is SUPPOSED to behave for
            // a project that hasn't written that test yet — wiring either claim here would either
            // make this gate permanently red, or (worse) require weakening the fixture's honesty
            // to make it pass. Mirrors the golden TypeScript/Java sources, which check a finding's
            // PRESENCE/well-formedness here, never its pass/fail value, for the identical reason.
            // The console-logger verify (add-narrative-tracing's "Send it to your logger" step) is
            // also left unregistered: its own project's Program.cs is a real `dotnet run` entry
            // point, but the step's Verify text is prose, not one of the SkillCommands constants
            // this replay matches against — already proven for real by
            // SixtySecondsTests.Sends_the_trace_to_its_logger (reflection on that project's own
            // compiled entry point) and contract-probe's TraceLogExporterProbe, just not by a
            // command this replay can name.
        };

    public static bool RunCommand(string command, string repoRoot, string fixtureDir)
    {
        if (!KnownCommands.TryGetValue(command, out var executor))
        {
            throw new InvalidOperationException(
                $"SkillReplayRegistry has no registered executor for: \"{command}\" — register it "
                    + "in SkillReplayRegistry.KnownCommands, or explain in a comment why it cannot "
                    + "safely be replayed.");
        }

        return executor(repoRoot, fixtureDir);
    }

    public static bool? TryVerify(string verify, string repoRoot, string fixtureDir)
    {
        return KnownVerifies.TryGetValue(verify, out var checker) ? checker(repoRoot, fixtureDir) : null;
    }

    // ── Command executors ───────────────────────────────────────────────────

    private static bool CliPacksAsDotnetNarrativetraceTool(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "src", "NarrativeTrace.Cli", "NarrativeTrace.Cli.csproj");
        if (!File.Exists(path))
        {
            return false;
        }

        var text = File.ReadAllText(path);
        return text.Contains("<PackAsTool>true</PackAsTool>", StringComparison.Ordinal)
            && text.Contains("<ToolCommandName>dotnet-narrativetrace</ToolCommandName>", StringComparison.Ordinal)
            && text.Contains("<PackageId>NarrativeTrace.Cli</PackageId>", StringComparison.Ordinal);
    }

    private static bool FixtureReferencesProxy(string fixtureDir)
    {
        var csproj = Directory.EnumerateFiles(fixtureDir, "*.csproj").FirstOrDefault();
        return csproj is not null
            && File.ReadAllText(csproj).Contains("NarrativeTrace.Proxy", StringComparison.Ordinal);
    }

    private static (bool IsWellFormed, JsonDocument? Report) RunDoctorJson(string repoRoot, string fixtureDir)
    {
        var cliProject = Path.Combine(repoRoot, "src", "NarrativeTrace.Cli");
        var result = RunProcess(
            "dotnet", $"run --project \"{cliProject}\" --no-build -- doctor --json --dir \"{fixtureDir}\"", repoRoot);
        try
        {
            var document = JsonDocument.Parse(result.StdOut);
            var wellFormed = document.RootElement.TryGetProperty("findings", out var findings)
                && findings.ValueKind == JsonValueKind.Array
                && findings.GetArrayLength() > 0;
            return (wellFormed, document);
        }
        catch (JsonException)
        {
            return (false, null);
        }
    }

    private static ProcessResult RunSixtySecondsTests(string repoRoot)
    {
        var project = Path.Combine(repoRoot, "tests", "NarrativeTrace.Examples.SixtySeconds.Tests");
        return RunProcess("dotnet", $"test \"{project}\" --no-build", repoRoot);
    }

    private static bool DotnetRunPrintsTraceLine(string fixtureDir)
    {
        var result = RunProcess("dotnet", "run --no-build", fixtureDir);
        return result.ExitCode == 0 && TraceLine.IsMatch(result.StdOut);
    }

    private static readonly Regex TraceLine = new(@"trace: .+\(.+\)", RegexOptions.Compiled);

    // ── Verify checkers ──────────────────────────────────────────────────────

    private static bool DoctorReportWellFormed(string repoRoot, string fixtureDir)
    {
        var (wellFormed, report) = RunDoctorJson(repoRoot, fixtureDir);
        if (!wellFormed || report is null)
        {
            return false;
        }

        return report.RootElement.GetProperty("findings").EnumerateArray().All(finding =>
            finding.GetProperty("passed").GetBoolean()
                || !string.IsNullOrEmpty(finding.GetProperty("fix").GetString()));
    }

    private static bool ToolchainChecksHold(string repoRoot, string fixtureDir)
    {
        var (wellFormed, report) = RunDoctorJson(repoRoot, fixtureDir);
        if (!wellFormed || report is null)
        {
            return false;
        }

        return report.RootElement.GetProperty("findings").EnumerateArray()
            .Where(finding => finding.GetProperty("id").GetString()!.StartsWith("toolchain.", StringComparison.Ordinal))
            .All(finding => finding.GetProperty("passed").GetBoolean());
    }

    private static bool FindingPasses(string repoRoot, string fixtureDir, string findingId)
    {
        var (wellFormed, report) = RunDoctorJson(repoRoot, fixtureDir);
        if (!wellFormed || report is null)
        {
            return false;
        }

        return report.RootElement.GetProperty("findings").EnumerateArray()
            .Any(finding => finding.GetProperty("id").GetString() == findingId
                && finding.GetProperty("passed").GetBoolean());
    }

    // ── Process plumbing ─────────────────────────────────────────────────────

    private readonly record struct ProcessResult(int ExitCode, string StdOut);

    private static ProcessResult RunProcess(string fileName, string arguments, string workingDirectory)
    {
        using var process = StartProcess(fileName, arguments, workingDirectory);
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        AwaitProcess(process, fileName, arguments);
        return new ProcessResult(process.ExitCode, output.ToString());
    }

    private static Process StartProcess(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return Process.Start(psi) ?? throw new InvalidOperationException($"failed to start: {fileName} {arguments}");
    }

    private static void AwaitProcess(Process process, string fileName, string arguments)
    {
        if (process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            return;
        }

        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* already exited */ }
        throw new TimeoutException($"replay command timed out after {ProcessTimeout}: {fileName} {arguments}");
    }
}
