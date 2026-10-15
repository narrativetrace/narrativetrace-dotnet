// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The Tier B trial flow: scaffold the case fixture into a scratch directory, drive the agent
/// command against the prompt, run the case's grader, and append one ledger row — factored out of
/// the entry point so it runs under a unit test with a fake process spawner, a fixed clock and a
/// temp ledger file, never a real subprocess or a real CLI.
///
/// <para>Every turn's agent command and the grader run through the same <see cref="ProcessRunner"/>
/// seam as an argv list (built by <see cref="AgentArgv"/>) — never a shell string — so a prompt
/// containing shell metacharacters can never be reinterpreted.</para>
///
/// <para>A trial's TRANSCRIPT and its stand-in log live in <see cref="TrialEnvironment"/>'s work
/// directory, outside the scaffolded project, and only the grader is told where. A record the
/// subject could read or rewrite is not evidence.</para>
/// </summary>
public sealed class EvalTrial
{
    /// <summary>
    /// Spawns <paramref name="argv"/> in <paramref name="cwd"/> with extra env and returns the exit
    /// code; with <paramref name="stdoutTo"/> set, its STANDARD OUTPUT is appended to that file (the
    /// transcript a grader reads) while its errors stay visible to whoever started the trial.
    /// </summary>
    public delegate int ProcessRunner(
        IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env, string? stdoutTo);

    private static readonly string[] ExcludedTopLevel = ["bin", "obj", ".git", ".vs", "TestResults"];

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly ProcessRunner _run;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _runsPath;
    private readonly RegistryDelivery _delivery;
    private readonly TrialEnvironment _environment;

    /// <summary>A case whose skill pages the harness itself copies in — every case but a registry one.</summary>
    public EvalTrial(ProcessRunner run, Func<DateTimeOffset> clock, string runsPath)
        : this(run, clock, runsPath, RegistryDelivery.None())
    {
    }

    /// <summary>A trial that keeps no transcript and installs no stand-ins — the unit-test shape.</summary>
    public EvalTrial(ProcessRunner run, Func<DateTimeOffset> clock, string runsPath, RegistryDelivery delivery)
        : this(run, clock, runsPath, delivery, TrialEnvironment.None())
    {
    }

    /// <summary>Wires the process seam, the clock, the ledger path, how this case got its pages, and what it records.</summary>
    public EvalTrial(
        ProcessRunner run, Func<DateTimeOffset> clock, string runsPath, RegistryDelivery delivery, TrialEnvironment environment)
    {
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _runsPath = runsPath ?? throw new ArgumentNullException(nameof(runsPath));
        _delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>The production seam: a real OS process, argv passed directly — never through a shell.</summary>
    public static int RealProcess(
        IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env, string? stdoutTo)
    {
        using var process = Process.Start(StartInfo(argv, cwd, env, stdoutTo))
            ?? throw new InvalidOperationException($"failed to start: {string.Join(' ', argv)}");
        if (stdoutTo is not null)
        {
            using var transcript = new FileStream(stdoutTo, FileMode.Append, FileAccess.Write);
            process.StandardOutput.BaseStream.CopyTo(transcript);
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static ProcessStartInfo StartInfo(
        IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env, string? stdoutTo)
    {
        var psi = new ProcessStartInfo
        {
            FileName = argv[0],
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = stdoutTo is not null,
        };
        foreach (var arg in argv.Skip(1))
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env)
        {
            psi.Environment[key] = value;
        }

        return psi;
    }

    /// <summary>
    /// Runs one trial into <paramref name="scratch"/> (created and deleted by the caller): copies
    /// the fixture in, drives every turn <paramref name="turns"/> declares (none when it drives no
    /// agent), runs the case's grader, appends one ledger row, and returns whether the grader passed.
    /// </summary>
    /// <exception cref="InvalidOperationException">a registry pre-step or any turn exited non-zero (a crash).</exception>
    public bool Run(
        EvalRunnerArgs args,
        string repoRoot,
        string fixtureDir,
        string caseDir,
        string scratch,
        AgentTurns turns,
        int trial)
    {
        ArgumentNullException.ThrowIfNull(turns);
        Scaffold(repoRoot, fixtureDir, caseDir, scratch);
        RunPreStep(repoRoot, scratch);
        if (turns.DrivesAnAgent)
        {
            DriveTheConversation(turns, scratch);
        }

        var pass = RunGrader(caseDir, scratch, repoRoot) == 0;
        AppendLedgerRow(new RunLedgerRow(
            _clock().UtcDateTime.ToString("o"), args.Skill, args.CaseName, args.Platform, args.Model, trial, pass));
        return pass;
    }

    /// <summary>
    /// The fixture always; the harness's own rendered pages ONLY when no registry delivers them —
    /// a registry case measures the state the registry left behind, and a page the harness wrote
    /// over it would answer the case's own question. A case that asks what the INSTALL leaves behind
    /// declares <c>"seedSkills": false</c> for the same reason.
    /// </summary>
    private void Scaffold(string repoRoot, string fixtureDir, string caseDir, string scratch)
    {
        CopyRecursively(fixtureDir, scratch, skipRootReadme: true);
        if (_delivery.DeliversTheSkills() || !CaseFixture.SeedsTheRenderedSkills(caseDir))
        {
            return;
        }

        foreach (var layout in new[] { ".agents", ".claude" })
        {
            var skills = Path.Combine(repoRoot, layout, "skills");
            if (Directory.Exists(skills))
            {
                CopyRecursively(skills, Path.Combine(scratch, layout, "skills"));
            }
        }
    }

    /// <summary>
    /// The registry's own delivery, before the agent starts: the staged snapshot, then the
    /// documented commands a reader runs, in order, in the project itself. A command that exits
    /// non-zero CRASHES the trial rather than failing it — an absent vendor tool or an unreachable
    /// registry is not a verdict about the registry path, so it leaves no ledger row.
    /// </summary>
    private void RunPreStep(string repoRoot, string scratch)
    {
        if (!_delivery.DeliversTheSkills())
        {
            return;
        }

        Directory.CreateDirectory(_delivery.StagedSnapshot());
        foreach (var command in _delivery.Commands(repoRoot))
        {
            var exit = _run(command, scratch, _delivery.Environment(), null);
            if (exit != 0)
            {
                throw new InvalidOperationException(
                    $"registry pre-step exited {exit}: {string.Join(' ', command)}");
            }
        }
    }

    /// <summary>
    /// Every turn, in order: the user's words into the transcript first, then the agent's own turn
    /// with its standard output appended after them. A turn that exits non-zero stops the trial, so
    /// no later turn runs and the grader never does — a second turn driven after a failed first is
    /// an approval answering a question that was never asked, and its grader would read "nothing
    /// was filed" as a pass.
    /// </summary>
    private void DriveTheConversation(AgentTurns turns, string scratch)
    {
        for (var turn = 1; turn <= turns.TurnCount; turn++)
        {
            var prompt = turns.PromptForTurn(turn);
            if (_environment.IsRecording)
            {
                _environment.RecordUserTurn(turn, prompt);
            }

            RunOneTurn(turns.CommandForTurn(turn), prompt, scratch, turn);
        }
    }

    private void RunOneTurn(string command, string prompt, string scratch, int turn)
    {
        var argv = AgentArgv.Build(command, prompt);
        var stdoutTo = _environment.IsRecording ? _environment.Transcript() : null;
        var exit = _run(argv, scratch, Merged(_delivery.Environment(), _environment.AgentEnvironment()), stdoutTo);
        if (exit != 0)
        {
            throw new InvalidOperationException($"agent command for turn {turn} exited {exit}: {string.Join(' ', argv)}");
        }
    }

    /// <summary>
    /// The grader runs with the agent's environment PLUS what only it may know: where this trial's
    /// transcript and stand-in log are, the project it grades, and the built CLI it may invoke.
    /// </summary>
    private int RunGrader(string caseDir, string scratch, string repoRoot)
    {
        var grader = Path.GetFullPath(Path.Combine(caseDir, "graders", "verify.sh"));
        var env = Merged(_delivery.Environment(), _environment.GraderEnvironment());
        env["PROJECT_DIR"] = scratch;
        var cli = CliDll.Locate(repoRoot);
        if (cli is not null)
        {
            env["NARRATIVETRACE_CLI_DLL"] = cli;
        }

        return _run(["bash", grader], scratch, env, null);
    }

    private static Dictionary<string, string> Merged(
        IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
    {
        var merged = new Dictionary<string, string>(first, StringComparer.Ordinal);
        foreach (var (key, value) in second)
        {
            merged[key] = value;
        }

        return merged;
    }

    private void AppendLedgerRow(RunLedgerRow row)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_runsPath)!);
        File.AppendAllText(_runsPath, row.ToJsonLine() + "\n");
    }

    /// <param name="skipRootReadme">
    /// Leave out <c>README.md</c> at <paramref name="source"/>'s root: a fixture's root README describes
    /// the fixture — a hidden defect included — for its maintainers, never for the agent under test.
    /// </param>
    private static void CopyRecursively(string source, string target, bool skipRootReadme = false)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (IsExcluded(relative) || (skipRootReadme && relative == "README.md"))
            {
                continue;
            }

            var dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    // A trial must earn its reports: a fixture's previously built output is not evidence of success.
    private static bool IsExcluded(string relative)
    {
        var segments = relative.Split(PathSeparators);
        return relative != "." && segments.Any(segment => ExcludedTopLevel.Contains(segment));
    }
}
